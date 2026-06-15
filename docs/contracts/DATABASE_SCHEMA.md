# Database Schema Contract

Day la dac ta schema ky thuat cho Azure SQL/SQL Server. Tai lieu nay khong thay the ERD.

## Conventions

- Primary key nghiep vu dung `uniqueidentifier`, do application tao.
- Telemetry va audit stream dung `bigint identity` de insert hieu qua.
- Tat ca timestamp dung `datetime2(3)` UTC.
- Chuoi public co gioi han ro rang; secret/token/pairing code chi luu hash.
- Dynamic value duoc luu vao dung mot trong ba cot `NumberValue`, `BooleanValue`, `StringValue` va co `ValueType` check constraint.
- Status/kind/data type dung chuoi co check constraint de database de doc va khong chap nhan gia tri la.
- JSON chi dung cho danh sach command va snapshot bo sung; cot JSON co `ISJSON` constraint.

## Tables and important indexes

| Table | Purpose | Important constraints/indexes |
| --- | --- | --- |
| `Users` | Tai khoan | unique normalized email |
| `RefreshTokens` | Rotating session token | unique token hash, user/expiry index |
| `Devices` | Device identity va ownership | unique serial, owner/status index |
| `PairingCodes` | Claim code mot lan | unique code hash, device/expiry index |
| `Capabilities` | Runtime manifest | unique device/key, kind/type checks |
| `TelemetryRecords` | Lich su sensor | unique message/capability, capability/time index |
| `DeviceLatestValues` | Gia tri moi nhat | one row per capability |
| `DeviceCommands` | Desired actuator command | unique command ID, device/status index |
| `CommandAcknowledgements` | ACK stream | unique message ID, command/time index |
| `AutomationRules` | If-Then rule | owner/enabled index, source/target FKs |
| `AutomationExecutions` | Rule evaluation history | unique rule/message, rule/time index |
| `ProcessedMessages` | MQTT idempotency | unique message ID, processed time index |
| `AuditLogs` | Security/business audit | user/time and entity/time indexes |

## Delete policy

- User/device/capability business records are deactivated or unclaimed rather than physically deleted during normal operation.
- Historical telemetry, command, automation execution and audit rows use restrictive foreign keys to prevent accidental data loss.
- Refresh token and pairing code rows may be deleted by retention jobs after expiry.

