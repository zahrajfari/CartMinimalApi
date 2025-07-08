public class InMemoryEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = new();

    public async Task PublishAsync<T>(T eventData) where T : class
    {
        var eventType = typeof(T);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            var tasks = handlers.Select(handler => handler(eventData));
            await Task.WhenAll(tasks);
        }
    }

    public void Subscribe<T>(Func<T, Task> handler) where T : class
    {
        var eventType = typeof(T);
        if (!_handlers.ContainsKey(eventType)) _handlers[eventType] = new List<Func<object, Task>>();

        _handlers[eventType].Add(async obj => await handler((T) obj));
    }
}