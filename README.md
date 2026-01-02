# OPC UA Communication Engine - Phase 1

## 📋 Tổng Quan

Đây là Phase 1 của dự án OPC UA Multi-PLC Communication Engine - một ứng dụng WPF Desktop để giao tiếp với nhiều PLC thông qua giao thức OPC UA.

### Phase 1 bao gồm:
- ✅ Data Models (PLCDevice, TagItem, SubscriptionGroup)
- ✅ Configuration Service (Load/Save JSON)
- ✅ Data Cache Service (Thread-safe)
- ✅ MVVM Architecture
- ✅ Basic WPF UI

---

## 🛠️ Yêu Cầu Hệ Thống

- **OS**: Windows 10/11
- **.NET SDK**: 8.0 trở lên
- **IDE**: Visual Studio 2022 hoặc VS Code với C# extension
- **PLC**: Có OPC UA Server (Siemens S7-1500, Beckhoff, Kepware, etc.)

---

## 📁 Cấu Trúc Project

```
OpcUaCommunicationEngine/
├── Configurations/          # File cấu hình JSON
│   ├── appsettings.json    # Cài đặt ứng dụng
│   └── plc_config.json     # Cấu hình PLC và Tags
├── Enums/                   # Các enum types
│   ├── PlcConnectionState.cs
│   ├── OpcUaSecurityPolicy.cs
│   └── TagEnums.cs
├── Models/                  # Data models
│   ├── ObservableObject.cs # Base class với INotifyPropertyChanged
│   ├── PlcDevice.cs        # Model cho PLC
│   ├── TagItem.cs          # Model cho Tag/Node
│   ├── SubscriptionGroup.cs # Nhóm subscription theo scan rate
│   ├── AppConfiguration.cs # Cấu hình toàn bộ app
│   └── TagValue.cs         # Giá trị runtime của Tag
├── Interfaces/              # Contracts/Interfaces
│   ├── IConfigurationService.cs
│   ├── IPlcManager.cs
│   └── IDataCache.cs
├── Services/                # Business logic
│   ├── ConfigurationService.cs
│   └── DataCacheService.cs
├── ViewModels/              # MVVM ViewModels
│   ├── ViewModelBase.cs
│   └── MainViewModel.cs
├── Views/                   # WPF Views
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
├── Helpers/                 # Utilities
│   ├── RelayCommand.cs     # ICommand implementation
│   └── Converters.cs       # Value converters cho binding
├── App.xaml                 # Application resources
├── App.xaml.cs              # Application entry point với DI
└── OpcUaCommunicationEngine.csproj
```

---

## 🚀 Hướng Dẫn Cài Đặt

### Bước 1: Clone/Copy Project

```bash
# Copy toàn bộ folder OpcUaCommunicationEngine vào thư mục làm việc
```

### Bước 2: Restore NuGet Packages

```bash
cd OpcUaCommunicationEngine
dotnet restore
```

### Bước 3: Build Project

```bash
dotnet build
```

### Bước 4: Chạy Ứng Dụng

```bash
dotnet run
```

---

## 📝 Hướng Dẫn Sử Dụng

### 1. Cấu Hình PLC

#### Qua Giao Diện:
1. Click **"Add PLC"** trên toolbar
2. Nhập thông tin PLC trong panel Properties:
   - **Name**: Tên hiển thị của PLC
   - **Endpoint**: URL OPC UA (vd: `opc.tcp://192.168.1.100:4840`)
   - **Session Timeout**: Thời gian timeout (ms)
   - **KeepAlive**: Khoảng thời gian keep alive (ms)
3. Click **Save** (Ctrl+S) để lưu cấu hình

#### Qua File JSON:
Chỉnh sửa file `Configurations/plc_config.json`:

```json
{
  "PlcDevices": [
    {
      "Id": "plc-001",
      "Name": "Siemens S7-1500",
      "EndpointUrl": "opc.tcp://192.168.1.100:4840",
      "IsEnabled": true,
      "SecurityPolicy": 0,
      "SecurityMode": 1,
      "SessionTimeout": 60000,
      "KeepAliveInterval": 5000,
      "AutoReconnect": true
    }
  ]
}
```

### 2. Cấu Hình Tags

#### Qua Giao Diện:
1. Chọn PLC trong danh sách bên trái
2. Click **"Add Tag"**
3. Nhập thông tin Tag trong DataGrid:
   - **Name**: Tên của Tag
   - **NodeId**: OPC UA NodeId (vd: `ns=3;s="DB1"."Temperature"`)
   - **Data Type**: Kiểu dữ liệu
   - **Access**: Read/Write/ReadWrite
   - **Scan Rate**: Tốc độ quét (ms)

#### Định dạng NodeId phổ biến:

| Loại PLC | Ví dụ NodeId |
|----------|--------------|
| Siemens S7 | `ns=3;s="DB1"."Temperature"` |
| Beckhoff | `ns=4;s=MAIN.Temperature` |
| Generic | `ns=2;i=1001` (numeric) |
| Generic | `ns=2;s=MyVariable` (string) |

### 3. Lưu Cấu Hình

- **Ctrl+S**: Lưu nhanh
- **File → Save As**: Lưu với tên mới
- Cấu hình được lưu dạng JSON, dễ dàng backup và chia sẻ

