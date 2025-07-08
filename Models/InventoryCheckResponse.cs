public record InventoryCheckResponse(
    bool AllItemsInStock,
    List<InventoryItemStatus> Items,
    List<string> Issues
);