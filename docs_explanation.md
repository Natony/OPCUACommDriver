# OPC UA Communication Engine — Giải Thích Codebase Chi Tiết

**Dự án:** OpcUaCommunicationEngine (AVS / Junction brand)  
**Stack:** WPF .NET 8, C#, OPC UA SDK 1.5.374, Serilog, BCrypt.Net, Newtonsoft.Json, ASP.NET Core, Microsoft.Extensions.DependencyInjection  
**Mục đích:** Desktop app kết nối PLC qua OPC UA, duyệt node tree, quản lý tag/subscription, REST API, phân quyền user, operator locking.  
**Ngày tạo tài liệu:** 2026-06-19

---

## MỤC LỤC

1. Tổng quan kiến trúc
2. Thứ tự build lại từ đầu
3. Layer 1 — Enums
4. Layer 2 — Models
5. Layer 3 — Helpers (RelayCommand, Converters)
6. Layer 4 — Interfaces
7. Layer 5 — Services (Auth, Config, OpcUa, Protocols)
8. Layer 6 — ViewModels
9. Layer 7 — App startup (App.xaml.cs)
10. Layer 8 — Views (XAML + code-behind)
11. Các bug đã fix trong dự án
12. Patterns và quyết định kiến trúc

---

## 1. TỔNG QUAN KIẾN TRÚC

```
┌─────────────────────────────────────────────────────────────┐
│                      VIEWS (XAML + cs)                       │
│  MainWindow  LoginWindow  BrowseServerWindow  Dialogs...     │
└────────────────────────┬────────────────────────────────────┘
                         │ DataContext Binding / Commands
┌────────────────────────▼────────────────────────────────────┐
│                     VIEWMODELS                               │
│         MainViewModel        BrowseServerViewModel           │
└────────────────────────┬────────────────────────────────────┘
                         │ Interface injection (DI)
┌────────────────────────▼────────────────────────────────────┐
│                      SERVICES                                │
│  PlcManager  PlcConnection  UserService  ConfigService       │
│  ApiHostService  OperatorLockService  UiLogSink              │
│  ProtocolConnectionFactory  (S7, Mitsubishi, Modbus)         │
└────────────────────────┬────────────────────────────────────┘
                         │ Depends on
┌────────────────────────▼────────────────────────────────────┐
│           MODELS / ENUMS / HELPERS / INTERFACES              │
│  PlcDevice  TagItem  User  BrowseNode  RelayCommand...       │
└─────────────────────────────────────────────────────────────┘
```

**Tại sao dùng MVVM?**
WPF được thiết kế tối ưu cho MVVM. XAML binding trực tiếp vào ViewModel property — khi property thay đổi, UI tự cập nhật mà không cần code-behind. Điều này giúp tách biệt logic (ViewModel) khỏi giao diện (View), dễ test và maintain.

**Tại sao dùng Dependency Injection?**
Khi class A cần class B, thay vì `new B()` bên trong A (tight coupling), ta inject B qua constructor. Lợi ích: (1) dễ thay thế implementation — đổi `PlcManager` thành mock khi test, (2) vòng đời được quản lý tập trung trong DI container, (3) tránh singleton tự quản lý (anti-pattern).

---

## 2. THỨ TỰ BUILD LẠI TỪ ĐẦU

Khi rebuild từ đầu, luôn build theo thứ tự phụ thuộc — file nào không phụ thuộc gì thì build trước:

```
Bước 1:  Enums/                     ← không phụ thuộc gì
Bước 2:  Models/ (ObservableObject) ← chỉ cần System + WPF
Bước 3:  Models/ (các model khác)   ← phụ thuộc Enums + ObservableObject
Bước 4:  Interfaces/                ← phụ thuộc Models
Bước 5:  Helpers/                   ← phụ thuộc System.Windows.Input
Bước 6:  Services/Auth/UserService  ← BCrypt.Net + Models
Bước 7:  Services/ConfigService     ← Newtonsoft.Json + Models
Bước 8:  Services/UiLogSink         ← Serilog + Models
Bước 9:  Services/OpcUa/PlcConn    ← OPC UA SDK + tất cả trên
Bước 10: Services/OpcUa/PlcManager ← PlcConnection + Interfaces
Bước 11: Services/Protocols/        ← Protocol libs (S7Net, EasyModbus)
Bước 12: Api/                       ← ASP.NET Core + PlcManager
Bước 13: ViewModels/                ← Services + Helpers
Bước 14: App.xaml + App.xaml.cs    ← DI wiring + startup
Bước 15: Views/ (Dialogs)          ← ViewModels + XAML
Bước 16: Views/ (MainWindow)       ← Tất cả trên
```

Quy tắc: **không bao giờ** để tầng dưới import tầng trên. Models không import ViewModel. Services không import Views.

---

## 3. LAYER 1 — ENUMS

Enums là những kiểu dữ liệu liệt kê — không có logic, không có dependencies. Nên viết trước tiên.

### UserRole

```csharp
public enum UserRole
{
    Viewer   = 0,    // chỉ xem, không ghi
    Operator = 1,    // có thể ghi tag (cần có operator lock)
    Admin    = 2     // toàn quyền, bao gồm quản lý user
}
```

**Tại sao có giá trị số?** Để so sánh phân quyền: `if (role >= UserRole.Operator)`. Không cần switch/if phức tạp. Giá trị 0/1/2 lưu vào JSON và database tốt hơn string.

### PlcConnectionState

```csharp
public enum PlcConnectionState
{
    Disabled,       // PLC bị vô hiệu hóa trong config (IsEnabled=false)
    Connecting,     // đang thực hiện kết nối
    Connected,      // kết nối thành công, session active
    Disconnecting,  // đang ngắt kết nối (có thể mất thời gian)
    Disconnected,   // đã ngắt kết nối sạch sẽ
    Reconnecting,   // mất kết nối, đang thử lại
    Error           // lỗi không phục hồi được
}
```

**Tại sao cần nhiều state?** Để UI hiển thị đúng: nút "Kết nối" disabled khi Connecting/Reconnecting. Màu icon khác nhau cho từng state. `Disconnecting` riêng biệt với `Disconnected` vì có thể mất 1-5 giây để close session sạch.

### OpcUaSecurityPolicy

```csharp
public enum OpcUaSecurityPolicy
{
    None,                   // không mã hóa — dùng cho lab/test
    Basic128Rsa15,          // mã hóa AES-128 — cũ, ít dùng
    Basic256,               // mã hóa AES-256
    Basic256Sha256,         // mã hóa AES-256 + SHA-256 — phổ biến nhất
    Aes128Sha256RsaOaep,    // mới hơn, nhanh hơn
    Aes256Sha256RsaPss      // mạnh nhất
}
```

**Lưu ý thực tế:** Siemens S7-1200 mất 6-14 giây để renew Secure Channel với `Basic256Sha256`. Điều này gây timeout và reconnect không cần thiết. Khuyến nghị dùng `None` cho môi trường nội bộ.

### TagQuality

```csharp
public enum TagQuality { Unknown, Good, Bad, Uncertain }
```

Map trực tiếp từ OPC UA `StatusCode`. `Unknown` dùng cho lúc chưa đọc lần nào. Hiển thị màu khác nhau trên UI.

---

## 4. LAYER 2 — MODELS

### Models/ObservableObject.cs — Nền tảng WPF Binding

```csharp
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
```

`INotifyPropertyChanged` là interface của WPF/XAML. Khi một property thay đổi, WPF cần được thông báo để re-render binding tương ứng. Nếu không implement interface này, binding sẽ chỉ đọc một lần lúc khởi tạo và không bao giờ cập nhật.

```csharp
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;
        if (handler == null) return;
```

`[CallerMemberName]` là C# attribute đặc biệt: khi gọi `OnPropertyChanged()` bên trong property setter mà không truyền tên, compiler tự điền tên property. Ví dụ: gọi trong `set` của property `Name` → `propertyName = "Name"`. Điều này tránh lỗi typo khi viết tên string thủ công.

Lưu `handler` vào local variable trước — pattern thread-safety: tránh race condition khi subscriber unsubscribe đúng lúc ta đang invoke.

```csharp
        if (Application.Current?.Dispatcher != null &&
            !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
        else
        {
            handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
```

**Đây là điểm quan trọng nhất của ObservableObject.** WPF chỉ cho phép cập nhật UI từ UI thread (main thread). Nhưng OPC UA subscription callback chạy trên background thread — khi giá trị tag thay đổi, nó gọi `tag.UpdateValue()` → `OnPropertyChanged()` từ background thread.

`Dispatcher.CheckAccess()` trả về `true` nếu đang trên UI thread. Nếu `false` (background thread) → `BeginInvoke` để post action vào UI thread queue, async không chờ. Nếu đang trên UI thread → invoke trực tiếp.

```csharp
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;    // không thay đổi → không notify
        
        field = value;
        OnPropertyChanged(propertyName);
        return true;         // đã thay đổi
    }
}
```

`SetProperty<T>` là shorthand pattern: kiểm tra thay đổi + set + notify trong một dòng. Tất cả property setter trong Models và ViewModels dùng pattern này:

