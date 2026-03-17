using GalacticFleetCommand.Api.Cache;

namespace GalacticFleetCommand.Tests.Cache;

public class LRUCacheTests
{
    [Fact]
    public void Get_ReturnsDefault_WhenKeyNotPresent()
    {
        var cache = new LRUCache<string, int>(3);
        Assert.Equal(0, cache.Get("missing"));
    }

    [Fact]
    public void Put_And_Get_WorkCorrectly()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);

        Assert.Equal(1, cache.Get("a"));
        Assert.Equal(2, cache.Get("b"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Eviction_RemovesLeastRecentlyUsed()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);
        cache.Put("c", 3);

        // Cache full: [c, b, a]. Adding "d" should evict "a" (LRU)
        cache.Put("d", 4);

        Assert.Equal(3, cache.Count);
        Assert.Equal(0, cache.Get("a")); // evicted
        Assert.Equal(2, cache.Get("b"));
        Assert.Equal(3, cache.Get("c"));
        Assert.Equal(4, cache.Get("d"));
    }

    [Fact]
    public void Get_MoveEntryToMRU_PreventingEviction()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);
        cache.Put("c", 3);

        // Access "a" — moves it to MRU. Now LRU is "b".
        cache.Get("a");

        cache.Put("d", 4); // should evict "b"

        Assert.Equal(3, cache.Count);
        Assert.Equal(1, cache.Get("a")); // still present
        Assert.Equal(0, cache.Get("b")); // evicted
        Assert.Equal(3, cache.Get("c"));
        Assert.Equal(4, cache.Get("d"));
    }

    [Fact]
    public void Put_ExistingKey_UpdatesValue_And_MovesToMRU()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);
        cache.Put("c", 3);

        // Update "a" — moves it to MRU. Now LRU is "b".
        cache.Put("a", 100);

        Assert.Equal(100, cache.Get("a"));

        cache.Put("d", 4); // should evict "b"
        Assert.Equal(0, cache.Get("b")); // evicted
    }

    [Fact]
    public void Capacity1_EvictsOnEveryNewKey()
    {
        var cache = new LRUCache<string, int>(1);
        cache.Put("a", 1);
        Assert.Equal(1, cache.Get("a"));

        cache.Put("b", 2);
        Assert.Equal(1, cache.Count);
        Assert.Equal(0, cache.Get("a")); // evicted
        Assert.Equal(2, cache.Get("b"));
    }

    [Fact]
    public void Remove_RemovesEntry()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);

        Assert.True(cache.Remove("a"));
        Assert.Equal(1, cache.Count);
        Assert.Equal(0, cache.Get("a"));
        Assert.False(cache.Remove("nonexistent"));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);
        cache.Put("c", 3);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Get("a"));
    }

    [Fact]
    public void InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LRUCache<string, int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LRUCache<string, int>(-1));
    }

    [Fact]
    public void StructuralTest_CountMatchesExpected_AfterManyOps()
    {
        var cache = new LRUCache<int, int>(5);

        // Add 10 items — cache should hold only the last 5
        for (int i = 0; i < 10; i++)
            cache.Put(i, i * 10);

        Assert.Equal(5, cache.Count);

        // Items 0–4 should be evicted, items 5–9 should be present
        for (int i = 0; i < 5; i++)
            Assert.Equal(0, cache.Get(i));

        for (int i = 5; i < 10; i++)
            Assert.Equal(i * 10, cache.Get(i));
    }

    [Fact]
    public void TryGet_ReturnsTrueAndValue_WhenPresent()
    {
        var cache = new LRUCache<string, string>(3);
        cache.Put("key", "value");

        Assert.True(cache.TryGet("key", out var val));
        Assert.Equal("value", val);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        var cache = new LRUCache<string, string>(3);

        Assert.False(cache.TryGet("missing", out var val));
        Assert.Null(val);
    }
}
