# Huong Dan Trien Khai Modbus TCP/IP

## Tong Quan

Tai lieu nay huong dan cach trien khai Modbus TCP/IP de dong bo voi ung dung Android da doc du lieu tu OPC UA API.

### Anh Xa Du Lieu

| Tag Name | Modbus Address | Mieu Ta |
|----------|----------------|---------|
| bool1-bool15 | HR0 (bit 0-14) | 15 bits trong Holding Register 0 |
| int1-int28 | HR1-HR28 | 28 Holding Registers |

---

## 1. Cau Hinh Ben PLC (Modbus Server)

### 1.1 Bang Anh Xa Holding Registers

```
+------------------+-------------------+------------------------+
| Modbus Address   | Variable          | Kieu Du Lieu           |
+------------------+-------------------+------------------------+
| HR0 (40001)      | BoolRegister      | UINT16 (chua 15 bits)  |
|   - Bit 0        | bool1             | BOOL                   |
|   - Bit 1        | bool2             | BOOL                   |
|   - Bit 2        | bool3             | BOOL                   |
|   - Bit 3        | bool4             | BOOL                   |
|   - Bit 4        | bool5             | BOOL                   |
|   - Bit 5        | bool6             | BOOL                   |
|   - Bit 6        | bool7             | BOOL                   |
|   - Bit 7        | bool8             | BOOL                   |
|   - Bit 8        | bool9             | BOOL                   |
|   - Bit 9        | bool10            | BOOL                   |
|   - Bit 10       | bool11            | BOOL                   |
|   - Bit 11       | bool12            | BOOL                   |
|   - Bit 12       | bool13            | BOOL                   |
|   - Bit 13       | bool14            | BOOL                   |
|   - Bit 14       | bool15            | BOOL                   |
+------------------+-------------------+------------------------+
| HR1 (40002)      | int1              | INT16                  |
| HR2 (40003)      | int2              | INT16                  |
| HR3 (40004)      | int3              | INT16                  |
| HR4 (40005)      | int4              | INT16                  |
| HR5 (40006)      | int5              | INT16                  |
| HR6 (40007)      | int6              | INT16                  |
| HR7 (40008)      | int7              | INT16                  |
| HR8 (40009)      | int8              | INT16                  |
| HR9 (40010)      | int9              | INT16                  |
| HR10 (40011)     | int10             | INT16                  |
| HR11 (40012)     | int11             | INT16                  |
| HR12 (40013)     | int12             | INT16                  |
| HR13 (40014)     | int13             | INT16                  |
| HR14 (40015)     | int14             | INT16                  |
| HR15 (40016)     | int15             | INT16                  |
| HR16 (40017)     | int16             | INT16                  |
| HR17 (40018)     | int17             | INT16                  |
| HR18 (40019)     | int18             | INT16                  |
| HR19 (40020)     | int19             | INT16                  |
| HR20 (40021)     | int20             | INT16                  |
| HR21 (40022)     | int21             | INT16                  |
| HR22 (40023)     | int22             | INT16                  |
| HR23 (40024)     | int23             | INT16                  |
| HR24 (40025)     | int24             | INT16                  |
| HR25 (40026)     | int25             | INT16                  |
| HR26 (40027)     | int26             | INT16                  |
| HR27 (40028)     | int27             | INT16                  |
| HR28 (40029)     | int28             | INT16                  |
+------------------+-------------------+------------------------+
```

### 1.2 Chuong Trinh PLC (Ladder Logic / Structured Text)

#### Structured Text (ST) - Vi du cho Siemens, Beckhoff, etc.