```csharp
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);  // một dòng thay vì 5 dòng
}
```

`EqualityComparer<T>.Default.Equals()` dùng generic equality — đúng cho cả value type (int, bool) lẫn reference type (string, object). Với string, `"abc".Equals("abc")` trả `true` dù là 2 instance khác nhau.

---

### Models/PlcDevice.cs — Model đại diện cho một PLC

`PlcDevice` kế thừa `ObservableObject` vì nó cần binding trực tiếp lên UI (ListBox, DataGrid). Mỗi PLC hiển thị state, connection status, tags — tất cả đều live-update.

**Tại sao dùng private backing field thay vì auto-property?**

```csharp
// Auto-property — KHÔNG dùng cho binding
public string Name { get; set; }

// Backing field + SetProperty — dùng cho binding
private string _name = string.Empty;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

Auto-property không có cách gọi `OnPropertyChanged` — không thể binding two-way. Backing field + `SetProperty` mới có thể notify WPF.

**[JsonIgnore] trên runtime state:**

```csharp
[JsonIgnore]
public PlcConnectionState ConnectionState
{
    get => _connectionState;
    set => SetProperty(ref _connectionState, value);
}
```

`[JsonIgnore]` của Newtonsoft.Json — khi serialize `PlcDevice` ra file JSON (save config), ta không muốn lưu `ConnectionState` vì đó là trạng thái runtime, không phải config. Mỗi lần mở app trạng thái reset về Disconnected.

**Computed properties với [JsonIgnore]:**

```csharp
[JsonIgnore]
public string ConnectionStateText => ConnectionState switch
{
    PlcConnectionState.Connected => "Connected",
    PlcConnectionState.Error => $"Error: {LastError}",
    PlcConnectionState.Reconnecting => "Reconnecting...",
    _ => ConnectionState.ToString()
};
```

Property computed (không có setter, tính từ state khác). Khi `ConnectionState` thay đổi, phải manually raise `OnPropertyChanged(nameof(ConnectionStateText))` nếu muốn binding cập nhật. Thường làm trong `ConnectionState` setter:

```csharp
set
{
    if (SetProperty(ref _connectionState, value))
    {
        OnPropertyChanged(nameof(ConnectionStateText));
        OnPropertyChanged(nameof(IsConnected));
    }
}
```

**Factory methods:**

```csharp
public static PlcDevice Create(string name, string endpointUrl)
{
    return new PlcDevice
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        EndpointUrl = endpointUrl,
        ProtocolType = ProtocolType.OpcUa,
        ConnectionState = PlcConnectionState.Disabled
    };
}
```

Static factory thay vì constructor có nhiều tham số — dễ đọc hơn, tên method nói lên ngữ cảnh (`CreateSiemensS7` vs `CreateMitsubishiMc`). `Guid.NewGuid().ToString()` tạo ID unique dạng "550e8400-e29b-41d4-a716-446655440000".

**Clone() method:**

```csharp
public PlcDevice Clone()
{
    return new PlcDevice
    {
        Id = this.Id,
        Name = this.Name,
        // copy tất cả config fields...
        // KHÔNG copy runtime state (ConnectionState, LastError, ...)
    };
}
```

Dùng trong EditPlcDialog: lấy bản copy, user chỉnh sửa copy, nếu cancel thì không ảnh hưởng gốc, nếu save thì merge lại. Tránh mutate trực tiếp trong lúc user đang edit.

---

### Models/TagItem.cs — Model đại diện cho một OPC UA Tag

Tương tự PlcDevice, TagItem cũng kế thừa ObservableObject vì nó binding vào DataGrid — giá trị, quality, timestamp live-update khi subscription nhận data mới.

**UpdateValue() — cập nhật tất cả fields cùng lúc:**

```csharp
public void UpdateValue(object? newValue, TagQuality quality, DateTime timestamp, DateTime? serverTimestamp = null)
{
    PreviousValue = Value;      // lưu giá trị cũ để so sánh
    Value = newValue;           // set mới → triggers DisplayValue, HasValueChanged
    Quality = quality;          // Good/Bad/Uncertain
    Timestamp = timestamp;      // client-side timestamp
    ServerTimestamp = serverTimestamp;
    LastError = string.Empty;   // xóa lỗi cũ khi có data mới
}
```

`PreviousValue = Value` phải gán trước `Value = newValue` — nếu đổi thứ tự, PreviousValue sẽ bằng Value mới.

**HasValueChanged:**

```csharp
[JsonIgnore]
public bool HasValueChanged => !Equals(Value, PreviousValue);
```

Dùng `Equals()` thay vì `==` vì Value là `object?` — với reference type, `==` so sánh reference, không so sánh nội dung. `Equals()` gọi virtual method, cho phép type cụ thể (int, double, string) override với so sánh giá trị.

---

### Models/User.cs — Model tài khoản người dùng

```csharp
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public int? LockDurationMinutes { get; set; }
}
```

**User KHÔNG kế thừa ObservableObject** — không cần binding trực tiếp. UserManagementWindow dùng DataGrid với `ItemsSource` refresh toàn bộ danh sách khi có thay đổi.

`PasswordHash` — **KHÔNG BAO GIỜ** lưu password plaintext. BCrypt hash là chuỗi 60 ký tự dạng `$2a$11$...`. Ngay cả khi file JSON bị lộ, attacker không thể reverse-engineer password.

`LockDurationMinutes` là `int?` (nullable) — null nghĩa là dùng default từ `AuthSettings`. 0 nghĩa là không giới hạn thời gian (unlimited lock).

`UserStorage` là wrapper class để serialize/deserialize toàn bộ danh sách users:

```csharp
public class UserStorage
{
    public List<User> Users { get; set; } = new();
}
```

Tại sao cần wrapper thay vì serialize trực tiếp `List<User>`? Để dễ mở rộng sau — có thể thêm metadata (version, createdAt, checksum) vào file mà không phá vỡ format cũ.

---

## 5. LAYER 3 — HELPERS

### Helpers/RelayCommand.cs — Cầu nối ViewModel → View

**Vấn đề MVVM cần giải quyết:** XAML Button cần `Command` (ICommand), không phải `Click` event. ViewModel có method `void SaveConfig()` — cần bọc nó thành `ICommand`.

**RelayCommand (synchronous):**

```csharp
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
```

`CommandManager.RequerySuggested` là WPF mechanism tự động re-evaluate `CanExecute` khi UI state thay đổi (focus change, input change). Bằng cách forward `CanExecuteChanged` sang `RequerySuggested`, mọi lần WPF hỏi "button này có enable không?" đều gọi `CanExecute()` của ta.

```csharp
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }
```

Overload thứ hai nhận `Action` (không có parameter) và `Func<bool>` (không có parameter) — dễ dùng hơn khi không cần CommandParameter:

```csharp
SaveCommand = new RelayCommand(Save, () => HasUnsavedChanges);
// thay vì
SaveCommand = new RelayCommand(_ => Save(), _ => HasUnsavedChanges);
```

**AsyncRelayCommand (asynchronous):**

```csharp
public class AsyncRelayCommand : ICommand
{
    private bool _isExecuting;

    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute == null || _canExecute());
```

`!_isExecuting` ngăn user click nhiều lần khi command đang chạy. Khi `_isExecuting = true`, `CanExecute()` trả về false → button tự disabled.

```csharp
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;    // disables button
            await _execute();      // chạy async task
        }
        finally
        {
            IsExecuting = false;   // re-enables button dù có exception
        }
    }
```

`async void` trên ICommand.Execute là **hợp lệ** vì ICommand.Execute có signature `void Execute(object)`. Ta không thể return Task từ đây. Exception trong `async void` sẽ propagate lên `AppDomain.UnhandledException` — đó là lý do cần global exception handler.

`finally` đảm bảo button luôn được re-enable dù task throw exception — không để UI bị kẹt.

---

## 6. LAYER 4 — INTERFACES

Interfaces định nghĩa **contract** (hợp đồng) giữa các layer, không có implementation. Cho phép:
- Test với mock objects
- Thay đổi implementation mà không ảnh hưởng caller
- DI container inject đúng implementation

### IPlcConnection — hợp đồng cho 1 kết nối PLC

```csharp
public interface IPlcConnection : IDisposable
{
    PlcDevice Device { get; }
    PlcConnectionState ConnectionState { get; }
    bool IsConnected { get; }
    string? SessionId { get; }

    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<bool> ReconnectAsync(CancellationToken cancellationToken = default);
    Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default);
    Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default);
}
```

`IDisposable` — kết nối cần cleanup (đóng session, hủy subscription). Khi `PlcManager.Dispose()`, nó gọi `connection.Dispose()` cho tất cả connections.

`IReadOnlyList<T>` thay vì `List<T>` — caller không thể modify list trả về (immutable interface). Tránh side effect không mong muốn.

### IPlcManager — quản lý nhiều PLCs

```csharp
public interface IPlcManager : IDisposable
{
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<TagValueChangedEventArgs>? TagValueChanged;

