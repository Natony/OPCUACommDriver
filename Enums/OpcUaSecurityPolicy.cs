namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// OPC UA Security Policy cho kết nối
/// </summary>
public enum OpcUaSecurityPolicy
{
    /// <summary>
    /// Không bảo mật (chỉ dùng cho test)
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic128Rsa15 (deprecated, không khuyến khích)
    /// </summary>
    Basic128Rsa15 = 1,

    /// <summary>
    /// Basic256 (deprecated, không khuyến khích)
    /// </summary>
    Basic256 = 2,

    /// <summary>
    /// Basic256Sha256 (khuyến khích sử dụng)
    /// </summary>
    Basic256Sha256 = 3,

    /// <summary>
    /// Aes128_Sha256_RsaOaep
    /// </summary>
    Aes128Sha256RsaOaep = 4,

    /// <summary>
    /// Aes256_Sha256_RsaPss (bảo mật cao nhất)
    /// </summary>
    Aes256Sha256RsaPss = 5
}

/// <summary>
/// OPC UA Message Security Mode
/// </summary>
public enum OpcUaSecurityMode
{
    /// <summary>
    /// Không bảo mật
    /// </summary>
    None = 1,

    /// <summary>
    /// Chỉ ký (Sign only)
    /// </summary>
    Sign = 2,

    /// <summary>
    /// Ký và mã hóa (Sign and Encrypt)
    /// </summary>
    SignAndEncrypt = 3
}
