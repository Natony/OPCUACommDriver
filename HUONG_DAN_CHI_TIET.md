# 📖 HƯỚNG DẪN CHI TIẾT TỪNG BƯỚC
# OPC UA Communication Engine - Phase 1

---

## 🎯 MỤC TIÊU PHASE 1

Phase 1 tập trung vào việc xây dựng:
1. **Data Models** - Các class định nghĩa cấu trúc dữ liệu
2. **Configuration Management** - Quản lý cấu hình PLC/Tags
3. **Basic WPF UI** - Giao diện người dùng cơ bản

---

## 📦 BƯỚC 1: CÀI ĐẶT MÔI TRƯỜNG

### 1.1 Cài đặt .NET 8 SDK

```bash
# Windows (dùng winget)
winget install Microsoft.DotNet.SDK.8

# Hoặc tải từ https://dotnet.microsoft.com/download/dotnet/8.0
```

Kiểm tra cài đặt:
```bash
dotnet --version
# Output: 8.0.x
```

### 1.2 Cài đặt Visual Studio 2022 (Recommended)

1. Tải Visual Studio 2022 Community (free) từ https://visualstudio.microsoft.com/
2. Chọn workload: **".NET Desktop Development"**
3. Đảm bảo chọn:
   - .NET 8.0 Runtime
   - Windows App SDK

### 1.3 Alternative: VS Code

```bash
# Cài đặt VS Code extensions
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
```

---

## 📁 BƯỚC 2: TẠO PROJECT

### 2.1 Copy Source Code

Copy toàn bộ folder `OpcUaCommunicationEngine` vào thư mục làm việc.

### 2.2 Cấu trúc files đã có

```
OpcUaCommunicationEngine/
│
├── 📄 OpcUaCommunicationEngine.csproj    # Project file
├── 📄 App.xaml                           # Application resources
├── 📄 App.xaml.cs                        # Entry point + DI setup
│
├── 📁 Enums/
│   ├── PlcConnectionState.cs     # Trạng thái kết nối
│   ├── OpcUaSecurityPolicy.cs    # Security options
│   └── TagEnums.cs               # Tag data types, quality
│
├── 📁 Models/
│   ├── ObservableObject.cs       # Base class với INotifyPropertyChanged
│   ├── PlcDevice.cs              # ⭐ Model PLC
│   ├── TagItem.cs                # ⭐ Model Tag
│   ├── SubscriptionGroup.cs      # Nhóm Tags theo scan rate
│   ├── AppConfiguration.cs       # Root configuration
│   └── TagValue.cs               # Runtime value cache
│
├── 📁 Interfaces/
│   ├── IConfigurationService.cs  # Contract cho config
│   ├── IPlcManager.cs            # Contract cho PLC management
│   └── IDataCache.cs             # Contract cho data cache
│
├── 📁 Services/
│   ├── ConfigurationService.cs   # ⭐ Load/Save JSON config
│   └── DataCacheService.cs       # Thread-safe value cache
│
├── 📁 ViewModels/
│   ├── ViewModelBase.cs          # Base ViewModel
│   └── MainViewModel.cs          # ⭐ Main window logic
│
├── 📁 Views/
│   ├── MainWindow.xaml           # ⭐ Main UI
│   └── MainWindow.xaml.cs        # Code-behind
│
├── 📁 Helpers/
│   ├── RelayCommand.cs           # ICommand implementation
│   └── Converters.cs             # Value converters
│
└── 📁 Configurations/
    ├── appsettings.json          # App settings
    └── plc_config.json           # ⭐ PLC & Tags config
```

---

## 🔧 BƯỚC 3: RESTORE VÀ BUILD

### 3.1 Mở Terminal/Command Prompt

```bash
cd path/to/OpcUaCommunicationEngine
```

### 3.2 Restore NuGet Packages

```bash
dotnet restore
```

**Packages sẽ được download:**
- OPCFoundation.NetStandard.Opc.Ua (1.5.374.126)
- OPCFoundation.NetStandard.Opc.Ua.Client
- CommunityToolkit.Mvvm (8.2.2)
- Newtonsoft.Json (13.0.3)
- Serilog + Serilog.Sinks.File/Console
- Microsoft.Extensions.DependencyInjection (8.0.0)

### 3.3 Build Project