    Task<IPlcConnection?> AddPlcAsync(PlcDevice device, ...);
    Task<bool> RemovePlcAsync(string plcId, ...);
    IPlcConnection? GetConnection(string plcId);
    Task<bool> ConnectAsync(string plcId, ...);
    Task DisconnectAsync(string plcId, ...);
    Task<int> ConnectAllAsync(...);
    Task<IReadOnlyList<BrowseNode>> BrowseAsync(string plcId, string? nodeId = null, ...);
    PlcStatus? GetStatus(string plcId);
}
```

MainViewModel inject `IPlcManager` không phải `PlcManager` cụ thể. Trong unit test, có thể inject `MockPlcManager` implement interface này.

---

## 7. LAYER 5 — SERVICES

### Services/UiLogSink.cs — Đưa log lên UI

**Vấn đề:** Serilog mặc định ghi log ra file và console. Ta muốn log xuất hiện trực tiếp trong cửa sổ app (log panel).

**Giải pháp:** Viết custom Serilog sink implement `ILogEventSink`.

```csharp
public class UiLogSink : ILogEventSink
{
    private readonly ObservableCollection<LogEntry> _logEntries;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly int _maxEntries;

    public UiLogSink(ObservableCollection<LogEntry> logEntries, int maxEntries = 1000)
    {
        _logEntries = logEntries;
        _dispatcher = System.Windows.Application.Current.Dispatcher;  // capture UI dispatcher
        _maxEntries = maxEntries;
    }
```

`Dispatcher` được capture lúc constructor — constructor chạy trên UI thread, nên `Application.Current.Dispatcher` là đúng. Nếu capture sau này (khi Emit được gọi từ background thread), `Application.Current` có thể null.

```csharp
    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp.LocalDateTime,  // convert từ DateTimeOffset
            Level = GetLevelShortName(logEvent.Level),     // "INF", "WRN", "ERR", ...
            Message = logEvent.RenderMessage(),             // format message với parameters
            Exception = logEvent.Exception?.ToString()     // full stack trace nếu có
        };

        _dispatcher.InvokeAsync(() =>
        {
            _logEntries.Add(entry);

            while (_logEntries.Count > _maxEntries)
                _logEntries.RemoveAt(0);   // xóa entry cũ nhất, giữ max 500 entries
        });
    }
```

`logEvent.RenderMessage()` format message template với values. Ví dụ: `"Connected to {PlcName}"` với `PlcName="PLC1"` → `"Connected to PLC1"`.

`while (_logEntries.Count > _maxEntries)` loop thay vì `if` — phòng trường hợp burst nhiều logs cùng lúc.

```csharp
public static class UiLogSinkExtensions
{
    public static LoggerConfiguration UiSink(
        this LoggerSinkConfiguration sinkConfig,
        ObservableCollection<LogEntry> logEntries,
        int maxEntries = 1000)
        => sinkConfig.Sink(new UiLogSink(logEntries, maxEntries));
}
```

Extension method cho phép dùng fluent API của Serilog:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("log.txt")
    .WriteTo.UiSink(_mainViewModel.LogEntries, maxEntries: 500)  // extension method
    .CreateLogger();
```

---

### Services/Auth/UserService.cs — Quản lý users

```csharp
public class UserService
{
    private readonly string _usersFilePath;
    private readonly object _lock = new();    // thread-safety lock
    private UserStorage _storage;
    private static ILogger Logger => Log.Logger;  // dynamic, not captured
```

`private readonly object _lock = new()` — object lock đơn giản, không dùng static (tránh deadlock với singleton). Tất cả public methods đều wrap trong `lock(_lock)` vì:
- UI thread gọi khi admin thêm/sửa user
- Background API thread gọi khi xác thực login API

```csharp
    public UserService(AuthSettings settings)
    {
        _usersFilePath = settings.UsersFilePath;
        _storage = LoadOrCreateStorage();  // load ngay trong constructor
    }
```

Load users ngay trong constructor — không lazy-load. DI container tạo UserService là Singleton, nên chỉ load 1 lần lúc startup.

**LoadOrCreateStorage() — self-healing:**

```csharp
    private UserStorage LoadOrCreateStorage()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = File.ReadAllText(_usersFilePath);
                var storage = JsonConvert.DeserializeObject<UserStorage>(json);
                if (storage != null && storage.Users.Any())
                {
                    Logger.Information("Loaded {Count} users from storage", storage.Users.Count);
                    return storage;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading users storage, creating default");
        }

        // Tự tạo admin mặc định nếu file không có hoặc corrupt
        var defaultStorage = CreateDefaultStorage();
        SaveStorageInternal(defaultStorage);
        return defaultStorage;
    }
```

**Tại sao cần self-healing?** Lần đầu deploy app, file users.json chưa có. Thay vì crash, app tự tạo `admin/admin123`. Tương tự khi file bị corrupt (disk lỗi) — app phục hồi thay vì không cho ai login được.

**HashPassword / VerifyPassword:**

```csharp
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;   // hash corrupt → false, không crash
        }
    }
```

`workFactor: 11` — BCrypt tính toán 2^11 = 2048 vòng hash. Khoảng 0.15-0.3 giây mỗi lần hash — đủ để brute force 1 tỉ password mất hàng nghìn năm, nhưng không làm người dùng cảm thấy chậm khi login.

`workFactor: 12` (= 4096 vòng) sẽ mất 0.3-0.6 giây — dùng khi security quan trọng hơn UX.

**ValidateCredentials — timing attack prevention:**

```csharp
    public User? ValidateCredentials(string username, string password)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.IsActive);

            if (user == null) return null;         // user không tồn tại

            if (VerifyPassword(password, user.PasswordHash))
            {
                user.LastLoginAt = DateTime.UtcNow;
                SaveStorage();
                return user;
            }

            return null;   // sai password — cùng trả null như user không tồn tại
        }
    }
```

Cả "user không tồn tại" và "sai password" đều trả về `null` — không lộ thông tin cho attacker biết cái nào sai. (Nếu phân biệt, attacker có thể enumerate username hợp lệ.)

`StringComparison.OrdinalIgnoreCase` — "Admin" == "admin" == "ADMIN". Username case-insensitive.

**DeleteUser — bảo vệ last admin:**

```csharp
    public bool DeleteUser(string userId)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            if (user.Role == UserRole.Admin)
            {
                var adminCount = _storage.Users.Count(u => u.Role == UserRole.Admin && u.IsActive);
                if (adminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last admin user");
                }
            }

            _storage.Users.Remove(user);
            SaveStorage();
            return true;
        }
    }
```

Ném exception thay vì return false — caller (EditUserDialog) phải xử lý exception này để hiển thị message cụ thể cho user. Return false chỉ nên dùng khi "not found" — đó là expected case, không phải error.

---

### Services/ConfigurationService.cs — Đọc/ghi cấu hình

```csharp
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger _logger;
    private readonly string _defaultConfigPath;
    private AppConfiguration _currentConfiguration;
    private string? _currentFilePath;
    private bool _hasUnsavedChanges;

    public event EventHandler<AppConfiguration>? ConfigurationChanged;
```

`ConfigurationChanged` event — khi load file mới, MainViewModel cần biết để refresh UI. Event pattern tốt hơn polling.

```csharp
    public ConfigurationService(ILogger logger, string? defaultConfigPath = null)
    {
        _defaultConfigPath = defaultConfigPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Configurations",
            "plc_config.json");

        _currentConfiguration = AppConfiguration.CreateDefault();
    }
```

`AppDomain.CurrentDomain.BaseDirectory` — thư mục chứa file .exe. Tốt hơn `Environment.CurrentDirectory` vì CurrentDirectory có thể thay đổi nếu user chạy từ shortcut hoặc command line ở thư mục khác.

Không load config trong constructor — để cho `InitializeAsync()` trong MainViewModel gọi sau khi DI wired xong.

**GetJsonSettings():**

```csharp
    private static JsonSerializerSettings GetJsonSettings()
    {
        return new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,          // JSON đọc được bằng mắt
            NullValueHandling = NullValueHandling.Ignore,  // không ghi null fields
            DefaultValueHandling = DefaultValueHandling.Include,  // ghi default values
            DateFormatString = "yyyy-MM-dd HH:mm:ss"  // format dễ đọc, không ISO 8601
        };
    }
```

`NullValueHandling.Ignore` — khi serialize, bỏ qua properties null → file JSON gọn hơn. Ví dụ: `"CertificatePath": null` không xuất hiện trong file nếu không dùng.

`Formatting.Indented` — file JSON có indent, dễ đọc và diff trong git. Trade-off: file to hơn nhưng chấp nhận được vì config file không đến vài MB.

---

### Services/OpcUa/PlcConnection.cs — Trái tim OPC UA

**Tại sao `private static ILogger Logger => Log.Logger` (property) thay vì `private readonly ILogger _logger` (field)?**

```csharp
// Sai — captures logger tại thời điểm construction
private readonly ILogger _logger;
public PlcConnection(PlcDevice device, ILogger logger) { _logger = logger; }
```