```pascal
// Khai bao bien
VAR
    // Holding Register 0 - Chua 15 Boolean bits
    BoolRegister : UINT := 0;
    
    // Cac bien Boolean rieng le
    bool1, bool2, bool3, bool4, bool5 : BOOL;
    bool6, bool7, bool8, bool9, bool10 : BOOL;
    bool11, bool12, bool13, bool14, bool15 : BOOL;
    
    // Holding Registers 1-28 - Cac gia tri Integer
    int1, int2, int3, int4, int5 : INT;
    int6, int7, int8, int9, int10 : INT;
    int11, int12, int13, int14, int15 : INT;
    int16, int17, int18, int19, int20 : INT;
    int21, int22, int23, int24, int25 : INT;
    int26, int27, int28 : INT;
END_VAR

// Logic de dong bo Boolean vao BoolRegister
// Doc tu Boolean -> Pack vao Register
BoolRegister := 0;
IF bool1 THEN BoolRegister := BoolRegister OR 16#0001; END_IF;
IF bool2 THEN BoolRegister := BoolRegister OR 16#0002; END_IF;
IF bool3 THEN BoolRegister := BoolRegister OR 16#0004; END_IF;
IF bool4 THEN BoolRegister := BoolRegister OR 16#0008; END_IF;
IF bool5 THEN BoolRegister := BoolRegister OR 16#0010; END_IF;
IF bool6 THEN BoolRegister := BoolRegister OR 16#0020; END_IF;
IF bool7 THEN BoolRegister := BoolRegister OR 16#0040; END_IF;
IF bool8 THEN BoolRegister := BoolRegister OR 16#0080; END_IF;
IF bool9 THEN BoolRegister := BoolRegister OR 16#0100; END_IF;
IF bool10 THEN BoolRegister := BoolRegister OR 16#0200; END_IF;
IF bool11 THEN BoolRegister := BoolRegister OR 16#0400; END_IF;
IF bool12 THEN BoolRegister := BoolRegister OR 16#0800; END_IF;
IF bool13 THEN BoolRegister := BoolRegister OR 16#1000; END_IF;
IF bool14 THEN BoolRegister := BoolRegister OR 16#2000; END_IF;
IF bool15 THEN BoolRegister := BoolRegister OR 16#4000; END_IF;

// Unpack tu Register -> Doc thanh Boolean (khi nhan tu Modbus)
bool1 := (BoolRegister AND 16#0001) <> 0;
bool2 := (BoolRegister AND 16#0002) <> 0;
bool3 := (BoolRegister AND 16#0004) <> 0;
bool4 := (BoolRegister AND 16#0008) <> 0;
bool5 := (BoolRegister AND 16#0010) <> 0;
bool6 := (BoolRegister AND 16#0020) <> 0;
bool7 := (BoolRegister AND 16#0040) <> 0;
bool8 := (BoolRegister AND 16#0080) <> 0;
bool9 := (BoolRegister AND 16#0100) <> 0;
bool10 := (BoolRegister AND 16#0200) <> 0;
bool11 := (BoolRegister AND 16#0400) <> 0;
bool12 := (BoolRegister AND 16#0800) <> 0;
bool13 := (BoolRegister AND 16#1000) <> 0;
bool14 := (BoolRegister AND 16#2000) <> 0;
bool15 := (BoolRegister AND 16#4000) <> 0;
```

#### Ladder Logic - Vi du cho Allen-Bradley, Mitsubishi

```
// Rung 1: Pack bool1-bool8 vao byte thap cua BoolRegister
// Rung 2: Pack bool9-bool15 vao byte cao cua BoolRegister
// Su dung lenh MOV, OTE, XIC de thao tac bit
```

---

## 2. Cau Hinh Ben Server (OPC UA Communication Engine)

### 2.1 Tao PLC Device voi Modbus TCP

Su dung file cau hinh `modbus_plc_config_sample.json` hoac cau hinh qua giao dien:

```json
{
  "Id": "modbus-plc-001",
  "Name": "Modbus PLC Controller",
  "ProtocolType": 3,  // 3 = ModbusTcp
  "IpAddress": "192.168.1.10",
  "Port": 502,
  "IsEnabled": true,
  "ConnectionTimeout": 5000,
  "ReadTimeout": 3000,
  "WriteTimeout": 3000,
  "AutoReconnect": true
}
```

### 2.2 Dinh Nghia Tags

#### Format dia chi Modbus:

| Format | Mieu ta | Vi du |
|--------|---------|-------|
| HR{n} | Holding Register n | HR0, HR1, HR100 |
| HR{n}.{bit} | Bit trong Holding Register | HR0.0, HR0.14 |
| IR{n} | Input Register n | IR0, IR50 |
| C{n} | Coil n | C0, C100 |
| DI{n} | Discrete Input n | DI0, DI50 |

#### Vi du Tags:

```json
{
  "Tags": [
    // Boolean bits trong HR0
    {"Name": "bool1", "NodeId": "HR0.0", "DataType": 0},
    {"Name": "bool2", "NodeId": "HR0.1", "DataType": 0},
    // ...
    {"Name": "bool15", "NodeId": "HR0.14", "DataType": 0},
    
    // Integer values trong HR1-HR28
    {"Name": "int1", "NodeId": "HR1", "DataType": 3},
    {"Name": "int2", "NodeId": "HR2", "DataType": 3},
    // ...
    {"Name": "int28", "NodeId": "HR28", "DataType": 3}
  ]
}
```

---

## 3. Dong Bo Voi Ung Dung Android

### 3.1 API Endpoints

Ung dung Android se su dung cac API sau:

#### Doc tat ca Tags:
```
GET /api/tags
Authorization: Bearer {token}
```

#### Doc Tags theo PLC:
```
GET /api/tags/plc/{plcId}
Authorization: Bearer {token}
```

