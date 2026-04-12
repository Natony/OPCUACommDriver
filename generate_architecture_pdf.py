#!/usr/bin/env python3
"""Generate System Architecture PDF for OPC UA Communication Engine using ReportLab"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor, white, black
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, Preformatted, KeepTogether
)
from reportlab.lib import colors

BLUE = HexColor('#0066CC')
DARK = HexColor('#1E1E1E')
GRAY = HexColor('#666666')
LIGHT_BG = HexColor('#F5F5FF')
CODE_BG = HexColor('#F0F0F0')

def get_styles():
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(
        'CoverTitle', parent=styles['Title'],
        fontSize=26, textColor=BLUE, spaceAfter=10, alignment=TA_CENTER
    ))
    styles.add(ParagraphStyle(
        'CoverSub', parent=styles['Normal'],
        fontSize=14, textColor=GRAY, alignment=TA_CENTER, spaceAfter=5
    ))
    styles.add(ParagraphStyle(
        'SectionTitle', parent=styles['Heading1'],
        fontSize=15, textColor=BLUE, spaceAfter=8, spaceBefore=16,
        borderWidth=1, borderColor=BLUE, borderPadding=3
    ))
    styles.add(ParagraphStyle(
        'SubTitle', parent=styles['Heading2'],
        fontSize=11, textColor=DARK, spaceAfter=5, spaceBefore=10
    ))
    styles.add(ParagraphStyle(
        'Body', parent=styles['Normal'],
        fontSize=9.5, textColor=DARK, spaceAfter=6, leading=13
    ))
    styles.add(ParagraphStyle(
        'BulletItem', parent=styles['Normal'],
        fontSize=9.5, textColor=DARK, leftIndent=20, bulletIndent=10,
        spaceAfter=3, leading=13
    ))
    styles.add(ParagraphStyle(
        'CodeBlock', parent=styles['Code'],
        fontSize=7.5, leading=10, backColor=CODE_BG,
        leftIndent=10, rightIndent=10, spaceBefore=4, spaceAfter=8,
        fontName='Courier'
    ))
    styles.add(ParagraphStyle(
        'CoverInfo', parent=styles['Normal'],
        fontSize=11, textColor=GRAY, alignment=TA_CENTER, spaceAfter=3
    ))
    return styles


def make_table(headers, rows, col_widths=None):
    data = [headers] + rows
    t = Table(data, colWidths=col_widths, repeatRows=1)
    style = [
        ('BACKGROUND', (0, 0), (-1, 0), BLUE),
        ('TEXTCOLOR', (0, 0), (-1, 0), white),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 8.5),
        ('FONTNAME', (0, 1), (-1, -1), 'Helvetica'),
        ('FONTSIZE', (0, 1), (-1, -1), 8),
        ('ALIGN', (0, 0), (-1, 0), 'CENTER'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.grey),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
    ]
    for i in range(1, len(data)):
        if i % 2 == 0:
            style.append(('BACKGROUND', (0, i), (-1, i), LIGHT_BG))
    t.setStyle(TableStyle(style))
    return t


def build_pdf():
    output = '/home/user/OPCUACommDriver/OPC_UA_System_Architecture.pdf'
    doc = SimpleDocTemplate(
        output, pagesize=A4,
        leftMargin=15*mm, rightMargin=15*mm,
        topMargin=20*mm, bottomMargin=20*mm
    )
    s = get_styles()
    story = []

    # ==================== COVER ====================
    story.append(Spacer(1, 60*mm))
    story.append(Paragraph('OPC UA Communication Engine', s['CoverTitle']))
    story.append(Spacer(1, 5*mm))
    story.append(Paragraph('System Architecture Document', s['CoverSub']))
    story.append(Spacer(1, 8*mm))
    story.append(Paragraph('.NET 8.0 WPF + ASP.NET Core REST API + SignalR', s['CoverInfo']))
    story.append(Spacer(1, 15*mm))
    for line in [
        '<b>Version:</b> 1.0.0',
        '<b>Date:</b> 2026-04-12',
        '<b>Framework:</b> .NET 8.0 (Windows)',
        '<b>UI:</b> WPF (Windows Presentation Foundation)',
        '<b>API:</b> ASP.NET Core + SignalR',
        '<b>Protocols:</b> OPC UA, Modbus TCP, Siemens S7, Mitsubishi MC',
    ]:
        story.append(Paragraph(line, s['CoverInfo']))
    story.append(PageBreak())

    # ==================== 1. OVERVIEW ====================
    story.append(Paragraph('1. Overview', s['SectionTitle']))
    story.append(Paragraph(
        'OPC UA Communication Engine is a multi-PLC industrial communication platform '
        'supporting 4 protocols: OPC UA, Modbus TCP, Siemens S7, and Mitsubishi MC. '
        'It provides a WPF desktop interface for monitoring and an embedded ASP.NET Core '
        'server exposing REST API and SignalR Hub for mobile/web clients.', s['Body']))
    story.append(Paragraph(
        'Key features: multi-protocol PLC connections, real-time tag monitoring '
        'via polling/subscriptions, JWT authentication with role-based access, operator '
        'lock mechanism for write safety, automatic reconnection on connection loss, '
        'and SignalR-based real-time data broadcasting.', s['Body']))

    # ==================== 2. PROJECT STRUCTURE ====================
    story.append(Paragraph('2. Project Structure', s['SectionTitle']))
    story.append(Preformatted(
        'OPCUACommDriver/\n'
        '|-- Interfaces/           # Service contracts\n'
        '|-- Models/               # Data models\n'
        '|-- Enums/                # Enumerations\n'
        '|-- Services/\n'
        '|   |-- OpcUa/            # PlcManager, PlcConnection (OPC UA)\n'
        '|   |-- Protocols/        # BaseProtocolConnection, Modbus/S7/MC\n'
        '|   |-- Auth/             # AuthService, UserService, OperatorLockService\n'
        '|   |-- ConfigurationService.cs\n'
        '|   |-- DataCacheService.cs\n'
        '|-- ViewModels/           # MVVM ViewModels\n'
        '|-- Views/                # WPF Windows & Dialogs\n'
        '|-- Helpers/              # Converters, RelayCommand\n'
        '|-- Api/\n'
        '|   |-- Controllers/      # REST (Plcs, Tags, Auth, Users, Lock)\n'
        '|   |-- Hubs/             # SignalR PlcHub\n'
        '|   |-- Models/           # API DTOs\n'
        '|   |-- ApiHostService.cs\n'
        '|-- Configurations/       # JSON config files', s['CodeBlock']))

    # ==================== 3. ARCHITECTURE LAYERS ====================
    story.append(Paragraph('3. Architecture Layers', s['SectionTitle']))
    story.append(Paragraph('3.1 Layer Overview', s['SubTitle']))
    story.append(make_table(
        ['Layer', 'Components', 'Description'],
        [
            ['UI', 'WPF Views + MVVM', 'MainWindow, LoginWindow, Dialogs'],
            ['ViewModel', 'MainViewModel', 'MVVM binding, commands, state'],
            ['Business', 'PlcManager, DataCache', 'Connection mgmt, caching, config'],
            ['Protocol', 'PlcConnection, Base*', 'OPC UA, Modbus, S7, MC'],
            ['API', 'Controllers + SignalR', 'REST endpoints + WebSocket hub'],
            ['Auth', 'JWT + bcrypt + Lock', 'Authentication, authorization'],
            ['Config', 'JSON files', 'plc_config, appsettings, users'],
        ],
        col_widths=[35*mm, 50*mm, 95*mm]
    ))

    story.append(Paragraph('3.2 Protocol Connection Hierarchy', s['SubTitle']))
    story.append(Preformatted(
        'IPlcConnection (interface)\n'
        '  |-- PlcConnection              [OPC UA direct implementation]\n'
        '  |       Session, KeepAlive, Subscriptions\n'
        '  |\n'
        '  |-- BaseProtocolConnection      [Abstract base for TCP protocols]\n'
        '        |  Background Polling, Connection Detection,\n'
        '        |  Auto-Reconnect, SemaphoreSlim Lock\n'
        '        |\n'
        '        |-- ModbusTcpConnection    [Modbus TCP/IP - port 502]\n'
        '        |     FC01-FC06, FC16, Bit-level HR access\n'
        '        |\n'
        '        |-- SiemensS7Connection    [S7comm - port 102]\n'
        '        |     DB, MW, IW, QW memory areas\n'
        '        |\n'
        '        |-- MitsubishiMcConnection [MC Protocol - port 5000]\n'
        '              D, W, M, X, Y device types', s['CodeBlock']))

    # ==================== 4. DATA FLOW ====================
    story.append(PageBreak())
    story.append(Paragraph('4. Data Flow', s['SectionTitle']))
    story.append(Paragraph('4.1 Tag Value Read Flow (PLC -> Client)', s['SubTitle']))
    story.append(Preformatted(
        'PLC Device\n'
        '  | (Modbus TCP / OPC UA / S7 / MC)\n'
        '  v\n'
        'Protocol Connection (poll or subscribe)\n'
        '  |  ReadTagInternalAsync() -> TagValue\n'
        '  v\n'
        'tag.UpdateValue() + OnTagValueChanged event\n'
        '  |                                       \n'
        '  v                                       \n'
        'PlcManager.TagValueChanged event           \n'
        '  |                                       \n'
        '  +---> DataCacheService.SetValue()        \n'
        '  |                                       \n'
        '  +---> ApiHostService subscriber          \n'
        '          |                               \n'
        '          v                               \n'
        '        PlcHubService.BroadcastTagValueAsync()\n'
        '          |                               \n'
        '          v                               \n'
        '        SignalR -> "TagValueChanged" -> All clients', s['CodeBlock']))

    story.append(Paragraph('4.2 Tag Value Write Flow (Client -> PLC)', s['SubTitle']))
    story.append(Preformatted(
        'Client (Android/Web)\n'
        '  | POST /api/tags/{plcId}/{nodeId}/write\n'
        '  v\n'
        'TagsController -> JWT Auth check\n'
        '  v\n'
        'OperatorLock check (must hold lock)\n'
        '  v\n'
        'PlcManager.WriteTagAsync()\n'
        '  v\n'
        'IPlcConnection.WriteTagAsync()\n'
        '  | (SemaphoreSlim lock for TCP protocols)\n'
        '  v\n'
        'WriteTagInternalAsync() -> PLC Device', s['CodeBlock']))

    # ==================== 5. CONNECTION STATE ====================
    story.append(PageBreak())
    story.append(Paragraph('5. Connection State Management', s['SectionTitle']))
    story.append(Paragraph('5.1 State Machine', s['SubTitle']))
    story.append(Preformatted(
        'Disabled --(enable)--> Connecting --(success)--> Connected\n'
        '                           |                        |\n'
        '                       (failure)              (IO error x3)\n'
        '                           |                        |\n'
        '                           v                        v\n'
        '                         Error <------------- Reconnecting\n'
        '                      (max retries)               |\n'
        '                                            (success)\n'
        '                                                  |\n'
        '                                                  v\n'
        'Disconnected <--(manual)-- Disconnecting <-- Connected', s['CodeBlock']))

    story.append(Paragraph('5.2 Connection State Broadcast Chain', s['SubTitle']))
    story.append(Preformatted(
        'BaseProtocolConnection.ConnectionState setter\n'
        '  | fires OnConnectionStateChanged event\n'
        '  v\n'
        'PlcManager.OnConnectionStateChanged()\n'
        '  | re-fires ConnectionStateChanged event\n'
        '  v\n'
        'ApiHostService subscriber\n'
        '  | calls PlcHubService.BroadcastConnectionStateAsync()\n'
        '  v\n'
        'SignalR -> "ConnectionStateChanged" message\n'
        '  | {PlcId, PlcName, State, IsConnected, LastStateChange}\n'
        '  v\n'
        'ALL connected clients (Android, Web, etc.)', s['CodeBlock']))

    story.append(Paragraph('5.3 Modbus TCP Connection Loss Detection', s['SubTitle']))
    story.append(Paragraph(
        'The polling loop in BaseProtocolConnection tracks consecutive IO/Socket errors. '
        'When ALL tags fail with IOException or SocketException for 3 consecutive polling '
        'cycles, HandleConnectionLostAsync() is triggered:', s['Body']))
    for b in [
        'Marks all tags quality = Bad',
        'Calls DisconnectInternalAsync() to cleanup dead TCP socket',
        'Fires ErrorOccurred event (broadcast via SignalR)',
        'Calls StartAutoReconnectAsync() with configurable interval/retries',
        'State transitions: Connected -> Reconnecting -> Connected or Error',
    ]:
        story.append(Paragraph(b, s['BulletItem'], bulletText='\u2022'))

    # ==================== 6. API ENDPOINTS ====================
    story.append(PageBreak())
    story.append(Paragraph('6. REST API Endpoints', s['SectionTitle']))

    story.append(Paragraph('6.1 PLC Management', s['SubTitle']))
    story.append(make_table(
        ['Method', 'Endpoint', 'Description'],
        [
            ['GET', '/api/plcs', 'List all PLCs with connection state'],
            ['GET', '/api/plcs/{id}', 'Get specific PLC details'],
            ['GET', '/api/plcs/status', 'Connection status of all PLCs'],
            ['POST', '/api/plcs/{id}/connect', 'Connect to a PLC'],
            ['POST', '/api/plcs/{id}/disconnect', 'Disconnect from a PLC'],
            ['POST', '/api/plcs/connect-all', 'Connect to all PLCs'],
            ['POST', '/api/plcs/disconnect-all', 'Disconnect all PLCs'],
            ['GET', '/api/plcs/{id}/browse', 'Browse OPC UA server nodes'],
        ],
        col_widths=[22*mm, 52*mm, 106*mm]
    ))

    story.append(Paragraph('6.2 Tag Operations', s['SubTitle']))
    story.append(make_table(
        ['Method', 'Endpoint', 'Description'],
        [
            ['GET', '/api/tags', 'List all tags'],
            ['GET', '/api/tags/plc/{plcId}', 'Tags for specific PLC'],
            ['GET', '/api/tags/subscribed', 'Tags from connected PLCs only'],
            ['GET', '/api/tags/{plcId}/{nodeId}/read', 'Read single tag value'],
            ['POST', '/api/tags/{plcId}/{nodeId}/write', 'Write single tag value'],
            ['POST', '/api/tags/{plcId}/write-multiple', 'Write multiple tags'],
        ],
        col_widths=[22*mm, 55*mm, 103*mm]
    ))

    story.append(Paragraph('6.3 Authentication & Lock', s['SubTitle']))
    story.append(make_table(
        ['Method', 'Endpoint', 'Description'],
        [
            ['POST', '/api/auth/login', 'Login (returns JWT access + refresh tokens)'],
            ['POST', '/api/auth/logout', 'Logout and revoke session'],
            ['POST', '/api/auth/refresh', 'Refresh access token'],
            ['GET', '/api/users', 'List users (Admin only)'],
            ['POST', '/api/lock/acquire', 'Acquire operator lock for writing'],
            ['POST', '/api/lock/release', 'Release operator lock'],
            ['GET', '/api/lock/status', 'Check current lock status'],
        ],
        col_widths=[22*mm, 50*mm, 108*mm]
    ))

    # ==================== 7. SIGNALR ====================
    story.append(PageBreak())
    story.append(Paragraph('7. SignalR Real-time Hub', s['SectionTitle']))
    story.append(Paragraph('Hub URL: /hubs/plc (WebSocket with JWT authentication)', s['Body']))

    story.append(Paragraph('7.1 Server -> Client Messages', s['SubTitle']))
    story.append(make_table(
        ['Message', 'Payload'],
        [
            ['ConnectionStateChanged', '{PlcId, PlcName, State, IsConnected, LastStateChange}'],
            ['TagValueChanged', '{PlcId, TagId, NodeId, Value, Quality, Timestamp}'],
            ['PlcStatus', 'List<{PlcId, PlcName, State, IsConnected}>'],
            ['LockAcquired', '{LockId, UserId, Username, AcquiredAt, ExpiresAt}'],
            ['LockReleased', '{LockId, ReleasedBy}'],
            ['LockExtended', '{LockId, NewExpiresAt}'],
        ],
        col_widths=[55*mm, 125*mm]
    ))

    story.append(Paragraph('7.2 Client -> Server Methods', s['SubTitle']))
    story.append(make_table(
        ['Method', 'Description'],
        [
            ['SubscribeToPlc(plcId)', 'Subscribe to updates for a specific PLC'],
            ['UnsubscribeFromPlc(plcId)', 'Unsubscribe from a specific PLC'],
            ['SubscribeToAll()', 'Subscribe to all PLC updates'],
            ['GetStatus()', 'Request current connection status of all PLCs'],
            ['GetAllTagValues()', 'Request all current tag values'],
            ['WriteTag(plcId, nodeId, value)', 'Write a tag value via SignalR'],
        ],
        col_widths=[60*mm, 120*mm]
    ))

    # ==================== 8. AUTH ====================
    story.append(PageBreak())
    story.append(Paragraph('8. Authentication & Security', s['SectionTitle']))

    story.append(Paragraph('8.1 Role-Based Access Control', s['SubTitle']))
    story.append(make_table(
        ['Role', 'Read', 'Write', 'Lock', 'User Mgmt', 'Notes'],
        [
            ['Viewer', 'Yes', 'No', 'No', 'No', 'Read-only access'],
            ['Operator', 'Yes', 'Yes*', 'Yes', 'No', '*Requires lock'],
            ['Admin', 'Yes', 'Yes', 'Override', 'Yes', 'Full access'],
        ],
        col_widths=[25*mm, 18*mm, 18*mm, 22*mm, 25*mm, 72*mm]
    ))

    story.append(Paragraph('8.2 Authentication Flow', s['SubTitle']))
    story.append(Preformatted(
        'Client                          Server\n'
        '  |                                |\n'
        '  |--- POST /api/auth/login ------>|\n'
        '  |    {username, password,         |\n'
        '  |     deviceId}                   |\n'
        '  |                                |\n'
        '  |<-- {accessToken,               |\n'
        '  |     refreshToken,              |\n'
        '  |     expiresAt} ----------------|\n'
        '  |                                |\n'
        '  |--- GET /api/tags ------------->|\n'
        '  |    Authorization: Bearer token  |\n'
        '  |                                |\n'
        '  |--- POST /api/lock/acquire ---->|\n'
        '  |    (Operator/Admin only)        |\n'
        '  |                                |\n'
        '  |--- POST /api/tags/.../write -->|\n'
        '  |    (Must hold operator lock)    |', s['CodeBlock']))

    story.append(Paragraph('8.3 Operator Lock Mechanism', s['SubTitle']))
    story.append(Paragraph(
        'Only one operator can write at a time. The lock has configurable timeout '
        'and auto-expiration. Admin can override locks. Lock state is persisted to '
        'lock_state.json and broadcast via SignalR on acquire/release/extend.', s['Body']))

    # ==================== 9. MODBUS MAPPING ====================
    story.append(PageBreak())
    story.append(Paragraph('9. Modbus TCP Tag Mapping', s['SectionTitle']))

    story.append(Paragraph('9.1 Address Format', s['SubTitle']))
    story.append(make_table(
        ['Format', 'Description', 'Example'],
        [
            ['HR{n}', 'Holding Register n (FC03/FC06)', 'HR0, HR1, HR100'],
            ['HR{n}.{bit}', 'Bit in Holding Register', 'HR0.0, HR0.14'],
            ['IR{n}', 'Input Register n (FC04)', 'IR0, IR50'],
            ['C{n}', 'Coil n (FC01/FC05)', 'C0, C8192'],
            ['DI{n}', 'Discrete Input n (FC02)', 'DI0, DI50'],
        ],
        col_widths=[30*mm, 85*mm, 65*mm]
    ))

    story.append(Paragraph('9.2 S7-1500 Mapping (192.168.1.10:502)', s['SubTitle']))
    story.append(Paragraph('Booleans packed in HR0 bits, integers in HR1-HR28:', s['Body']))
    story.append(make_table(
        ['Tag', 'NodeId', 'DataType', 'PLC Register', 'Modbus Addr'],
        [
            ['bool1', 'HR0.0', 'Boolean', 'MB_HOLD_REG[0].0', '40001 bit 0'],
            ['bool2', 'HR0.1', 'Boolean', 'MB_HOLD_REG[0].1', '40001 bit 1'],
            ['...', '...', '...', '...', '...'],
            ['bool15', 'HR0.14', 'Boolean', 'MB_HOLD_REG[0].14', '40001 bit 14'],
            ['int1', 'HR1', 'Int16', 'MB_HOLD_REG[1]', '40002'],
            ['int2', 'HR2', 'Int16', 'MB_HOLD_REG[2]', '40003'],
            ['...', '...', '...', '...', '...'],
            ['int28', 'HR28', 'Int16', 'MB_HOLD_REG[28]', '40029'],
        ],
        col_widths=[25*mm, 25*mm, 25*mm, 50*mm, 55*mm]
    ))

    story.append(Paragraph('9.3 FX5U Mapping (192.168.1.11:502)', s['SubTitle']))
    story.append(Paragraph('Booleans via M coils (direct bit), integers via D registers:', s['Body']))
    story.append(make_table(
        ['Tag', 'NodeId', 'DataType', 'PLC Device', 'Modbus Addr'],
        [
            ['bool1', 'C8192', 'Boolean', 'M0', 'Coil 8192'],
            ['bool2', 'C8193', 'Boolean', 'M1', 'Coil 8193'],
            ['...', '...', '...', '...', '...'],
            ['bool15', 'C8206', 'Boolean', 'M14', 'Coil 8206'],
            ['int1', 'HR0', 'Int16', 'D0', '40001'],
            ['int2', 'HR1', 'Int16', 'D1', '40002'],
            ['...', '...', '...', '...', '...'],
            ['int28', 'HR27', 'Int16', 'D27', '40028'],
        ],
        col_widths=[25*mm, 25*mm, 25*mm, 50*mm, 55*mm]
    ))

    # ==================== 10. DEPENDENCIES ====================
    story.append(PageBreak())
    story.append(Paragraph('10. Dependencies', s['SectionTitle']))
    story.append(make_table(
        ['Package', 'Version', 'Purpose'],
        [
            ['OPCFoundation.NetStandard.Opc.Ua', '1.5.374', 'OPC UA protocol stack'],
            ['OPCFoundation...Opc.Ua.Client', '1.5.374', 'OPC UA client library'],
            ['CommunityToolkit.Mvvm', '8.2.2', 'MVVM toolkit for WPF'],
            ['Newtonsoft.Json', '13.0.3', 'JSON serialization'],
            ['Serilog + Sinks', '3.1.1', 'Structured logging (File, Console)'],
            ['Microsoft.Extensions.DI', '8.0.0', 'Dependency Injection'],
            ['Microsoft.AspNetCore.App', '8.0', 'ASP.NET Core framework'],
            ['Swashbuckle.AspNetCore', '6.5.0', 'Swagger / OpenAPI docs'],
            ['BCrypt.Net-Next', '4.0.3', 'Password hashing'],
            ['MS...Authentication.JwtBearer', '8.0.0', 'JWT authentication'],
        ],
        col_widths=[65*mm, 25*mm, 90*mm]
    ))

    # ==================== 11. CONFIG FILES ====================
    story.append(Spacer(1, 8*mm))
    story.append(Paragraph('11. Configuration Files', s['SectionTitle']))
    story.append(make_table(
        ['File', 'Purpose'],
        [
            ['plc_config.json', 'PLC devices, tags, subscription groups'],
            ['appsettings.json', 'API port, bind address, logging settings'],
            ['auth_settings.json', 'JWT secret, token expiry, lock timeout'],
            ['users.json', 'User accounts (bcrypt hashed passwords)'],
            ['lock_state.json', 'Current operator lock state (auto-generated)'],
            ['modbus_plc_config_sample.json', 'Sample config for Modbus TCP PLCs'],
        ],
        col_widths=[65*mm, 115*mm]
    ))

    # ==================== BUILD ====================
    doc.build(story)
    print(f'PDF generated: {output}')


if __name__ == '__main__':
    build_pdf()