DI container tạo PlcConnection trước khi UiSink được add vào Serilog. Nếu capture logger qua field, tất cả logs từ PlcConnection sẽ không xuất hiện trên UI (UiSink chưa được add).

```csharp
// Đúng — lấy logger mới nhất mỗi lần gọi
private static ILogger Logger => Log.Logger;
```

`Log.Logger` là static property của Serilog — luôn trả về instance hiện tại. Sau khi App.xaml.cs tạo lại logger với UiSink, mọi `Logger.Information(...)` từ PlcConnection sẽ tự động đến UI.

**Fields phức tạp — giải thích từng cái:**

```csharp
private readonly object _lock = new();
```
Object lock cho các thao tác cần atomic trên `_reconnectHandler` và `_isReconnecting`. `lock(obj)` đảm bảo chỉ một thread chạy trong block tại một thời điểm.

```csharp
private bool _isDisconnecting;
```
Flag ngăn KeepAlive event trigger reconnect trong khi đang disconnect. Nếu không có flag này: `DisconnectAsync()` đang chạy → KeepAlive fires vì session đang đóng → trigger reconnect → reconnect thất bại → vòng lặp vô tận.

```csharp
private bool _isReconnecting;
```
Flag ngăn nhiều reconnect loops chạy song song. Không có flag: lần kết nối thất bại 1 → start AutoReconnect loop 1. Loop 1 thất bại lần đầu → start AutoReconnect loop 2. Tiếp tục nhân đôi → resource leak.

```csharp
private SessionReconnectHandler? _reconnectHandler;
```
Class của OPC UA SDK. Xử lý 2-layer reconnection: (1) Secure Channel (TLS) renewal; (2) Session resumption. Phức tạp hơn đơn giản là `ConnectAsync()` lại vì phải giữ subscription state.

```csharp
private DateTime _lastReconnectCompleteTime = DateTime.MinValue;
private const int KeepAliveSettlingPeriodMs = 2000;
```
Sau khi reconnect xong, OPC UA session có thể gửi vài KeepAlive "stale" từ kết nối cũ. Nếu không ignore, chúng kích hoạt reconnect lần 2 ngay lập tức — race condition. Settling period 2 giây bỏ qua các KeepAlive này.

```csharp
private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
private readonly ConcurrentDictionary<uint, (string TagId, string NodeId)> _monitoredItemMapping = new();
```
`ConcurrentDictionary` vì subscriptions được tạo/xóa từ cả UI thread (khi user add/remove tag) lẫn reconnect thread.

`_monitoredItemMapping` map `ClientHandle` (uint, OPC UA internal ID) → `(TagId, NodeId)`. Khi MonitoredItem_Notification callback fires, ta lookup tag cần update.

**ConnectAsync() — chuỗi kết nối:**

```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
{
    if (_disposed) throw new ObjectDisposedException(nameof(PlcConnection));
    if (IsConnected) { Logger.Warning("Already connected"); return true; }

    try
    {
        ConnectionState = PlcConnectionState.Connecting;
        // Bước 1: tạo OPC UA application config
        _appConfig = await CreateApplicationConfigurationAsync();
        // Bước 2: discover endpoints và chọn tốt nhất
        var selectedEndpoint = await SelectEndpointAsync(_device.EndpointUrl, cancellationToken);
        // Bước 3: tạo session
        _session = await Session.Create(_appConfig, endpoint, false, appName, sessionTimeout, userIdentity, null, ct);
        // Bước 4: đăng ký events
        _session.KeepAlive += Session_KeepAlive;
        _session.Notification += Session_Notification;
        _session.PublishError += Session_PublishError;
        // Bước 5: update state
        ConnectionState = PlcConnectionState.Connected;
        LastConnectedTime = DateTime.Now;
        LastError = null;
        // Bước 6: tạo subscriptions
        foreach (var group in _device.SubscriptionGroups.Where(g => g.IsEnabled))
            await CreateSubscriptionAsync(group, cancellationToken);
        return true;
    }
    catch (Exception ex)
    {
        LastError = ex.Message;
        ConnectionState = PlcConnectionState.Error;
        // Log chi tiết OPC UA status code nếu có
        if (ex is ServiceResultException sre)
            Logger.Error("OPC UA StatusCode: 0x{Code:X8}", sre.StatusCode);
        // Bắt đầu auto-reconnect nếu được cấu hình
        if (_device.AutoReconnect && !_isReconnecting && !_isDisconnecting)
            StartAutoReconnect();
        return false;
    }
}
```

`ServiceResultException` là exception type của OPC UA SDK — chứa `StatusCode` dạng hex (0x80350000 = BadNotConnected, ...). Log hex code giúp debug chính xác.

**CreateApplicationConfigurationAsync() — certificate và timeouts:**

```csharp
TransportQuotas = new TransportQuotas
{
    OperationTimeout = 60000,        // 60 giây — vì S7-1200 mất 6-14s renew Secure Channel
    SecurityTokenLifetime = 3600000  // 1 giờ — tần suất renew Secure Channel
},
```

S7-1200 với `Basic256Sha256` mất 6-14 giây để renew Secure Channel. Nếu `OperationTimeout = 15000ms` (default), timeout xảy ra trong lúc renew → KeepAlive fail → reconnect → lại renew → vòng lặp. Tăng lên 60s giải quyết vấn đề này.

```csharp
config.CertificateValidator = new CertificateValidator();
config.CertificateValidator.CertificateValidation += (validator, e) =>
{
    e.Accept = true;  // chấp nhận mọi certificate — chỉ dùng cho lab/internal
};
```

Trong môi trường factory/nội bộ, PLC thường dùng self-signed certificate. Nếu validate strict, kết nối sẽ bị từ chối. `e.Accept = true` bỏ qua kiểm tra certificate — **KHÔNG dùng trong môi trường internet-facing**.

**SelectBestEndpoint() — ưu tiên No Security:**

```csharp
private EndpointDescription SelectBestEndpoint(EndpointDescriptionCollection endpoints)
{
    // Priority 1: None security (khuyến nghị cho S7-1200 stability)
    if (!useSecurity)
    {
        var noneEndpoint = endpoints
            .Where(e => e.SecurityMode == MessageSecurityMode.None)
            .OrderBy(e => e.SecurityLevel)
            .FirstOrDefault();
        if (noneEndpoint != null) return noneEndpoint;
    }
    // Priority 2: Exact match
    // Priority 3: Policy match
    // Priority 4: Lighter security (Basic128 hoặc Basic256 không Sha256)
    // Priority 5: Any secure endpoint
    // Last: first available
}
```

Logic này đặc thù cho Siemens S7-1200/1500. PLC expose nhiều endpoints với security khác nhau. Thứ tự ưu tiên được tối ưu cho stability hơn security — phù hợp factory environment.

**Session_KeepAlive() — xử lý mất kết nối:**

```csharp
private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
{
    if (_isDisconnecting || _disposed) return;  // ignore khi đang shutdown

    if (e.Status != null && ServiceResult.IsNotGood(e.Status))
    {
        if (_reconnectHandler != null) {
            Logger.Debug("KeepAlive during reconnection");  // handler đã chạy rồi
            return;
        }

        // Kiểm tra settling period — bỏ qua KeepAlive stale sau khi reconnect
        var timeSince = (DateTime.Now - _lastReconnectCompleteTime).TotalMilliseconds;
        if (timeSince < KeepAliveSettlingPeriodMs) {
            Logger.Debug("Ignoring KeepAlive during settling period");
            return;
        }

        if (ConnectionState == PlcConnectionState.Connected && _device.AutoReconnect && !_isReconnecting)
        {
            ConnectionState = PlcConnectionState.Reconnecting;

            lock (_lock)
            {
                if (_reconnectHandler == null)  // double-check trong lock
                {
                    _isReconnecting = true;
                    _reconnectHandler = new SessionReconnectHandler(true);
                    _reconnectHandler.BeginReconnect(_session, 10000, SessionReconnectHandler_Complete);
                }
            }
        }
    }
    else
    {
        // KeepAlive thành công — session sống
        if (ConnectionState == PlcConnectionState.Reconnecting)
        {
            // Session tự recover trong lúc handler đang chạy
            lock (_lock)
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;
                _isReconnecting = false;
                _lastReconnectCompleteTime = DateTime.Now;
            }
            ConnectionState = PlcConnectionState.Connected;
        }
    }
}
```

`lock(_lock)` xung quanh `_reconnectHandler = new SessionReconnectHandler()` — double-check locking pattern. KeepAlive có thể fire từ nhiều thread (OPC UA SDK có internal thread pool). Mà không có lock, 2 threads có thể cùng check `_reconnectHandler == null` trước khi một trong chúng set nó → 2 handlers chạy song song.

**BrowseAsync() — lỗi NodeId quan trọng:**

```csharp
var startNode = string.IsNullOrEmpty(nodeId)
    ? ObjectIds.ObjectsFolder        // = NodeId với identifier=85
    : NodeId.Parse(nodeId);          // ĐÚNG: Parse "i=85" → Numeric NodeId
                                     // SAI: new NodeId("i=85") → String NodeId
```