```bash
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 3.4 Chạy Application

```bash
dotnet run
```

---

## 🖥️ BƯỚC 4: HIỂU GIAO DIỆN

### 4.1 Layout chính

```
┌─────────────────────────────────────────────────────────────────┐
│  File  Edit  Connection  View  Help                    [Menu]   │
├─────────────────────────────────────────────────────────────────┤
│  📂 Load | 💾 Save | ➕ Add PLC | 🗑️ Delete | 🔌 Connect [Toolbar]│
├──────────────┬──────────────────────────────────────────────────┤
│              │                                                  │
│  PLC List    │              Tag DataGrid                        │
│              │                                                  │
│  ● PLC 1     │  Name | NodeId | Value | Quality | Timestamp... │
│  ○ PLC 2     │  ─────────────────────────────────────────────── │
│              │  Temp1 | ns=3;... | 25.5 | Good | 10:30:15      │
│              │  Press | ns=3;... | 3.2  | Good | 10:30:15      │
│              │                                                  │
├──────────────┴──────────────────────────────────────────────────┤
│                      Properties Panel                           │
│  Name: [PLC 1    ]  Endpoint: [opc.tcp://192.168.1.100:4840  ] │
│  Status: Connected  Auto Reconnect: [✓]                        │
├─────────────────────────────────────────────────────────────────┤
│  Ready | PLCs: 2 | ●                                 [StatusBar]│
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Các thành phần

| Vùng | Chức năng |
|------|-----------|
| **Menu** | File operations, Edit PLC/Tags, Connection control |
| **Toolbar** | Quick access buttons |
| **PLC List** | Danh sách PLCs với status indicator |
| **Tag DataGrid** | Bảng hiển thị Tags của PLC đang chọn |
| **Properties** | Chi tiết và chỉnh sửa PLC đang chọn |
| **StatusBar** | Thông báo và thống kê |

---

## ⚙️ BƯỚC 5: CẤU HÌNH PLC

### 5.1 Thêm PLC mới qua UI

1. Click **➕ Add PLC** hoặc **Ctrl+N**
2. PLC mới xuất hiện trong danh sách
3. Chỉnh sửa trong Properties Panel:

| Field | Giá trị mẫu | Mô tả |
|-------|-------------|-------|
| Name | `Siemens S7-1500 Line 1` | Tên hiển thị |
| Endpoint | `opc.tcp://192.168.1.100:4840` | OPC UA URL |
| Session Timeout | `60000` | Timeout (ms) |
| KeepAlive | `5000` | Keep alive interval (ms) |
| Auto Reconnect | `✓` | Tự động kết nối lại |
| Enabled | `✓` | Cho phép kết nối |

4. **Ctrl+S** để lưu

### 5.2 Chỉnh sửa file JSON trực tiếp

Mở `Configurations/plc_config.json`:

```json
{
  "PlcDevices": [
    {
      "Id": "plc-001",
      "Name": "Siemens S7-1500 Line 1",
      "Description": "PLC điều khiển dây chuyền 1",
      "EndpointUrl": "opc.tcp://192.168.1.100:4840",
      "IsEnabled": true,
      "SecurityPolicy": 0,
      "SecurityMode": 1,
      "UserName": "",
      "Password": "",
      "SessionTimeout": 60000,
      "KeepAliveInterval": 5000,
      "AutoReconnect": true,
      "ReconnectInterval": 5000,
      "MaxReconnectAttempts": 10,
      "Tags": [],
      "SubscriptionGroups": []
    }
  ]
}
```

### 5.3 Security Options

**SecurityPolicy:**
```
0 = None           → Không mã hóa (dùng cho test)
3 = Basic256Sha256 → Khuyên dùng cho production
```

**SecurityMode:**
```
1 = None           → Không ký, không mã hóa
2 = Sign           → Chỉ ký điện tử
3 = SignAndEncrypt → Ký và mã hóa (an toàn nhất)
```

---

## 🏷️ BƯỚC 6: CẤU HÌNH TAGS

### 6.1 Thêm Tag qua UI

1. Chọn PLC trong danh sách
2. Click **➕ Add Tag**
3. Chỉnh sửa trong DataGrid:

| Column | Giá trị mẫu | Mô tả |
|--------|-------------|-------|
| Name | `Temperature_Sensor1` | Tên Tag |
| NodeId | `ns=3;s="DB1"."Temp"` | OPC UA Node ID |
| Data Type | `Float` | Kiểu dữ liệu |
| Access | `Read` | Read/Write/ReadWrite |
| Scan Rate | `500` | Tốc độ quét (ms) |
| Enabled | `✓` | Bật/tắt Tag |

### 6.2 Định dạng NodeId theo loại PLC

#### Siemens S7-1500/1200:
```
# Data Block variable
ns=3;s="DB1"."Temperature"
ns=3;s="DB_Production"."Line1"."Speed"

# Memory area
ns=3;s="MW100"        # Memory Word
ns=3;s="M10.0"        # Memory Bit
```

#### Beckhoff TwinCAT:
```
ns=4;s=MAIN.Temperature
ns=4;s=GVL.ProcessData.Pressure
```

#### Generic OPC UA:
```
# Numeric NodeId
ns=2;i=1001

# String NodeId  
ns=2;s=MyVariable

# GUID NodeId
ns=2;g=12345678-1234-1234-1234-123456789012
```

### 6.3 Cấu hình Tag trong JSON

```json
{
  "Tags": [
    {
      "Id": "tag-001",
      "PlcId": "plc-001",
      "Name": "Temperature_Sensor1",
      "Description": "Nhiệt độ cảm biến 1",
      "NodeId": "ns=3;s=\"DB1\".\"Temperature1\"",
      "DataType": 9,
      "AccessMode": 0,
      "IsEnabled": true,
      "ScanRate": 100,
      "Deadband": 0.5,
      "EngineeringUnit": "°C",
      "MinValue": -40.0,
      "MaxValue": 200.0
    }
  ]
}
```

### 6.4 Data Type Reference

| Code | Type | .NET Type | Ví dụ |
|------|------|-----------|-------|
| 0 | Boolean | bool | true/false |
| 3 | Int16 | short | -32768 to 32767 |
| 4 | UInt16 | ushort | 0 to 65535 |
| 5 | Int32 | int | ±2 tỷ |
| 9 | Float | float | 3.14 |
| 10 | Double | double | 3.14159265359 |
| 11 | String | string | "Hello" |

---

## 🔌 BƯỚC 7: KẾT NỐI PLC THẬT

### 7.1 Chuẩn bị PLC

#### Siemens S7-1500 (TIA Portal):

1. Mở project trong TIA Portal
2. Vào **Device Configuration** → **OPC UA**
3. **Enable OPC UA Server**:
   - ✓ Activate OPC UA Server
   - Port: 4840 (default)
   - Security: Chọn phù hợp

4. **Download** config xuống PLC

#### Kiểm tra kết nối:
```bash
# Ping PLC
ping 192.168.1.100

# Telnet port 4840
telnet 192.168.1.100 4840
```

### 7.2 Tìm NodeId bằng OPC UA Browser

**Công cụ miễn phí:**
- **UaExpert** (Unified Automation) - https://www.unified-automation.com/
- **Prosys OPC UA Browser** - https://www.prosysopc.com/

**Các bước:**
1. Mở UaExpert
2. Add Server → `opc.tcp://192.168.1.100:4840`
3. Connect
4. Browse Address Space
5. Tìm variable cần đọc
6. Copy NodeId (Attributes panel)

### 7.3 Test với Prosys Simulation Server

Nếu chưa có PLC thật, dùng simulation:

1. Tải **Prosys OPC UA Simulation Server**
2. Cài đặt và chạy
3. Endpoint: `opc.tcp://localhost:53530/OPCUA/SimulationServer`
4. Browse để tìm sample variables

---

## 📊 BƯỚC 8: KIỂM TRA VÀ DEBUG

### 8.1 Xem Log Files

```bash
# Log files trong folder Logs/
cat Logs/app-2024-01-15.log
```

**Log format:**
```
2024-01-15 10:30:15.123 [INF] Application starting...
2024-01-15 10:30:15.456 [INF] Configuration loaded with 2 PLCs
2024-01-15 10:30:20.789 [ERR] Failed to connect: Connection refused
```

### 8.2 Debug trong Visual Studio

1. Mở solution trong VS 2022
2. Set breakpoint trong `MainViewModel.cs`
3. F5 để debug
4. Theo dõi:
   - `PlcDevices` collection
   - `SelectedPlc` properties
   - `SelectedPlcTags` collection

### 8.3 Common Issues

| Vấn đề | Nguyên nhân | Giải pháp |
|--------|-------------|-----------|
| Build failed | Missing SDK | Cài .NET 8 SDK |
| NuGet error | Network issue | `dotnet nuget locals all --clear` |
| UI not updating | Binding error | Check XAML bindings |
| Config not saving | Permission | Run as Admin |

---

## ✅ CHECKLIST HOÀN THÀNH PHASE 1

- [ ] Cài đặt .NET 8 SDK
- [ ] Build project thành công
- [ ] Chạy application được
- [ ] Thêm được PLC mới
- [ ] Chỉnh sửa PLC properties
- [ ] Thêm được Tags
- [ ] Save configuration thành công
- [ ] Load configuration thành công
- [ ] Hiểu cấu trúc code

---

## 🚀 TIẾP THEO: PHASE 2

Phase 2 sẽ implement:
- OPC UA Session connection
- Subscription management
- Real-time data reading
- Auto-reconnect logic

**Bạn đã sẵn sàng cho Phase 2 chưa?**
