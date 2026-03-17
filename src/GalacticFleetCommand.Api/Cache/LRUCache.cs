namespace GalacticFleetCommand.Api.Cache;

/// <summary>
/// O(1) LRU cache using a hand-rolled doubly-linked list + Dictionary.
/// Thread-safe via a single lock.
/// </summary>
public class LRUCache<TKey, TValue> where TKey : notnull
{
    private sealed class Node
    {
        public TKey Key = default!;
        public TValue Value = default!;
        public Node? Prev;
        public Node? Next;
    }

    private readonly int _capacity;
    private readonly Dictionary<TKey, Node> _map;
    private readonly Node _head; // sentinel
    private readonly Node _tail; // sentinel
    private readonly object _lock = new();

    public LRUCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _map = new Dictionary<TKey, Node>(capacity);

        _head = new Node();
        _tail = new Node();
        _head.Next = _tail;
        _tail.Prev = _head;
    }

    public int Count
    {
        get { lock (_lock) { return _map.Count; } }
    }

    public TValue? Get(TKey key)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node))
                return default;

            MoveToHead(node);
            return node.Value;
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    public void Put(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                node.Value = value;
                MoveToHead(node);
                return;
            }

            var newNode = new Node { Key = key, Value = value };
            AddAfterHead(newNode);
            _map[key] = newNode;

            if (_map.Count > _capacity)
            {
                var lru = _tail.Prev!;
                RemoveNode(lru);
                _map.Remove(lru.Key);
            }
        }
    }

    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node))
                return false;

            RemoveNode(node);
            _map.Remove(key);
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head.Next = _tail;
            _tail.Prev = _head;
            _map.Clear();
        }
    }

    private void MoveToHead(Node node)
    {
        RemoveNode(node);
        AddAfterHead(node);
    }

    private void AddAfterHead(Node node)
    {
        node.Prev = _head;
        node.Next = _head.Next;
        _head.Next!.Prev = node;
        _head.Next = node;
    }

    private static void RemoveNode(Node node)
    {
        node.Prev!.Next = node.Next;
        node.Next!.Prev = node.Prev;
    }
}