`NodeId.Parse("i=85")` → `NodeId` với `NamespaceIndex=0`, `IdentifierType=Numeric`, `Identifier=85`  
`new NodeId("i=85")` → `NodeId` với `NamespaceIndex=0`, `IdentifierType=String`, `Identifier="i=85"`

OPC UA server trả về lỗi `BadNodeIdUnknown` khi dùng String NodeId cho node numeric. Bug này không crash — chỉ trả về empty list — gây ra "browse không ra gì" mà khó debug.

---

### Services/OpcUa/PlcManager.cs — Quản lý nhiều connections

```csharp
public class PlcManager : IPlcManager
{
    private readonly ConcurrentDictionary<string, IPlcConnection> _connections = new();
```

`ConcurrentDictionary` vì:
- UI thread: Add/Remove khi user thêm/xóa PLC
- Background threads: PlcConnection sự kiện ConnectionStateChanged access dictionary
- Auto-reconnect: các connection objects được update từ thread riêng

**Factory pattern qua IProtocolConnectionFactory:**

```csharp
    public async Task<IPlcConnection?> AddPlcAsync(PlcDevice device, ...)
    {
        if (!_connectionFactory.IsProtocolSupported(device))
        {
            Logger.Error("Protocol {Protocol} not supported", device.ProtocolType);
            return null;
        }

        var connection = _connectionFactory.CreateConnection(device);
```

`ProtocolConnectionFactory.CreateConnection()` nhìn vào `device.ProtocolType` và trả về:
- `PlcConnection` cho OpcUa
- `SiemensS7Connection` cho SiemensS7
- `MitsubishiMcConnection` cho MitsubishiMc
- `ModbusTcpConnection` cho ModbusTcp

PlcManager không biết và không cần biết implementation cụ thể — chỉ làm việc qua `IPlcConnection` interface. Đây là Open/Closed Principle: thêm protocol mới chỉ cần thêm class mới trong Factory, không cần sửa PlcManager.

**Event aggregation pattern:**

```csharp
        connection.ConnectionStateChanged += OnConnectionStateChanged;
        connection.TagValueChanged += OnTagValueChanged;
        connection.ErrorOccurred += OnErrorOccurred;
```

PlcManager subscribe vào events của mỗi connection. Khi event fires từ bất kỳ connection nào, PlcManager forward lên MainViewModel. MainViewModel chỉ cần subscribe vào PlcManager một chỗ thay vì phải subscribe vào từng connection riêng (và manage lifecycle).

```csharp
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Logger.Information("PLC {Name}: {Old} -> {New}", e.PlcName, e.OldState, e.NewState);
        ConnectionStateChanged?.Invoke(this, e);  // forward nguyên event args
    }
```

Log thêm ở đây — tất cả state changes đều được ghi, ngay cả khi MainViewModel không handle.

---

### Services/Auth/OperatorLockService.cs — Hệ thống khoá operator

**Use case:** Trong môi trường nhiều người vận hành, chỉ 1 người được phép ghi giá trị vào PLC tại một thời điểm để tránh xung đột. OperatorLock giải quyết vấn đề này.

**Lock state persistence — tại sao lưu ra file?**

```csharp
private readonly string _lockStatePath = "Configurations/lock_state.json";
```

Nếu chỉ lưu trong memory, khi app restart lock biến mất. Khi nhiều instance app chạy (supervisor + operator), chúng cần share lock state. File JSON được chia sẻ qua file system.

**FileSystemWatcher — detect external changes:**

```csharp
    private void StartFileWatcher()
    {
        _fileWatcher = new FileSystemWatcher(directory, "lock_state.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnLockFileChanged;
        _fileWatcher.Created += OnLockFileChanged;
        _fileWatcher.Deleted += OnLockFileDeleted;
    }
```

`FileSystemWatcher` monitor file thay đổi từ bên ngoài — khi app B acquire lock, app A nhận event và cập nhật UI.

**Debounce trong file watcher:**

```csharp
    private void OnLockFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_isInternalUpdate) return;  // ignore changes ta tự tạo

        var now = DateTime.UtcNow;
        if ((now - _lastFileChange).TotalMilliseconds < 500) return;  // debounce 500ms
        _lastFileChange = now;

        Task.Delay(100).ContinueWith(_ => ReloadLockStateFromFile());  // 100ms delay
    }
```

`FileSystemWatcher` thường fire nhiều events cho một lần write (Created, Changed, Changed lần nữa). Debounce 500ms gộp chúng thành một xử lý.

`_isInternalUpdate` flag — khi ta tự write file (SaveLockState), không muốn trigger lại watcher của chính mình.

**Atomic file write:**

```csharp
    private void SaveLockState()
    {
        _isInternalUpdate = true;
        var tempPath = _lockStatePath + ".tmp";
        var json = JsonConvert.SerializeObject(_currentLock, Formatting.Indented);
        File.WriteAllText(tempPath, json);  // write vào temp trước

        if (File.Exists(_lockStatePath)) File.Delete(_lockStatePath);
        File.Move(tempPath, _lockStatePath);  // rename atomic (single operation)

        Task.Delay(200).ContinueWith(_ => _isInternalUpdate = false);  // reset sau 200ms
    }
```

Write trực tiếp vào file có thể để lại file incomplete nếu app crash giữa chừng. Write vào `.tmp` trước, rename sau — rename thường là atomic operation trên hệ điều hành (single OS call). App khác đọc file sẽ thấy hoặc version cũ hoặc version mới, không bao giờ thấy version partial.

**TryAcquireLock — logic phức tạp:**

```csharp
    public (bool success, OperatorLock? operatorLock, string? error, ...) TryAcquireLock(...)
    {
        lock (_lock)
        {
            if (role < UserRole.Operator)
                return (false, null, "Insufficient permissions", null, null);

            if (_currentLock != null && !_currentLock.IsExpired)
            {
                if (_currentLock.UserId == userId)
                    return ExtendLockInternal(userId, durationMinutes ?? default);  // refresh

                return (false, null, "Lock held by another", _currentLock.Username, ...);
            }

            // Calculate expiry
            DateTime expiresAt = (role == UserRole.Admin || durationMinutes == 0)
                ? DateTime.MaxValue     // unlimited
                : DateTime.UtcNow.AddMinutes(Math.Min(durationMinutes ?? default, maxDuration));

            _currentLock = new OperatorLock { ... ExpiresAt = expiresAt ... };
            SaveLockState();
            LockAcquired?.Invoke(this, new LockEventArgs { Lock = _currentLock, Reason = "acquired" });
            return (true, _currentLock, null, null, null);
        }
    }
```

Tuple return type `(bool success, OperatorLock?, string? error, string? holderUsername, string? holderDisplayName)` — trả về nhiều thông tin trong một call mà không cần class wrapper riêng. Deconstruct ở caller: `var (ok, lockObj, err, holderName, _) = lockService.TryAcquireLock(...)`.

---

## 8. LAYER 6 — VIEWMODELS

### ViewModels/BrowseServerViewModel.cs

```csharp
public class BrowseServerViewModel : ObservableObject
{
    private readonly PlcConnection _connection;
    private readonly PlcDevice _device;

    public BrowseServerViewModel(PlcConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _device = connection.Device;  // convenience reference
        
        // Initialize commands
        RefreshCommand        = new AsyncRelayCommand(RefreshAsync);
        AddSelectedTagCommand = new RelayCommand(AddSelectedTag, () => CanAddSelectedTag);
        // ...
        
        _ = LoadRootNodesAsync();  // fire and forget — bắt đầu load ngay
    }
```

`_ = LoadRootNodesAsync()` trong constructor — dấu `_` discard kết quả Task. Không `await` vì constructor không async. Task chạy ngầm; khi hoàn thành, `RootNodes` được populate và UI tự cập nhật qua binding.

**LoadRootNodesAsync() — load OPC UA root folders:**

```csharp
    private async Task LoadRootNodesAsync()
    {
        IsBusy = true;

        var rootFolders = new[]
        {
            ("i=85", "Objects"),  // Objects folder — chứa device data
            ("i=86", "Types"),    // Types — kiểu dữ liệu OPC UA
            ("i=87", "Views")     // Views — tùy chỉnh UI của server
        };

        foreach (var (nodeIdStr, fallbackName) in rootFolders)
        {
            var info = await _connection.GetNodeInfoAsync(nodeIdStr);
            var folderNode = new BrowseNodeItem
            {
                NodeId      = nodeIdStr,
                DisplayName = info?.DisplayName ?? fallbackName,  // fallback nếu null
                HasChildren = true
            };
            folderNode.OnExpandRequested += OnNodeExpandRequested;  // lazy load khi expand
            folderNode.AddLoadingPlaceholder();  // thêm "Loading..." item vào tree

            if (nodeIdStr == "i=85")
            {
                await LoadChildrenAsync(folderNode);  // eager load Objects
                folderNode.IsExpanded = true;
            }

            RootNodes.Add(folderNode);
        }
    }
```

`i=85, i=86, i=87` là NodeIds chuẩn của OPC UA — mọi OPC UA server đều có chúng. `i=` prefix nghĩa là `IdentifierType=Numeric`.

