# Huong Dan Cau Hinh Siemens S7-1500 Modbus TCP Server

## Tong Quan

Huong dan nay chi tiet cach cau hinh S7-1500 lam Modbus TCP Server de:
- bool1-bool15: Luu trong Holding Register 0 (bit 0-14)
- int1-int28: Luu trong Holding Registers 1-28

---

## PHAN 1: CAU HINH HARDWARE TRONG TIA PORTAL

### Buoc 1.1: Mo Project TIA Portal

1. Mo TIA Portal V15/V16/V17/V18
2. Tao project moi hoac mo project hien co
3. Chon PLC S7-1500 cua ban

### Buoc 1.2: Cau Hinh Ethernet Interface

1. Vao **Device configuration** > Click vao PLC
2. Chon **PROFINET interface [X1]**
3. Tab **Ethernet addresses**:
   ```
   IP address:     192.168.1.100  (hoac IP cua ban)
   Subnet mask:    255.255.255.0
   ```

4. Tab **Advanced options** > **Real time settings**:
   - IO controller: Checked
   - IO device: Unchecked (neu khong dung)

### Buoc 1.3: Bat Modbus TCP Server

**QUAN TRONG: S7-1500 can them module Communication Board hoac dung instruction MB_SERVER**

1. Vao **Device view**
2. Click phai vao PLC > **Properties**
3. Tim **Protection & Security** > **Connection mechanisms**:
   - Check: **Permit access with PUT/GET communication**
   - Check: **Permit access with OPC UA**

---

## PHAN 2: TAO CAC BLOCK TRONG TIA PORTAL

### Buoc 2.1: Tao Data Block (DB100)

1. Vao **Program blocks** > Click phai > **Add new block**
2. Chon **Data block**
3. Ten: `ModbusData`
4. Number: `100`
5. **QUAN TRONG**: Bo check **Optimized block access**

6. Them cac bien nhu sau:

| Name | Data Type | Start Value | Comment |
|------|-----------|-------------|---------|
| BoolRegister | Word | 0 | HR0 - Chua 15 bits |
| bool1 | Bool | FALSE | Bit 0 |
| bool2 | Bool | FALSE | Bit 1 |
| bool3 | Bool | FALSE | Bit 2 |
| bool4 | Bool | FALSE | Bit 3 |
| bool5 | Bool | FALSE | Bit 4 |
| bool6 | Bool | FALSE | Bit 5 |
| bool7 | Bool | FALSE | Bit 6 |
| bool8 | Bool | FALSE | Bit 7 |
| bool9 | Bool | FALSE | Bit 8 |
| bool10 | Bool | FALSE | Bit 9 |
| bool11 | Bool | FALSE | Bit 10 |
| bool12 | Bool | FALSE | Bit 11 |
| bool13 | Bool | FALSE | Bit 12 |
| bool14 | Bool | FALSE | Bit 13 |
| bool15 | Bool | FALSE | Bit 14 |
| int1 | Int | 0 | HR1 |
| int2 | Int | 0 | HR2 |
| ... | Int | 0 | ... |
| int28 | Int | 0 | HR28 |
| MB_HOLD_REG | Array[0..28] of Word | - | Modbus Registers |
| ModbusConnected | Bool | FALSE | Trang thai ket noi |
| LastError | Word | 0 | Ma loi |

### Buoc 2.2: Tao Function Block FB_ModbusServer (FB100)

1. **Add new block** > **Function block**
2. Ten: `FB_ModbusServer`
3. Language: **SCL**
4. Copy code sau:

```scl
FUNCTION_BLOCK "FB_ModbusServer"
{ S7_Optimized_Access := 'FALSE' }
VERSION : 0.1

VAR_INPUT
    Enable : Bool := TRUE;
END_VAR

VAR_OUTPUT
    Connected : Bool;
    Error : Bool;
    Status : Word;
END_VAR

VAR
    MB_SERVER_Instance : MB_SERVER;
    ConnID : Word := 1;
END_VAR

BEGIN
    #MB_SERVER_Instance(
        DISCONNECT := NOT #Enable,
        CONNECT_ID := #ConnID,
        IP_PORT := 502,
        MB_HOLD_REG := "ModbusData".MB_HOLD_REG,
        NDR => ,
        DR => ,
        ERROR => #Error,
        STATUS => #Status
    );
    
    #Connected := #Enable AND NOT #Error;
    
END_FUNCTION_BLOCK
```

### Buoc 2.3: Tao Function FC_SyncBoolRegister (FC100)

1. **Add new block** > **Function**
2. Ten: `FC_SyncBoolRegister`
3. Language: **SCL**
4. Copy code sau:

```scl
FUNCTION "FC_SyncBoolRegister" : Void
{ S7_Optimized_Access := 'FALSE' }
VERSION : 0.1

VAR_INPUT
    Direction : Int;  // 0 = Bool->Reg, 1 = Reg->Bool
END_VAR

VAR_TEMP
    tempWord : Word;
END_VAR

BEGIN
    IF #Direction = 0 THEN
        // PACK: Bool -> Register
        #tempWord := 0;
        
        IF "ModbusData".bool1 THEN #tempWord := #tempWord OR 16#0001; END_IF;
        IF "ModbusData".bool2 THEN #tempWord := #tempWord OR 16#0002; END_IF;
        IF "ModbusData".bool3 THEN #tempWord := #tempWord OR 16#0004; END_IF;
        IF "ModbusData".bool4 THEN #tempWord := #tempWord OR 16#0008; END_IF;
        IF "ModbusData".bool5 THEN #tempWord := #tempWord OR 16#0010; END_IF;
        IF "ModbusData".bool6 THEN #tempWord := #tempWord OR 16#0020; END_IF;
        IF "ModbusData".bool7 THEN #tempWord := #tempWord OR 16#0040; END_IF;
        IF "ModbusData".bool8 THEN #tempWord := #tempWord OR 16#0080; END_IF;
        IF "ModbusData".bool9 THEN #tempWord := #tempWord OR 16#0100; END_IF;
        IF "ModbusData".bool10 THEN #tempWord := #tempWord OR 16#0200; END_IF;
        IF "ModbusData".bool11 THEN #tempWord := #tempWord OR 16#0400; END_IF;
        IF "ModbusData".bool12 THEN #tempWord := #tempWord OR 16#0800; END_IF;
        IF "ModbusData".bool13 THEN #tempWord := #tempWord OR 16#1000; END_IF;
        IF "ModbusData".bool14 THEN #tempWord := #tempWord OR 16#2000; END_IF;
        IF "ModbusData".bool15 THEN #tempWord := #tempWord OR 16#4000; END_IF;
        
        "ModbusData".BoolRegister := #tempWord;
        "ModbusData".MB_HOLD_REG[0] := #tempWord;
        
    ELSE
        // UNPACK: Register -> Bool
        #tempWord := "ModbusData".MB_HOLD_REG[0];
        "ModbusData".BoolRegister := #tempWord;
        
        "ModbusData".bool1 := (#tempWord AND 16#0001) <> 0;
        "ModbusData".bool2 := (#tempWord AND 16#0002) <> 0;
        "ModbusData".bool3 := (#tempWord AND 16#0004) <> 0;
        "ModbusData".bool4 := (#tempWord AND 16#0008) <> 0;
        "ModbusData".bool5 := (#tempWord AND 16#0010) <> 0;
        "ModbusData".bool6 := (#tempWord AND 16#0020) <> 0;
        "ModbusData".bool7 := (#tempWord AND 16#0040) <> 0;
        "ModbusData".bool8 := (#tempWord AND 16#0080) <> 0;
        "ModbusData".bool9 := (#tempWord AND 16#0100) <> 0;
        "ModbusData".bool10 := (#tempWord AND 16#0200) <> 0;
        "ModbusData".bool11 := (#tempWord AND 16#0400) <> 0;
        "ModbusData".bool12 := (#tempWord AND 16#0800) <> 0;
        "ModbusData".bool13 := (#tempWord AND 16#1000) <> 0;
        "ModbusData".bool14 := (#tempWord AND 16#2000) <> 0;
        "ModbusData".bool15 := (#tempWord AND 16#4000) <> 0;
    END_IF;
    
END_FUNCTION
```

### Buoc 2.4: Tao Function FC_SyncIntRegisters (FC101)