---

## 🔧 Cấu Hình Chi Tiết

### Security Policy

```
0 = None (không bảo mật - chỉ dùng cho test)
1 = Basic128Rsa15 (deprecated)
2 = Basic256 (deprecated)
3 = Basic256Sha256 (khuyến khích)
4 = Aes128_Sha256_RsaOaep
5 = Aes256_Sha256_RsaPss (bảo mật cao nhất)
```

### Security Mode

```
1 = None (không ký, không mã hóa)
2 = Sign (chỉ ký)
3 = SignAndEncrypt (ký và mã hóa)
```

### Data Types

```
0 = Boolean       5 = Int32        10 = Double
1 = SByte         6 = UInt32       11 = String
2 = Byte          7 = Int64        12 = DateTime
3 = Int16         8 = UInt64       13 = ByteString
4 = UInt16        9 = Float        99 = Unknown
```

### Scan Rate Guidelines

| Loại dữ liệu | Scan Rate khuyến nghị |
|--------------|----------------------|
| Critical/Safety | 100ms |
| Process Values | 500ms |
| Status/Alarms | 1000ms |
| Configuration | 5000ms+ |

---

## 🔌 Kết Nối Với PLC Thật

### Siemens S7-1500/1200

1. **Bật OPC UA Server trên PLC:**
   - Trong TIA Portal, vào **Device Configuration**
   - Mở **OPC UA** → **Server**
   - Enable **"Activate OPC UA Server"**
   - Chọn **Security Policy** (khuyên dùng None cho test ban đầu)

2. **Cấu hình Endpoint:**
   ```
   opc.tcp://[IP_PLC]:4840
   ```

3. **Tìm NodeId:**
   - Sử dụng tool **UaExpert** hoặc **Prosys OPC UA Browser**
   - Browse đến tag cần đọc
   - Copy NodeId (vd: `ns=3;s="DB1"."Temperature"`)

### Beckhoff TwinCAT

1. **Cấu hình OPC UA Server:**
   - Trong TwinCAT XAE, add **TF6100 OPC UA Server**
   - Configure trong **OPC UA Configurator**

2. **Endpoint thường là:**
   ```
   opc.tcp://[IP]:4840
   ```

3. **NodeId format:**
   ```
   ns=4;s=MAIN.VariableName
   ```

### Kepware KEPServerEX

1. **Tạo Channel và Device** cho PLC
2. **Enable OPC UA Server** trong settings
3. **Endpoint mặc định:**
   ```
   opc.tcp://localhost:49320
   ```

---

## 🔍 Troubleshooting

### Lỗi "BadSecurityModeRejected"

**Nguyên nhân**: Security mode không khớp với server

**Giải pháp**:
- Đặt SecurityPolicy = 0 (None)
- Đặt SecurityMode = 1 (None)
- Hoặc import certificate của client vào server

### Lỗi "BadCertificateUntrusted"

**Nguyên nhân**: Certificate chưa được trust

**Giải pháp**:
1. Lần đầu kết nối, certificate sẽ bị rejected
2. Vào folder rejected certificates của server
3. Move certificate sang trusted folder
4. Thử kết nối lại

### Lỗi "BadNodeIdUnknown"

**Nguyên nhân**: NodeId không tồn tại trên server

**Giải pháp**:
- Kiểm tra lại NodeId bằng OPC UA Browser
- Đảm bảo namespace index (ns=X) đúng
- Đảm bảo tên biến đúng chính tả

### Lỗi "Timeout"

**Nguyên nhân**: Không thể kết nối đến server

**Giải pháp**:
- Kiểm tra IP và Port
- Kiểm tra firewall (mở port 4840)
- Ping PLC để đảm bảo network thông

---

## 📊 Performance Tips

### Tối ưu số lượng Tags

- Nhóm Tags theo scan rate (Subscription Groups)
- Không đọc Tags không cần thiết
- Sử dụng deadband cho analog values

### Giới hạn khuyến nghị

| Metric | Giới hạn |
|--------|---------|
| Monitored items/subscription | 50,000 |
| Monitored items/session | 1,000,000 |
| Updates/second/session | ~25,000 |
| Total throughput | ~35,000 items/sec |

### Memory Usage

- Mỗi Tag cache chiếm ~100-200 bytes
- 10,000 Tags ≈ 1-2 MB RAM
- Log files rotate theo ngày, giữ 7 ngày

---

## 📅 Roadmap

### Phase 2: Core Communication Engine
- [ ] OPC UA Session Management
- [ ] Subscription/MonitoredItem handling
- [ ] Auto-reconnect logic
- [ ] Read/Write operations

### Phase 3: WPF UI Enhancement
- [ ] PLC Edit Dialog
- [ ] Tag Edit Dialog
- [ ] OPC UA Browser (TreeView)
- [ ] Real-time value display

### Phase 4: Testing & Deployment
- [ ] Unit tests
- [ ] Performance testing
- [ ] Documentation
- [ ] Installer

---

## 📞 Support

Nếu gặp vấn đề, hãy kiểm tra:
1. File log trong folder `Logs/`
2. Cấu hình trong `Configurations/`
3. Kết nối network đến PLC

---

## 📄 License

MIT License - Free to use and modify.