Chỉ eager-load `i=85` (Objects) vì đó là nơi chứa data PLC — người dùng quan tâm nhất. `Types` và `Views` load on-demand khi expand.

**Lazy loading qua event:**

```csharp
    private async void OnNodeExpandRequested(BrowseNodeItem node)
    {
        if (node.ChildrenLoaded) return;  // đã load rồi thì thôi
        await LoadChildrenAsync(node);
    }
```

`async void` — event handler không thể return Task. Exception trong async void propagates lên App global handler.

`ChildrenLoaded` flag tránh load lại nhiều lần khi user expand/collapse nhiều lần.

**AddSelectedTag() — thêm tag vào danh sách:**

```csharp
    private void AddSelectedTag()
    {
        if (SelectedNode == null || !SelectedNode.CanAddAsTag) return;

        var subscriptionGroup = _device.SubscriptionGroups.FirstOrDefault(g => g.IsEnabled);
        if (subscriptionGroup == null)
        {
            StatusMessage = "Không có subscription group nào khả dụng";
            return;
        }

        if (_device.Tags.Any(t => t.NodeId == SelectedNode.NodeId))
        {
            StatusMessage = $"Tag '{SelectedNode.DisplayName}' đã tồn tại";
            return;
        }

        var tag = SelectedNode.ToTagItem(_device.Id, subscriptionGroup.Id);
        _device.Tags.Add(tag);       // thêm vào PlcDevice model
        SelectedTags.Add(tag);       // thêm vào collection hiển thị trong window

        StatusMessage = $"Đã thêm tag: {tag.Name}";
    }
```

`_device.Tags.Add(tag)` — add trực tiếp vào `PlcDevice.Tags` (ObservableCollection). Sau khi BrowseServerWindow đóng, MainViewModel có thể truy cập `browseWindow.AddedTags` để biết những tag nào được thêm trong session này.

`SelectedTags` là collection riêng chỉ chứa tags được thêm trong session browse hiện tại — dùng để hiển thị trong panel "Tags đã thêm" và để `BrowseServerWindow.AddedTags` property.

---

### ViewModels/MainViewModel.cs

**Constructor — wiring commands:**

```csharp
    public MainViewModel(IConfigurationService configService, IDataCache dataCache,
        IPlcManager plcManager, UserService userService, ILogger logger)
    {
        // Store all dependencies
        _configService = configService;
        _plcManager    = plcManager;
        // ...

        // Subscribe to PlcManager events
        _plcManager.ConnectionStateChanged += OnPlcConnectionStateChanged;
        _plcManager.TagValueChanged        += OnTagValueChanged;
        _plcManager.ErrorOccurred          += OnPlcErrorOccurred;

        // LogCount phải update khi collection thay đổi
        LogEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogCount));

        // Wire all commands
        AddPlcCommand          = new RelayCommand(AddPlc);
        ConnectSelectedCommand = new AsyncRelayCommand(ConnectSelectedAsync, () => HasSelectedPlc);
        SaveConfigCommand      = new AsyncRelayCommand(SaveConfigurationAsync, () => HasUnsavedChanges);
        BrowseServerCommand    = new AsyncRelayCommand(BrowseServerAsync, () => HasSelectedPlc);
        // ...
    }
```

**Tại sao `LogCount` cần `CollectionChanged`?**

`LogCount` là property computed: `public int LogCount => LogEntries.Count`. Khi `LogEntries.Add()` được gọi, `ObservableCollection` fire `CollectionChanged` — nhưng WPF binding chỉ update controls binding vào `LogEntries` trực tiếp (ListView, ItemsControl), không update `LogCount` vì WPF không biết `LogCount` phụ thuộc vào `LogEntries.Count`. Ta phải manually raise `PropertyChanged` cho `LogCount` mỗi khi collection thay đổi.

**OnPlcConnectionStateChanged — thread marshalling:**

```csharp
    private void OnPlcConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ConnectedPlcCount = _plcManager.ConnectedCount;
            IsConnected = ConnectedPlcCount > 0;
            OnPropertyChanged(nameof(ConnectionStatusText));
            StatusMessage = $"{e.PlcName}: {e.NewState}";

            var plc = PlcDevices.FirstOrDefault(p => p.Id == e.PlcId);
            if (plc != null) plc.ConnectionState = e.NewState;
        });
    }
```

Event này đến từ OPC UA session thread — bắt buộc dispatch về UI thread. `Dispatcher.InvokeAsync` (async, non-blocking) thay vì `Dispatcher.Invoke` (sync, blocking). Blocking có thể gây deadlock nếu UI thread đang chờ background thread.

**AddPlc() — luồng đầy đủ:**

```csharp
    private void AddPlc()
    {
        var dialog = new Views.AddPlcDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        var newPlc = dialog.Result;

        // 1. Thêm vào UI list ngay (optimistic update)
        PlcDevices.Add(newPlc);
        _configService.CurrentConfiguration.PlcDevices.Add(newPlc);
        _configService.MarkAsModified();

        // 2. Tạo PlcConnection object (không connect)
        _ = _plcManager.AddPlcAsync(newPlc);

        // 3. Auto-connect nếu user chọn
        if (dialog.ConnectAfterAdd)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var ok = await _plcManager.ConnectAsync(newPlc.Id);
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (ok)
                        {
                            StatusMessage = $"Connected to {newPlc.Name}. Đang mở Browse Server...";
                            OpenBrowseServerForNewPlc(newPlc);
                        }
                        else
                        {
                            StatusMessage = $"Failed to connect to {newPlc.Name}";
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Connection error for {Name}", newPlc.Name);
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                        StatusMessage = $"Connection error: {ex.Message}");
                }
            });
        }
    }
```

`Task.Run()` — đẩy `ConnectAsync` ra background thread vì nó network I/O, có thể mất vài giây. Nếu chạy trên UI thread, window sẽ freeze.

Sau khi connect xong, `Dispatcher.InvokeAsync` trở về UI thread để mở BrowseServerWindow (ShowDialog phải chạy trên UI thread).

**OpenBrowseServerForNewPlc() — type check:**

```csharp
    private void OpenBrowseServerForNewPlc(PlcDevice plc)
    {
        var connection = _plcManager.GetConnection(plc.Id);
        if (connection is not Services.OpcUa.PlcConnection plcConnection)
        {
            // Không phải OPC UA — Siemens S7 / Modbus không có Browse Server
            StatusMessage = $"Connected to {plc.Name}";
            return;
        }

        SelectedPlc = plc;
        var browseWindow = new Views.BrowseServerWindow(plcConnection)
        {
            Owner = Application.Current.MainWindow
        };

        var result = browseWindow.ShowDialog();

        if (result == true && browseWindow.AddedTags.Any())
        {
            OnPropertyChanged(nameof(SelectedPlcTags));
            OnPropertyChanged(nameof(TotalTagCount));
            StatusMessage = $"Đã thêm {browseWindow.AddedTags.Count} tags từ {plc.Name}";
        }
    }
```

`connection is not Services.OpcUa.PlcConnection plcConnection` — C# pattern matching. Nếu `connection` không phải `PlcConnection`, trả về sớm. Nếu là, `plcConnection` variable được set. Chỉ OPC UA có Browse Server.

---

## 9. LAYER 7 — APP STARTUP (App.xaml.cs)

### OnStartup() — trình tự khởi động

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // BƯỚC 1: Ngăn app tắt khi LoginWindow đóng
    ShutdownMode = ShutdownMode.OnExplicitShutdown;
