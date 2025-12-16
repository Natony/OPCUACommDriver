using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Interfaces;

/// <summary>
/// Interface cho Data Cache service
/// Cache giá trị tag để truy xuất nhanh
/// </summary>
public interface IDataCache
{
    /// <summary>
    /// Sự kiện khi giá trị thay đổi
    /// </summary>
    event EventHandler<TagValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Lấy giá trị cached của 1 tag
    /// </summary>
    TagValue? GetValue(string plcId, string tagId);

    /// <summary>
    /// Lấy tất cả giá trị cached của 1 PLC
    /// </summary>
    IReadOnlyDictionary<string, TagValue> GetPlcValues(string plcId);

    /// <summary>
    /// Lấy tất cả giá trị cached
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TagValue>> GetAllValues();

    /// <summary>
    /// Cập nhật giá trị tag (by TagId)
    /// </summary>
    void SetValue(string plcId, string tagId, TagValue value);

    /// <summary>
    /// Cập nhật giá trị tag (by NodeId) - dùng cho OPC UA
    /// </summary>
    void UpdateTag(string plcId, string nodeId, TagValue value);

    /// <summary>
    /// Xóa cache của 1 PLC
    /// </summary>
    void ClearPlc(string plcId);

    /// <summary>
    /// Xóa toàn bộ cache
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Kiểm tra tag có trong cache không
    /// </summary>
    bool HasValue(string plcId, string tagId);

    /// <summary>
    /// Lấy số lượng tag đang cached
    /// </summary>
    int Count { get; }
}