#### Doc mot Tag cu the:
```
GET /api/tags/{plcId}/{nodeId}/read
Authorization: Bearer {token}

Vi du: GET /api/tags/modbus-plc-001/HR0.0/read
```

#### Ghi gia tri Tag:
```
POST /api/tags/{plcId}/{nodeId}/write
Authorization: Bearer {token}
Content-Type: application/json

{
  "Value": true  // hoac gia tri int cho integer
}
```

### 3.2 Response Format

```json
{
  "success": true,
  "data": {
    "id": "tag-bool1",
    "name": "bool1",
    "nodeId": "HR0.0",
    "plcId": "modbus-plc-001",
    "plcName": "Modbus PLC Controller",
    "value": true,
    "quality": "Good",
    "timestamp": "2026-04-10T10:30:00Z",
    "dataType": "Boolean",
    "isWritable": true
  },
  "error": null
}
```

### 3.3 SignalR Real-time Updates

Ket noi SignalR Hub de nhan cap nhat real-time:

```javascript
// Android Kotlin code
val connection = HubConnectionBuilder
    .create("http://server-ip:port/plchub")
    .withAccessTokenProvider { token }
    .build()

connection.on("TagValueChanged", { plcId, tagId, value, quality, timestamp ->
    // Cap nhat UI
}, String::class.java, String::class.java, Any::class.java, String::class.java, String::class.java)

connection.start()
```

---

## 4. Vi Du Trien Khai Hoan Chinh

### 4.1 Kich Ban: Dieu Khien 15 Relay va Doc 28 Cam Bien

| bool1-bool15 | Dieu khien 15 relay (ON/OFF) |
| int1-int10 | Doc gia tri 10 cam bien nhiet do |
| int11-int20 | Doc gia tri 10 cam bien ap suat |
| int21-int28 | Doc gia tri 8 cam bien khac |

### 4.2 Code Mau Android (Kotlin)

```kotlin
// Data class
data class TagValue(
    val id: String,
    val name: String,
    val nodeId: String,
    val value: Any?,
    val quality: String,
    val timestamp: String
)

// API Service
interface PlcApiService {
    @GET("api/tags/plc/{plcId}")
    suspend fun getTags(@Path("plcId") plcId: String): Response<ApiResponse<List<TagValue>>>
    
    @POST("api/tags/{plcId}/{nodeId}/write")
    suspend fun writeTag(
        @Path("plcId") plcId: String,
        @Path("nodeId") nodeId: String,
        @Body request: WriteTagRequest
    ): Response<ApiResponse<Boolean>>
}

// Toggle Boolean (Relay)
suspend fun toggleRelay(relayNumber: Int, state: Boolean) {
    val nodeId = "HR0.${relayNumber - 1}"  // bool1 = HR0.0
    apiService.writeTag("modbus-plc-001", nodeId, WriteTagRequest(state))
}

// Set Integer value
suspend fun setIntValue(intNumber: Int, value: Int) {
    val nodeId = "HR$intNumber"  // int1 = HR1
    apiService.writeTag("modbus-plc-001", nodeId, WriteTagRequest(value))
}
```

---

## 5. Troubleshooting

### 5.1 Loi Thuong Gap

| Loi | Nguyen Nhan | Giai Phap |
|-----|-------------|-----------|
| Connection timeout | IP/Port sai | Kiem tra IP va Port 502 |
| Illegal Data Address | Dia chi khong ton tai | Kiem tra dia chi Modbus tren PLC |
| Slave Device Failure | PLC loi | Kiem tra trang thai PLC |
| Gateway Path Unavailable | Khong tim thay device | Kiem tra Unit ID |

### 5.2 Kiem Tra Ket Noi

Su dung Modbus Poll hoac tool tuong tu de test:
1. Ket noi den IP:502
2. Doc Holding Registers 0-28
3. Xac nhan gia tri dung

---

## 6. Bao Mat

### 6.1 Khyen Nghi

1. **Su dung VPN** khi truy cap tu ben ngoai mang LAN
2. **Firewall** chi mo port 502 cho IP can thiet
3. **JWT Token** cho API authentication
4. **HTTPS** cho ket noi API
5. **Operator Lock** de tranh xung dot khi nhieu nguoi dieu khien

### 6.2 Cau Hinh Authentication

```json
{
  "AuthSettings": {
    "JwtSecret": "your-secure-secret-key-min-32-chars",
    "TokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

---

## Ket Luan

Voi cau hinh tren, ban co the:
1. Doc/ghi 15 Boolean thong qua HR0 (bit access)
2. Doc/ghi 28 Integer thong qua HR1-HR28
3. Dong bo real-time voi ung dung Android qua API/SignalR
4. Bao mat thong qua JWT authentication

Lien he ho tro neu can them thong tin.