```

WPF mặc định: khi cửa sổ đầu tiên đóng → app tắt. Nhưng LoginWindow đóng sau khi login → MainWindow mở. Nếu dùng default, app sẽ tắt ngay khi LoginWindow đóng. `OnExplicitShutdown` cho phép ta kiểm soát hoàn toàn.

```csharp
    // BƯỚC 2: Serilog lần đầu (chưa có UI sink)
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("Logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
        .CreateLogger();
```

Chưa thêm UiSink vì `_mainViewModel.LogEntries` chưa tồn tại. `RollingInterval.Day` = mỗi ngày một file mới. `retainedFileCountLimit: 7` = giữ 7 ngày rồi xóa — không để disk đầy.

```csharp
    // BƯỚC 3: Ba global exception handlers
    DispatcherUnhandledException += App_DispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
    TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
```

| Handler | Thread | Có thể prevent crash? | Khi nào fire? |
|---------|--------|----------------------|---------------|
| Dispatcher | UI thread | Có (e.Handled=true) | Exception không được catch trong event handler, command |
| AppDomain | Any | Không (e.IsTerminating) | Exception trong background thread |
| TaskScheduler | Any | Có (e.SetObserved()) | Task throw exception không được awaited |

```csharp
    // BƯỚC 4: DI setup
    var services = new ServiceCollection();
    ConfigureServices(services);
    _serviceProvider = services.BuildServiceProvider();

    // BƯỚC 5: Login
    var userService = _serviceProvider.GetRequiredService<UserService>();
    var loginWindow = new LoginWindow(userService);
    var loginResult = loginWindow.ShowDialog();  // blocks cho đến khi window đóng

    if (loginResult != true || loginWindow.LoggedInUser == null)
    {
        Shutdown(0);   // user cancel login → tắt app
        return;
    }

    _loggedInUser = loginWindow.LoggedInUser;
```

`ShowDialog()` trả về `bool?`. `true` = DialogResult=true (login success). `false` = DialogResult=false. `null` = window đóng bằng X hoặc Alt+F4.

```csharp
    // BƯỚC 6: Tạo MainWindow và MainViewModel từ DI
    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
    _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
    _mainViewModel.SetLoggedInUser(_loggedInUser);

    // BƯỚC 7: Thêm UiSink vào Serilog
    Log.Logger = new LoggerConfiguration()
        // ... same as before ...
        .WriteTo.UiSink(_mainViewModel.LogEntries, maxEntries: 500)  // NEW
        .CreateLogger();
```

Serilog được recreate hoàn toàn — không phải add sink vào instance cũ. Đây là thiết kế của Serilog: immutable logger, phải recreate khi muốn thêm sink.

Từ đây trở đi, mọi `Log.Information(...)` trong toàn bộ app (kể cả PlcConnection, PlcManager, ...) sẽ xuất hiện trên UI panel — vì chúng dùng `Log.Logger` static.

```csharp
    // BƯỚC 8: Switch shutdown mode
    MainWindow = mainWindow;
    ShutdownMode = ShutdownMode.OnMainWindowClose;  // bình thường từ đây
    mainWindow.Show();

    // BƯỚC 9: Initialize async (fire and forget)
    _ = InitializeViewModelAsync(_mainViewModel);
}
```

`mainWindow.Show()` trước `_ = InitializeViewModelAsync()` — MainWindow hiển thị ngay. Trong lúc load config (có thể mất 1-2 giây), user thấy UI (tuy empty). Nếu `await InitializeViewModelAsync()` trước Show(), user thấy màn hình trắng trong 2 giây.

```csharp
private async Task InitializeViewModelAsync(MainViewModel viewModel)
{
    await Task.Delay(100);      // đợi 100ms để UI render xong trước
    await viewModel.InitializeAsync();
    await StartApiServerAsync();
}
```

`Task.Delay(100)` — nhỏ nhưng quan trọng. Cho phép WPF message loop xử lý pending render messages. Không có delay, `InitializeAsync` có thể bắt đầu trước khi MainWindow fully rendered.

**ConfigureServices — chi tiết DI:**

```csharp
// Singleton logger — nhưng PlcConnection/PlcManager không dùng cái này
services.AddSingleton<ILogger>(Log.Logger);

// Load ApiSettings từ file trước khi register
_apiSettings = LoadApiSettings();
services.AddSingleton(_apiSettings);

// Transient MainWindow — mỗi lần resolve tạo instance mới
// (trong thực tế chỉ tạo 1 lần)
services.AddTransient<MainWindow>();

// Singleton MainViewModel — tạo 1 lần, sống suốt vòng đời app
services.AddSingleton<MainViewModel>();
```

**OnExit() — cleanup có thứ tự:**

```csharp
protected override void OnExit(ExitEventArgs e)
{
    // 1. Stop API server (có thể mất vài giây)
    _apiHostService?.StopAsync().GetAwaiter().GetResult();  // sync wait
    _apiHostService?.Dispose();

    // 2. Disconnect và dispose tất cả PLC connections
    var plcManager = _serviceProvider?.GetService<IPlcManager>();
    plcManager?.Dispose();

    // 3. Flush log buffer ra file
    Log.CloseAndFlush();

    base.OnExit(e);
}
```

`.GetAwaiter().GetResult()` là cách call async method từ sync context. Tránh dùng `.Wait()` vì có thể deadlock trong một số context. `.GetAwaiter().GetResult()` cũng đúng semantics hơn: nếu task throw exception, nó re-throw synchronously thay vì wrap trong `AggregateException`.

`Log.CloseAndFlush()` phải là dòng cuối — đảm bảo không mất log nào. Nếu call trước, các logs từ các Dispose() calls sau sẽ bị mất.

---

## 10. LAYER 8 — VIEWS

### Views/LoginWindow.xaml.cs

```csharp
public LoginWindow(UserService userService)
{
    InitializeComponent();
    _userService = userService;

    LoadSavedCredentials();

    // Chromeless window — phải tự xử lý drag
    MouseLeftButtonDown += (_, e) =>
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    };

    Loaded += (_, _) =>
    {
        // Focus vào đúng field sau khi load
        if (string.IsNullOrEmpty(UserBox.Text)) UserBox.Focus();
        else PasswordBox.Focus();
    };
}
```

`Loaded` event thay vì `MouseLeftButtonDown` làm focus — `Loaded` fires sau khi controls đã render và có thể nhận focus. Nếu `Focus()` trong constructor, control chưa visible nên sẽ không có tác dụng.

**Password show/hide pattern:**

```csharp
private void ShowPwdBtn_Changed(object sender, RoutedEventArgs e)
{
    bool show = ShowPwdBtn.IsChecked == true;
    if (show)
    {
        PasswordTextBox.Text = PasswordBox.Password;  // copy sang TextBox
        PasswordBox.Visibility = Visibility.Collapsed;
        PasswordTextBox.Visibility = Visibility.Visible;
        PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;  // cursor cuối
    }
    else
    {
        PasswordBox.Password = PasswordTextBox.Text;  // copy sang PasswordBox
        PasswordTextBox.Visibility = Visibility.Collapsed;
        PasswordBox.Visibility = Visibility.Visible;
    }
}
```

**Tại sao không chỉ dùng `PasswordBox.Password` binding?** `PasswordBox.Password` không thể bind two-way qua XAML vì security — password không được lưu dưới dạng `string` (DependencyProperty) trong WPF để tránh bị inspect từ memory dump. Ta phải dùng code-behind.

**SignIn_Click:**

```csharp
private void SignIn_Click(object sender, RoutedEventArgs e)
{
    HideError();
    var username = UserBox.Text?.Trim() ?? string.Empty;
    var password = GetPassword();

    if (string.IsNullOrWhiteSpace(username)) { ShowError("Vui lòng nhập tên đăng nhập."); return; }
    if (string.IsNullOrEmpty(password)) { ShowError("Vui lòng nhập mật khẩu."); return; }

    LoginButton.IsEnabled = false;  // prevent double-click

    try
    {
        LoggedInUser = _userService.ValidateCredentials(username, password);

        if (LoggedInUser != null)
        {
            if (RememberMeCheckBox.IsChecked == true) SaveCredentials(username);
            else ClearSavedCredentials();

            DialogResult = true;  // signal thành công cho ShowDialog() caller
            Close();
        }
        else
        {
            ShowError("Tên đăng nhập hoặc mật khẩu không đúng.");
            ClearPassword();
            PasswordBox.Focus();
        }
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Login error");
        ShowError($"Đăng nhập thất bại: {ex.Message}");
    }
    finally
    {
        LoginButton.IsEnabled = true;  // always re-enable
    }
}
```

`username.Trim()` — xóa whitespace đầu/cuối. User thường copy-paste username có trailing space. `ClearPassword()` sau login thất bại — không để password còn trong ô sau khi nhập sai.

**SavedCredentials — không bao giờ lưu password:**

```csharp
public class SavedCredentials
{
    public string Username { get; set; } = string.Empty;
    // Không có Password field
}
```

Lưu username thôi — user chỉ cần nhập password mỗi lần (security tradeoff). Lưu password thậm chí encrypted vẫn là bad practice cho desktop app.

---

### Views/MainWindow.xaml — Context Menu ngoài visual tree

**Vấn đề:** `ContextMenu` là `Popup` — nó không nằm trong visual tree của `ListBox`. Khi XAML binding `{Binding ConnectSelectedCommand}`, WPF traverse visual tree lên tìm DataContext nhưng dừng lại ở visual tree boundary (Popup wall).

**Giải pháp: Tag + PlacementTarget:**

```xml
<!-- Trong ListBox.ItemContainerStyle -->
<Setter Property="Tag"
        Value="{Binding DataContext, RelativeSource={RelativeSource AncestorType=ListBox}}"/>
<EventSetter Event="PreviewMouseRightButtonDown" Handler="PlcItem_RightClick"/>
<Setter Property="ContextMenu">
    <Setter.Value>
        <ContextMenu DataContext="{Binding PlacementTarget.Tag,
                                          RelativeSource={RelativeSource Self}}">
            <MenuItem Header="⚡ Kết nối"    Command="{Binding ConnectSelectedCommand}"/>
            <MenuItem Header="⏸ Ngắt kết nối" Command="{Binding DisconnectSelectedCommand}"/>
            <Separator/>
            <MenuItem Header="⎇ Browse Server" Command="{Binding BrowseServerCommand}"/>
            <Separator/>
            <MenuItem Header="✏ Sửa"  Command="{Binding EditPlcCommand}"/>
            <MenuItem Header="🗑 Xóa" Command="{Binding DeletePlcCommand}"/>
        </ContextMenu>
    </Setter.Value>
