using System.Collections.Concurrent;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Services;

/// <summary>
/// Service cache giá trị Tag để truy xuất nhanh
/// Thread-safe với ConcurrentDictionary
/// </summary>
public class DataCacheService : IDataCache
{
    // Cache structure: PlcId -> (TagId -> TagValue)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TagValue>> _cache = new();
    
    // Mapping: PlcId -> (NodeId -> TagId) để lookup nhanh
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _nodeIdMapping = new();

    public event EventHandler<TagValueChangedEventArgs>? ValueChanged;

    public int Count => _cache.Values.Sum(d => d.Count);

    /// <summary>
    /// Lấy giá trị cached của 1 tag
    /// </summary>
    public TagValue? GetValue(string plcId, string tagId)
    {
        if (_cache.TryGetValue(plcId, out var plcCache))
        {
            if (plcCache.TryGetValue(tagId, out var value))
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>
    /// Lấy tất cả giá trị cached của 1 PLC
    /// </summary>
    public IReadOnlyDictionary<string, TagValue> GetPlcValues(string plcId)
    {
        if (_cache.TryGetValue(plcId, out var plcCache))
        {
            return plcCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        return new Dictionary<string, TagValue>();
    }

    /// <summary>
    /// Lấy tất cả giá trị cached
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, TagValue>> GetAllValues()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, TagValue>>();
        foreach (var kvp in _cache)
        {
            result[kvp.Key] = kvp.Value.ToDictionary(v => v.Key, v => v.Value);
        }
        return result;
    }

    /// <summary>
    /// Cập nhật giá trị tag (by TagId)
    /// </summary>
    public void SetValue(string plcId, string tagId, TagValue value)
    {
        var plcCache = _cache.GetOrAdd(plcId, _ => new ConcurrentDictionary<string, TagValue>());
        
        TagValue? oldValue = null;
        if (plcCache.TryGetValue(tagId, out var existing))
        {
            oldValue = existing;
        }

        plcCache[tagId] = value;

        // Store nodeId mapping if available
        if (!string.IsNullOrEmpty(value.NodeId))
        {
            var nodeMapping = _nodeIdMapping.GetOrAdd(plcId, _ => new ConcurrentDictionary<string, string>());
            nodeMapping[value.NodeId] = tagId;
        }

        // Raise event
        ValueChanged?.Invoke(this, new TagValueChangedEventArgs
        {
            PlcId = plcId,
            TagId = tagId,
            NodeId = value.NodeId,
            Value = value
        });
    }

    /// <summary>
    /// Cập nhật giá trị tag by NodeId (dùng cho OPC UA subscriptions)
    /// </summary>
    public void UpdateTag(string plcId, string nodeId, TagValue value)
    {
        // Try to find tagId from nodeId mapping
        string? tagId = null;
        if (_nodeIdMapping.TryGetValue(plcId, out var nodeMapping))
        {
            nodeMapping.TryGetValue(nodeId, out tagId);
        }

        // Use nodeId as tagId if not found
        tagId ??= nodeId;
        
        value.NodeId = nodeId;
        value.TagId = tagId;
        
        SetValue(plcId, tagId, value);
    }

    /// <summary>
    /// Xóa cache của 1 PLC
    /// </summary>
    public void ClearPlc(string plcId)
    {
        _cache.TryRemove(plcId, out _);
        _nodeIdMapping.TryRemove(plcId, out _);
    }

    /// <summary>
    /// Xóa toàn bộ cache
    /// </summary>
    public void ClearAll()
    {
        _cache.Clear();
        _nodeIdMapping.Clear();
    }

    /// <summary>
    /// Kiểm tra tag có trong cache không
    /// </summary>
    public bool HasValue(string plcId, string tagId)
    {
        if (_cache.TryGetValue(plcId, out var plcCache))
        {
            return plcCache.ContainsKey(tagId);
        }
        return false;
    }

    /// <summary>
    /// Register tag với nodeId mapping
    /// </summary>
    public void RegisterTag(string plcId, string tagId, string nodeId)
    {
        var nodeMapping = _nodeIdMapping.GetOrAdd(plcId, _ => new ConcurrentDictionary<string, string>());
        nodeMapping[nodeId] = tagId;
    }

    /// <summary>
    /// Unregister tag
    /// </summary>
    public void UnregisterTag(string plcId, string tagId, string nodeId)
    {
        if (_nodeIdMapping.TryGetValue(plcId, out var nodeMapping))
        {
            nodeMapping.TryRemove(nodeId, out _);
        }
        
        if (_cache.TryGetValue(plcId, out var plcCache))
        {
            plcCache.TryRemove(tagId, out _);
        }
    }
}