```scl
FUNCTION "FC_SyncIntRegisters" : Void
{ S7_Optimized_Access := 'FALSE' }
VERSION : 0.1

VAR_INPUT
    Direction : Int;  // 0 = Int->Reg, 1 = Reg->Int
END_VAR

BEGIN
    IF #Direction = 0 THEN
        // INT -> REGISTER
        "ModbusData".MB_HOLD_REG[1] := INT_TO_WORD("ModbusData".int1);
        "ModbusData".MB_HOLD_REG[2] := INT_TO_WORD("ModbusData".int2);
        "ModbusData".MB_HOLD_REG[3] := INT_TO_WORD("ModbusData".int3);
        "ModbusData".MB_HOLD_REG[4] := INT_TO_WORD("ModbusData".int4);
        "ModbusData".MB_HOLD_REG[5] := INT_TO_WORD("ModbusData".int5);
        "ModbusData".MB_HOLD_REG[6] := INT_TO_WORD("ModbusData".int6);
        "ModbusData".MB_HOLD_REG[7] := INT_TO_WORD("ModbusData".int7);
        "ModbusData".MB_HOLD_REG[8] := INT_TO_WORD("ModbusData".int8);
        "ModbusData".MB_HOLD_REG[9] := INT_TO_WORD("ModbusData".int9);
        "ModbusData".MB_HOLD_REG[10] := INT_TO_WORD("ModbusData".int10);
        "ModbusData".MB_HOLD_REG[11] := INT_TO_WORD("ModbusData".int11);
        "ModbusData".MB_HOLD_REG[12] := INT_TO_WORD("ModbusData".int12);
        "ModbusData".MB_HOLD_REG[13] := INT_TO_WORD("ModbusData".int13);
        "ModbusData".MB_HOLD_REG[14] := INT_TO_WORD("ModbusData".int14);
        "ModbusData".MB_HOLD_REG[15] := INT_TO_WORD("ModbusData".int15);
        "ModbusData".MB_HOLD_REG[16] := INT_TO_WORD("ModbusData".int16);
        "ModbusData".MB_HOLD_REG[17] := INT_TO_WORD("ModbusData".int17);
        "ModbusData".MB_HOLD_REG[18] := INT_TO_WORD("ModbusData".int18);
        "ModbusData".MB_HOLD_REG[19] := INT_TO_WORD("ModbusData".int19);
        "ModbusData".MB_HOLD_REG[20] := INT_TO_WORD("ModbusData".int20);
        "ModbusData".MB_HOLD_REG[21] := INT_TO_WORD("ModbusData".int21);
        "ModbusData".MB_HOLD_REG[22] := INT_TO_WORD("ModbusData".int22);
        "ModbusData".MB_HOLD_REG[23] := INT_TO_WORD("ModbusData".int23);
        "ModbusData".MB_HOLD_REG[24] := INT_TO_WORD("ModbusData".int24);
        "ModbusData".MB_HOLD_REG[25] := INT_TO_WORD("ModbusData".int25);
        "ModbusData".MB_HOLD_REG[26] := INT_TO_WORD("ModbusData".int26);
        "ModbusData".MB_HOLD_REG[27] := INT_TO_WORD("ModbusData".int27);
        "ModbusData".MB_HOLD_REG[28] := INT_TO_WORD("ModbusData".int28);
    ELSE
        // REGISTER -> INT
        "ModbusData".int1 := WORD_TO_INT("ModbusData".MB_HOLD_REG[1]);
        "ModbusData".int2 := WORD_TO_INT("ModbusData".MB_HOLD_REG[2]);
        "ModbusData".int3 := WORD_TO_INT("ModbusData".MB_HOLD_REG[3]);
        "ModbusData".int4 := WORD_TO_INT("ModbusData".MB_HOLD_REG[4]);
        "ModbusData".int5 := WORD_TO_INT("ModbusData".MB_HOLD_REG[5]);
        "ModbusData".int6 := WORD_TO_INT("ModbusData".MB_HOLD_REG[6]);
        "ModbusData".int7 := WORD_TO_INT("ModbusData".MB_HOLD_REG[7]);
        "ModbusData".int8 := WORD_TO_INT("ModbusData".MB_HOLD_REG[8]);
        "ModbusData".int9 := WORD_TO_INT("ModbusData".MB_HOLD_REG[9]);
        "ModbusData".int10 := WORD_TO_INT("ModbusData".MB_HOLD_REG[10]);
        "ModbusData".int11 := WORD_TO_INT("ModbusData".MB_HOLD_REG[11]);
        "ModbusData".int12 := WORD_TO_INT("ModbusData".MB_HOLD_REG[12]);
        "ModbusData".int13 := WORD_TO_INT("ModbusData".MB_HOLD_REG[13]);
        "ModbusData".int14 := WORD_TO_INT("ModbusData".MB_HOLD_REG[14]);
        "ModbusData".int15 := WORD_TO_INT("ModbusData".MB_HOLD_REG[15]);
        "ModbusData".int16 := WORD_TO_INT("ModbusData".MB_HOLD_REG[16]);
        "ModbusData".int17 := WORD_TO_INT("ModbusData".MB_HOLD_REG[17]);
        "ModbusData".int18 := WORD_TO_INT("ModbusData".MB_HOLD_REG[18]);
        "ModbusData".int19 := WORD_TO_INT("ModbusData".MB_HOLD_REG[19]);
        "ModbusData".int20 := WORD_TO_INT("ModbusData".MB_HOLD_REG[20]);
        "ModbusData".int21 := WORD_TO_INT("ModbusData".MB_HOLD_REG[21]);
        "ModbusData".int22 := WORD_TO_INT("ModbusData".MB_HOLD_REG[22]);
        "ModbusData".int23 := WORD_TO_INT("ModbusData".MB_HOLD_REG[23]);
        "ModbusData".int24 := WORD_TO_INT("ModbusData".MB_HOLD_REG[24]);
        "ModbusData".int25 := WORD_TO_INT("ModbusData".MB_HOLD_REG[25]);
        "ModbusData".int26 := WORD_TO_INT("ModbusData".MB_HOLD_REG[26]);
        "ModbusData".int27 := WORD_TO_INT("ModbusData".MB_HOLD_REG[27]);
        "ModbusData".int28 := WORD_TO_INT("ModbusData".MB_HOLD_REG[28]);
    END_IF;
    
END_FUNCTION
```