</Setter>
```

1. `Tag = MainViewModel` — `ListBoxItem.Tag` lưu reference đến MainViewModel (lấy từ ListBox.DataContext)
2. `ContextMenu.PlacementTarget` = ListBoxItem mà menu được hiển thị từ đó
3. `PlacementTarget.Tag` = MainViewModel đã lưu ở bước 1
4. Commands trong MenuItem bind vào MainViewModel

**PreviewMouseRightButtonDown để auto-select:**

```csharp
private void PlcItem_RightClick(object sender, MouseButtonEventArgs e)
{
    if (sender is ListBoxItem item)
        item.IsSelected = true;   // select item trước khi hiển thị ContextMenu
}
```

WPF không auto-select ListBoxItem khi right-click (chỉ left-click mới select). Nếu không có handler này, user right-click PLC2 nhưng PLC1 vẫn selected → ContextMenu commands tác động lên PLC1 sai.

`PreviewMouseRightButtonDown` thay vì `MouseRightButtonDown` — `Preview` events (tunnel từ root xuống) fire trước regular events (bubble từ element lên). Đảm bảo item được select trước khi ContextMenu nhận sự kiện.

---

### Views/BrowseServerWindow.xaml.cs

```csharp
public partial class BrowseServerWindow : Window
{
    private readonly BrowseServerViewModel _viewModel;

    public BrowseServerWindow(PlcConnection connection)
    {
        InitializeComponent();
        _viewModel = new BrowseServerViewModel(connection);
        DataContext = _viewModel;
    }

    // Expose tags được thêm cho caller (MainViewModel)
    public IReadOnlyList<TagItem> AddedTags => _viewModel.SelectedTags.ToList();

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BrowseNodeItem node)
            _viewModel.SelectedNode = node;
    }
```

`TreeView_SelectedItemChanged` — TreeView có riêng event `SelectedItemChanged` với `RoutedPropertyChangedEventArgs<object>`. `e.NewValue as BrowseNodeItem` safe-cast — null nếu không đúng type (placeholder items).

```csharp
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.SelectedTags.Any();  // true nếu có tag nào được thêm
        Close();
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_viewModel.SelectedNode?.CanAddAsTag == true)
            _viewModel.AddSelectedTagCommand.Execute(null);
    }
}
```

`OnMouseDoubleClick` override — double click trên TreeView node nào có `CanAddAsTag=true` thì add ngay, nhanh hơn phải click nút "Thêm tag". UX improvement.

---

## 11. CÁC BUG ĐÃ FIX

### Bug 1: NodeId.Parse vs new NodeId

**Vấn đề:** `new NodeId("i=85")` tạo NodeId sai kiểu.

```
new NodeId("i=85")  →  Type=String, Identifier="i=85"  ← SAI
NodeId.Parse("i=85") →  Type=Numeric, Identifier=85    ← ĐÚNG
```

OPC UA server từ chối String NodeId cho nodes chuẩn (Objects, Types, Views folders). Kết quả: `BrowseAsync()` trả về empty list không có lỗi — khó debug.

**Fix:** Đổi `new NodeId(nodeId)` → `NodeId.Parse(nodeId)` trong `BrowseAsync()` và `GetNodeInfoAsync()`.

### Bug 2: BrowseServerViewModel gọi trực tiếp OPC UA SDK

**Vấn đề:** BrowseServerViewModel trước đây inject `Session` trực tiếp và gọi `_session.Browse()`, `_session.ReadNode()`. Điều này:
- Bypass service layer (PlcConnection)
- Không có error handling, logging
- PlcConnection có thể đang reconnect → session thay đổi mà ViewModel không biết

**Fix:** Đổi constructor nhận `PlcConnection` thay vì `Session`. Dùng `_connection.BrowseAsync()`, `_connection.GetNodeInfoAsync()`, `_connection.ReadTagAsync()`.

### Bug 3: ContextMenu binding thất bại

**Vấn đề:** `ContextMenu` là Popup nằm ngoài visual tree. `RelativeSource AncestorType=Window` hoặc `AncestorType=ListBox` không traverse qua visual tree boundary → binding fail silently (command = null → exception hoặc không làm gì).

**Fix:** Tag pattern — lưu ViewModel vào `Tag` property của `ListBoxItem`, ContextMenu bind vào `PlacementTarget.Tag`.

---

## 12. PATTERNS VÀ QUYẾT ĐỊNH KIẾN TRÚC

### Pattern 1: MVVM với WPF Binding

**Vì sao dùng MVVM thay vì code-behind thuần?**

```
Code-behind thuần:
  Button.Click → fetch data → update TextBox.Text trực tiếp
  → logic trộn lẫn với UI, khó test, khó maintain

MVVM:
  Button → Command → ViewModel method → update property → Binding → TextBox tự update
  → logic tách biệt, có thể unit test ViewModel mà không cần UI
```

### Pattern 2: ObservableCollection cho Live Updates

```csharp
public ObservableCollection<PlcDevice> PlcDevices { get; } = new();
public ObservableCollection<LogEntry> LogEntries { get; } = new();
```

`ObservableCollection<T>` implements `INotifyCollectionChanged` — khi `Add()` hoặc `Remove()`, ListBox/DataGrid tự cập nhật mà không cần reassign `ItemsSource`. `List<T>` không có mechanism này.

### Pattern 3: Fire-and-Forget với `_ =`

```csharp
_ = Task.Run(async () => { ... });           // background work
_ = InitializeViewModelAsync(_mainViewModel); // startup task
_ = LoadRootNodesAsync();                    // ViewModel init
```

Dấu `_` (discard) tắt warning "CS4014: Because this call is not awaited...". Không phải ignore exception — vẫn cần handle trong task body. Dùng khi: tác vụ phụ không ảnh hưởng luồng chính, hoặc không cần chờ kết quả.

### Pattern 4: `private static ILogger Logger => Log.Logger`

Dùng cho các class được tạo trước khi UiSink được add (PlcConnection, PlcManager). Static property thay vì field — mỗi lần gọi lấy logger hiện tại, không phải instance cũ đã captured.

### Pattern 5: Atomic file write (temp + rename)

```csharp
File.WriteAllText(tempPath, json);  // write vào .tmp
File.Delete(targetPath);
File.Move(tempPath, targetPath);    // rename — atomic trên OS level
```

Tránh corrupt file khi app crash giữa write. Dùng cho lock_state.json và có thể áp dụng cho config file.

### Pattern 6: Double-check locking

```csharp
if (_reconnectHandler == null)           // check ngoài lock (fast path)
{
    lock (_lock)
    {
        if (_reconnectHandler == null)   // check lại trong lock (safe)
        {
            _reconnectHandler = new SessionReconnectHandler(...);
        }
    }
}
```

Tối ưu performance: kiểm tra không lock trước (99% cases). Chỉ lock và kiểm tra lại khi `null`. Tránh overhead của lock khi không cần.

### Pattern 7: CancellationToken trên async methods

```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
```

`default` = `CancellationToken.None` — không cancellable nếu caller không truyền token. Dùng khi: user click Cancel trong lúc đang connect → cancel operation. Propagate token xuống `Session.Create()`, `Task.Delay()`, v.v.

### Pattern 8: IReadOnlyList return type

```csharp
public async Task<IReadOnlyList<BrowseNode>> BrowseAsync(...) { ... }
```

Trả về `IReadOnlyList<T>` thay vì `List<T>` — caller không thể `.Add()` hay `.Remove()` vào list trả về. Tránh side effects. Caller phải tạo copy riêng nếu muốn modify.

---

## TỔNG KẾT

Dự án OPC UA Communication Engine được xây dựng theo kiến trúc phân lớp rõ ràng:

| Lớp | Trách nhiệm | Files chính |
|-----|-------------|-------------|
| Enums | Kiểu dữ liệu liệt kê | Enums/*.cs |
| Models | Data transfer + binding | Models/*.cs |
| Interfaces | Contracts giữa layers | Interfaces/*.cs |
| Helpers | Utilities tái sử dụng | Helpers/*.cs |
| Services | Business logic + I/O | Services/**/*.cs |
| ViewModels | UI state + commands | ViewModels/*.cs |
| Views | XAML + event handlers | Views/*.xaml.cs |
| App | DI wiring + startup | App.xaml.cs |

**Nguyên tắc cốt lõi:**
1. **Dependency flows one way** — View → ViewModel → Service → Model. Không bao giờ ngược lại.
2. **UI thread safety** — Mọi thay đổi UI phải qua `Dispatcher`. `ObservableObject` tự handle.
3. **Fail gracefully** — Self-healing configs, BCrypt try/catch, connection retry với exponential backoff.
4. **Separation of concerns** — PlcConnection chỉ biết OPC UA. PlcManager chỉ biết quản lý connections. ViewModel chỉ biết UI state.
5. **Logs everywhere** — Serilog với structured logging ở mọi critical path, UI sink cho operator.

---

*Tài liệu này được tạo tự động từ source code của dự án OPCUACommDriver.*  
*Ngày: 2026-06-19*
