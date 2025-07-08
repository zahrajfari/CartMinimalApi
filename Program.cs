using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

AddServices(builder);
RegisterServices(builder);

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("cart-repository", () => HealthCheckResult.Healthy())
    .AddCheck("product-repository", () => HealthCheckResult.Healthy());

// Background Services
builder.Services.AddHostedService<CartExpirationService>();
builder.Services.AddHostedService<AnalyticsProcessorService>();

var app = builder.Build();

// Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Custom Middleware
app.UseMiddleware<MockResponseMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// SignalR Hub
app.MapHub<CartHub>("/cartHub");

// Health Check
app.MapHealthChecks("/health");

// Cart Management Endpoints
app.MapGet("/api/v1/carts/{userId}", async (string userId, ICartService cartService) =>
    {
        var cart = await cartService.GetOrCreateCartAsync(userId);
        var response = new CartSummaryResponse(
            cart.UserId,
            cart.Items.Count,
            cart.SubTotal,
            cart.TaxAmount,
            cart.ShippingAmount,
            cart.DiscountAmount,
            cart.TotalAmount,
            cart.Currency,
            cart.Items.Select(i => new CartItemResponse(
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice,
                i.Currency,
                i.Status,
                true, // Mock stock status
                i.Notes
            )).ToList(),
            cart.AppliedCoupons,
            cart.Status
        );

        return Results.Ok(response);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("GetCart")
    .WithOpenApi();

app.MapPost("/api/v1/carts/{userId}/items", async (
        string userId,
        AddToCartRequest request,
        ICartService cartService,
        IValidator<AddToCartRequest> validator) =>
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return Results.BadRequest(validationResult.Errors);

        var item = await cartService.AddToCartAsync(userId, request);
        return Results.Created($"/api/v1/carts/{userId}/items/{item.ProductId}", item);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("AddToCart")
    .WithOpenApi();

app.MapPut("/api/v1/carts/{userId}/items/{productId:int}", async (
        string userId,
        int productId,
        UpdateCartItemRequest request,
        ICartService cartService,
        IValidator<UpdateCartItemRequest> validator) =>
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return Results.BadRequest(validationResult.Errors);

        var item = await cartService.UpdateCartItemAsync(userId, productId, request);
        return Results.Ok(item);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("UpdateCartItem")
    .WithOpenApi();

app.MapDelete("/api/v1/carts/{userId}/items/{productId:int}", async (
        string userId,
        int productId,
        ICartService cartService) =>
    {
        await cartService.RemoveFromCartAsync(userId, productId);
        return Results.NoContent();
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("RemoveFromCart")
    .WithOpenApi();

app.MapDelete("/api/v1/carts/{userId}", async (string userId, ICartService cartService) =>
    {
        await cartService.ClearCartAsync(userId);
        return Results.NoContent();
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("ClearCart")
    .WithOpenApi();

// Inventory Management Endpoints
app.MapGet("/api/v1/carts/{userId}/inventory-check", async (string userId, ICartService cartService) =>
    {
        var result = await cartService.CheckInventoryAsync(userId);
        return Results.Ok(result);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("CheckInventory")
    .WithOpenApi();

app.MapGet("/api/v1/products/{productId:int}/stock", async (int productId, IProductRepository productRepository) =>
    {
        var product = await productRepository.GetProductAsync(productId);
        if (product == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            productId = product.Id,
            stockQuantity = product.StockQuantity,
            status = product.Status,
            isAvailable = product.StockQuantity > 0 && product.Status == ProductStatus.Available
        });
    })
    .RequireRateLimiting("default")
    .WithName("GetProductStock")
    .WithOpenApi();

app.MapPost("/api/v1/carts/{userId}/validate-stock", async (string userId, ICartService cartService) =>
    {
        var result = await cartService.CheckInventoryAsync(userId);

        if (!result.AllItemsInStock)
            return Results.BadRequest(new
            {
                message = "Some items are out of stock",
                issues = result.Issues
            });

        return Results.Ok(new {message = "All items are in stock"});
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("ValidateStock")
    .WithOpenApi();

// Cart Item Status Management
app.MapGet("/api/v1/carts/{userId}/items/{productId:int}/status", async (
        string userId,
        int productId,
        ICartService cartService) =>
    {
        var cart = await cartService.GetOrCreateCartAsync(userId);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            productId = item.ProductId,
            status = item.Status,
            lastUpdated = item.UpdatedAt,
            notes = item.Notes
        });
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("GetCartItemStatus")
    .WithOpenApi();

app.MapPut("/api/v1/carts/{userId}/items/{productId:int}/status", async (
        string userId,
        int productId,
        CartItemStatus status,
        ICartRepository cartRepository) =>
    {
        var cart = await cartRepository.GetCartAsync(userId);
        if (cart == null)
            return Results.NotFound();

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            return Results.NotFound();

        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;
        cart.UpdatedAt = DateTime.UtcNow;

        await cartRepository.SaveCartAsync(cart);

        return Results.Ok(item);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("UpdateCartItemStatus")
    .WithOpenApi();

// Product Management Endpoints
app.MapGet("/api/v1/products",
        async (IProductRepository productRepository, string? search = null, int page = 1, int size = 20) =>
        {
            var products = string.IsNullOrEmpty(search)
                ? await productRepository.GetProductsAsync()
                : await productRepository.SearchProductsAsync(search);

            var pagedProducts = products
                .Skip((page - 1) * size)
                .Take(size)
                .Select(p => new ProductResponse(
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.Currency,
                    p.StockQuantity,
                    p.Category,
                    p.ImageUrl,
                    p.IsActive,
                    p.Tags,
                    p.Status
                ))
                .ToList();

            return Results.Ok(new
            {
                products = pagedProducts,
                pagination = new
                {
                    page,
                    size,
                    total = products.Count,
                    totalPages = (int) Math.Ceiling(products.Count / (double) size)
                }
            });
        })
    .RequireRateLimiting("default")
    .WithName("GetProducts")
    .WithOpenApi();

app.MapGet("/api/v1/products/{productId:int}", async (int productId, IProductRepository productRepository) =>
    {
        var product = await productRepository.GetProductAsync(productId);
        if (product == null)
            return Results.NotFound();

        var response = new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Currency,
            product.StockQuantity,
            product.Category,
            product.ImageUrl,
            product.IsActive,
            product.Tags,
            product.Status
        );

        return Results.Ok(response);
    })
    .RequireRateLimiting("default")
    .WithName("GetProduct")
    .WithOpenApi();

// Discount & Coupon Endpoints
app.MapPost("/api/v1/carts/{userId}/coupons", async (
        string userId,
        ApplyCouponRequest request,
        ICartService cartService,
        IDiscountService discountService,
        ICartRepository cartRepository) =>
    {
        var cart = await cartService.GetOrCreateCartAsync(userId);
        var discount = await discountService.GetDiscountAsync(request.CouponCode);

        if (discount == null)
            return Results.BadRequest(new {error = "Invalid coupon code"});

        if (cart.AppliedCoupons.Contains(request.CouponCode))
            return Results.BadRequest(new {error = "Coupon already applied"});

        await discountService.ApplyDiscountAsync(cart, request.CouponCode);
        await cartRepository.SaveCartAsync(cart);

        return Results.Ok(new
        {
            message = "Coupon applied successfully",
            discountAmount = cart.DiscountAmount,
            totalAmount = cart.TotalAmount
        });
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("ApplyCoupon")
    .WithOpenApi();

// Cart Sharing Endpoints
app.MapPost("/api/v1/carts/{userId}/share", async (string userId, ICartService cartService) =>
    {
        var shareResponse = await cartService.ShareCartAsync(userId);
        return Results.Ok(shareResponse);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("ShareCart")
    .WithOpenApi();

app.MapGet("/api/v1/carts/shared/{shareToken}", async (string shareToken, ICartRepository cartRepository) =>
    {
        var cart = await cartRepository.GetSharedCartAsync(shareToken);
        if (cart == null)
            return Results.NotFound();

        var response = new CartSummaryResponse(
            cart.UserId,
            cart.Items.Count,
            cart.SubTotal,
            cart.TaxAmount,
            cart.ShippingAmount,
            cart.DiscountAmount,
            cart.TotalAmount,
            cart.Currency,
            cart.Items.Select(i => new CartItemResponse(
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice,
                i.Currency,
                i.Status,
                true,
                i.Notes
            )).ToList(),
            cart.AppliedCoupons,
            cart.Status
        );

        return Results.Ok(response);
    })
    .RequireRateLimiting("default")
    .WithName("GetSharedCart")
    .WithOpenApi();

// Analytics Endpoints
app.MapGet("/api/v1/analytics/metrics", async (IAnalyticsService analyticsService) =>
    {
        var metrics = await analyticsService.GetCartMetricsAsync();
        return Results.Ok(metrics);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("GetAnalyticsMetrics")
    .WithOpenApi();

// Bulk Operations
app.MapPost("/api/v1/carts/{userId}/items/bulk", async (
        string userId,
        List<AddToCartRequest> requests,
        ICartService cartService) =>
    {
        var results = new List<object>();

        foreach (var request in requests)
            try
            {
                var item = await cartService.AddToCartAsync(userId, request);
                results.Add(new {success = true, item});
            }
            catch (Exception ex)
            {
                results.Add(new {success = false, error = ex.Message, productId = request.ProductId});
            }

        return Results.Ok(new {results});
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("BulkAddToCart")
    .WithOpenApi();

// Save for Later
app.MapPost("/api/v1/carts/{userId}/items/{productId:int}/save-for-later", async (
        string userId,
        int productId,
        ICartRepository cartRepository) =>
    {
        var cart = await cartRepository.GetCartAsync(userId);
        if (cart == null)
            return Results.NotFound();

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            return Results.NotFound();

        item.Status = CartItemStatus.SavedForLater;
        item.UpdatedAt = DateTime.UtcNow;
        await cartRepository.SaveCartAsync(cart);

        return Results.Ok(new {message = "Item saved for later"});
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("SaveForLater")
    .WithOpenApi();

app.MapGet("/api/v1/carts/{userId}/saved-items", async (string userId, ICartRepository cartRepository) =>
    {
        var cart = await cartRepository.GetCartAsync(userId);
        if (cart == null)
            return Results.Ok(new List<CartItem>());

        var savedItems = cart.Items.Where(i => i.Status == CartItemStatus.SavedForLater).ToList();
        return Results.Ok(savedItems);
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("GetSavedItems")
    .WithOpenApi();

// Wishlist Integration
app.MapPost("/api/v1/wishlists/{userId}/items/{productId:int}/move-to-cart", async (
        string userId,
        int productId,
        ICartService cartService,
        IProductRepository productRepository) =>
    {
        var product = await productRepository.GetProductAsync(productId);
        if (product == null)
            return Results.NotFound();

        var request = new AddToCartRequest(productId, 1);
        var item = await cartService.AddToCartAsync(userId, request);

        return Results.Ok(new
        {
            message = "Item moved to cart",
            item
        });
    })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("MoveWishlistToCart")
    .WithOpenApi();

// Export Functionality
app.MapGet("/api/v1/carts/{userId}/export",
        async (string userId, ICartRepository cartRepository, string format = "json") =>
        {
            var cart = await cartRepository.GetCartAsync(userId);
            if (cart == null)
                return Results.NotFound();

            return format.ToLower() switch
            {
                "json" => Results.Ok(cart),
                "csv" => Results.Text(ConvertCartToCsv(cart), "text/csv"),
                _ => Results.BadRequest(new {error = "Unsupported format. Use 'json' or 'csv'"})
            };
        })
    .RequireAuthorization()
    .RequireRateLimiting("default")
    .WithName("ExportCart")
    .WithOpenApi();

// Testing & Development Endpoints
app.MapPost("/api/v1/test/chaos", async (HttpContext context) =>
    {
        var random = new Random();
        var scenarios = new[]
        {
            () => throw new InvalidOperationException("Simulated chaos error"),
            () => Task.Delay(5000), // Slow response
            () => Task.CompletedTask // Normal response
        };

        var scenario = scenarios[random.Next(scenarios.Length)];
        await scenario();

        return Results.Ok(new {message = "Chaos test completed"});
    })
    .RequireRateLimiting("default")
    .WithName("ChaosTest")
    .WithOpenApi();

// Performance Test Endpoints
app.MapGet("/api/v1/test/performance/{operations:int}", async (int operations) =>
    {
        var stopwatch = Stopwatch.StartNew();

        // Simulate heavy operations
        for (var i = 0; i < operations; i++) await Task.Delay(1);

        stopwatch.Stop();

        return Results.Ok(new
        {
            operations,
            duration = stopwatch.ElapsedMilliseconds,
            operationsPerSecond = operations / (stopwatch.ElapsedMilliseconds / 1000.0)
        });
    })
    .RequireRateLimiting("default")
    .WithName("PerformanceTest")
    .WithOpenApi();

// Authentication Endpoints (Mock)
app.MapPost("/api/v1/auth/login", (LoginRequest request) =>
    {
        // Mock authentication
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim("user_id", request.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSuperSecretKeyHereThatIsLongEnough123456"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            "CartAPI",
            "CartUsers",
            claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: creds);

        return Results.Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expires = DateTime.Now.AddHours(1)
        });
    })
    .WithName("Login")
    .WithOpenApi();

app.Run();

static string ConvertCartToCsv(ShoppingCart cart)
{
    var csv = new StringBuilder();
    csv.AppendLine("ProductId,ProductName,Quantity,UnitPrice,TotalPrice,Status");

    foreach (var item in cart.Items)
        csv.AppendLine(
            $"{item.ProductId},{item.ProductName},{item.Quantity},{item.UnitPrice},{item.TotalPrice},{item.Status}");

    return csv.ToString();
}

void AddServices(WebApplicationBuilder webApplicationBuilder)
{
    // Services
    webApplicationBuilder.Services.AddEndpointsApiExplorer();
    webApplicationBuilder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Enterprise Shopping Cart API",
            Version = "v1.0",
            Description = "Complete shopping cart solution with advanced features"
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                []
            }
        });
    });

// JWT Authentication
    webApplicationBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "CartAPI",
                ValidAudience = "CartUsers",
                IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSuperSecretKeyHereThatIsLongEnough123456"))
            };
        });

    webApplicationBuilder.Services.AddAuthorization();

// Rate Limiting
    webApplicationBuilder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("default", opt =>
        {
            opt.PermitLimit = 100;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 50;
        });
    });

// SignalR
    webApplicationBuilder.Services.AddSignalR();

// CORS
    webApplicationBuilder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
}

void RegisterServices(WebApplicationBuilder builder1)
{
    // Services
    builder1.Services.AddSingleton<ICartRepository, InMemoryCartRepository>();
    builder1.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
    builder1.Services.AddSingleton<IEventBus, InMemoryEventBus>();
    builder1.Services.AddScoped<ICartService, CartService>();
    builder1.Services.AddScoped<IDiscountService, DiscountService>();
    builder1.Services.AddScoped<IAnalyticsService, AnalyticsService>();
    builder1.Services.AddValidatorsFromAssemblyContaining<Program>();
}