### Buoc 2.5: Cau Hinh OB1 (Main)

1. Mo **OB1** (Main)
2. Trong Network 1, goi FB_ModbusServer:

**LAD/FBD:**
```
Network 1: Modbus Server
+----[FB_ModbusServer_DB]----+
|  EN         ENO           |
|  Enable-----Connected      |
|  TRUE       Error          |
|             Status         |
+---------------------------+
```

**SCL:**
```scl
// Network 1: Modbus Server
"FB_ModbusServer_DB"(
    Enable := TRUE,
    Connected => "ModbusData".ModbusConnected,
    Error => ,
    Status => "ModbusData".LastError
);

// Network 2: Sync Boolean (Client ghi -> PLC doc)
"FC_SyncBoolRegister"(Direction := 1);

// Network 3: Sync Integer (Client ghi -> PLC doc)
"FC_SyncIntRegisters"(Direction := 1);

// Network 4: Logic dieu khien cua ban
// Vi du: bool1 dieu khien relay Q0.0
%Q0.0 := "ModbusData".bool1;
%Q0.1 := "ModbusData".bool2;
// ...
```

---

## PHAN 3: COMPILE VA DOWNLOAD

### Buoc 3.1: Compile

1. Click phai vao PLC > **Compile** > **Hardware and software (only changes)**
2. Kiem tra khong co loi (0 errors)

### Buoc 3.2: Download

1. Ket noi PLC qua Ethernet
2. Click **Download to device**
3. Chon interface (vd: Realtek PCIe GBE)
4. Scan va chon PLC
5. Download

### Buoc 3.3: Go Online

1. Click **Go online**
2. Kiem tra PLC o che do RUN
3. Monitor "ModbusData" de xem gia tri

---

## PHAN 4: CAU HINH TREN SERVER (OPC UA Communication Engine)

### Buoc 4.1: Sua File plc_config.json

Copy noi dung tu `modbus_plc_config_sample.json` hoac sua:

```json
{
  "PlcDevices": [
    {
      "Id": "s7-1500-modbus",
      "Name": "S7-1500 Modbus Server",
      "Description": "Siemens S7-1500 with Modbus TCP",
      "ProtocolType": 3,
      "IpAddress": "192.168.1.100",
      "Port": 502,
      "IsEnabled": true,
      "ConnectionTimeout": 5000,
      "ReadTimeout": 3000,
      "WriteTimeout": 3000,
      "AutoReconnect": true,
      "Tags": [
        {"Id": "bool1", "Name": "bool1", "NodeId": "HR0.0", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool2", "Name": "bool2", "NodeId": "HR0.1", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool3", "Name": "bool3", "NodeId": "HR0.2", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool4", "Name": "bool4", "NodeId": "HR0.3", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool5", "Name": "bool5", "NodeId": "HR0.4", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool6", "Name": "bool6", "NodeId": "HR0.5", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool7", "Name": "bool7", "NodeId": "HR0.6", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool8", "Name": "bool8", "NodeId": "HR0.7", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool9", "Name": "bool9", "NodeId": "HR0.8", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool10", "Name": "bool10", "NodeId": "HR0.9", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool11", "Name": "bool11", "NodeId": "HR0.10", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool12", "Name": "bool12", "NodeId": "HR0.11", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool13", "Name": "bool13", "NodeId": "HR0.12", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool14", "Name": "bool14", "NodeId": "HR0.13", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "bool15", "Name": "bool15", "NodeId": "HR0.14", "DataType": 0, "AccessMode": 2, "ScanRate": 500},
        {"Id": "int1", "Name": "int1", "NodeId": "HR1", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int2", "Name": "int2", "NodeId": "HR2", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int3", "Name": "int3", "NodeId": "HR3", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int4", "Name": "int4", "NodeId": "HR4", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int5", "Name": "int5", "NodeId": "HR5", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int6", "Name": "int6", "NodeId": "HR6", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int7", "Name": "int7", "NodeId": "HR7", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int8", "Name": "int8", "NodeId": "HR8", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int9", "Name": "int9", "NodeId": "HR9", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int10", "Name": "int10", "NodeId": "HR10", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int11", "Name": "int11", "NodeId": "HR11", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int12", "Name": "int12", "NodeId": "HR12", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int13", "Name": "int13", "NodeId": "HR13", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int14", "Name": "int14", "NodeId": "HR14", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int15", "Name": "int15", "NodeId": "HR15", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int16", "Name": "int16", "NodeId": "HR16", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int17", "Name": "int17", "NodeId": "HR17", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int18", "Name": "int18", "NodeId": "HR18", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int19", "Name": "int19", "NodeId": "HR19", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int20", "Name": "int20", "NodeId": "HR20", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int21", "Name": "int21", "NodeId": "HR21", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int22", "Name": "int22", "NodeId": "HR22", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int23", "Name": "int23", "NodeId": "HR23", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int24", "Name": "int24", "NodeId": "HR24", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int25", "Name": "int25", "NodeId": "HR25", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int26", "Name": "int26", "NodeId": "HR26", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int27", "Name": "int27", "NodeId": "HR27", "DataType": 5, "AccessMode": 2, "ScanRate": 1000},
        {"Id": "int28", "Name": "int28", "NodeId": "HR28", "DataType": 5, "AccessMode": 2, "ScanRate": 1000}
      ]
    }
  ]
}
```

---

## PHAN 5: TEST KET NOI

### Buoc 5.1: Test voi Modbus Poll

1. Download **Modbus Poll** (free trial)
2. Connection > Connect > TCP/IP
3. IP: 192.168.1.100, Port: 502
4. Setup:
   - Slave ID: 1
   - Function: 03 Read Holding Registers
   - Address: 0
   - Quantity: 29

5. Kiem tra gia tri HR0-HR28

### Buoc 5.2: Test voi OPC UA Communication Engine

1. Chay ung dung
2. Kiem tra PLC ket noi thanh cong
3. Doc/ghi gia tri bool1-bool15, int1-int28

### Buoc 5.3: Test voi Android App

1. Mo app Android
2. Login
3. Doc tags
4. Ghi gia tri de dieu khien

---

## PHAN 6: TROUBLESHOOTING

### Loi: Khong ket noi duoc

1. Kiem tra IP va subnet cung dai
2. Ping PLC: `ping 192.168.1.100`
3. Kiem tra firewall mo port 502
4. Kiem tra PLC o che do RUN

### Loi: Illegal Data Address

1. Kiem tra DB100 co tat "Optimized block access"
2. Kiem tra mang MB_HOLD_REG[0..28] da khai bao
3. Download lai chuong trinh

### Loi: Gia tri khong dong bo

1. Kiem tra Direction trong FC_SyncBoolRegister/FC_SyncIntRegisters
2. Direction = 0: PLC ghi, Client doc
3. Direction = 1: Client ghi, PLC doc

---

## TOM TAT

```
+------------------------------------------+
|           S7-1500 PLC                    |
|                                          |
|  ModbusData (DB100)                      |
|  +------------------------------------+  |
|  | MB_HOLD_REG[0]  = BoolRegister     |  |
|  |   Bit 0-14 = bool1-bool15          |  |
|  | MB_HOLD_REG[1]  = int1             |  |
|  | MB_HOLD_REG[2]  = int2             |  |
|  | ...                                |  |
|  | MB_HOLD_REG[28] = int28            |  |
|  +------------------------------------+  |
|                 |                        |
|         Modbus TCP (Port 502)            |
+-----------------+------------------------+
                  |
                  v
+------------------------------------------+
|     OPC UA Communication Engine          |
|                                          |
|  ModbusTcpConnection                     |
|  - Read HR0.0-HR0.14 (bool1-bool15)      |
|  - Read/Write HR1-HR28 (int1-int28)      |
|                                          |
|         REST API + SignalR               |
+-----------------+------------------------+
                  |
                  v
+------------------------------------------+
|           Android App                    |
|                                          |
|  - Doc gia tri cam bien                  |
|  - Dieu khien relay/thiet bi             |
|  - Real-time updates qua SignalR         |
+------------------------------------------+
```

Chuc ban thanh cong!
