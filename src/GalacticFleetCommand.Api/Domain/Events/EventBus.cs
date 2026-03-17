using System.Collections.Concurrent;

namespace GalacticFleetCommand.Api.Domain.Events;

public interface IEventBus
{
    void Publish(FleetEvent fleetEvent);
    void Subscribe(FleetEventType type, Action<FleetEvent> handler);
    IReadOnlyList<FleetEvent> GetTimeline(Guid fleetId);
}

public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Guid, List<FleetEvent>> _events = new();
    private readonly Dictionary<FleetEventType, List<Action<FleetEvent>>> _subscribers = [];
    private readonly object _subscriberLock = new();

    public void Publish(FleetEvent fleetEvent)
    {
        var list = _events.GetOrAdd(fleetEvent.FleetId, _ => []);
        lock (list)
        {
            list.Add(fleetEvent);
        }

        List<Action<FleetEvent>> handlers;
        lock (_subscriberLock)
        {
            if (!_subscribers.TryGetValue(fleetEvent.Type, out var subs))
                return;
            handlers = [.. subs];
        }

        foreach (var handler in handlers)
        {
            handler(fleetEvent);
        }
    }

    public void Subscribe(FleetEventType type, Action<FleetEvent> handler)
    {
        lock (_subscriberLock)
        {
            if (!_subscribers.TryGetValue(type, out var list))
            {
                list = [];
                _subscribers[type] = list;
            }
            list.Add(handler);
        }
    }

    public IReadOnlyList<FleetEvent> GetTimeline(Guid fleetId)
    {
        if (!_events.TryGetValue(fleetId, out var list))
            return [];

        lock (list)
        {
            return list.OrderBy(e => e.Timestamp).ToList().AsReadOnly();
        }
    }
}
