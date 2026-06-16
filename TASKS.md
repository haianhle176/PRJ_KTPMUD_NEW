# Tasks

Tai lieu nay la backlog thuc thi. Lam theo thu tu dependency; khong bat dau P6 truoc khi P1-P5 hoan thanh.

Ky hieu:

- `[ ]` chua bat dau.
- `[>]` dang thuc hien.
- `[x]` hoan thanh va da kiem chung.
- `[!]` dang bi chan.

## P0 - Dac ta va nen mong

### P0.1 Contract va tai lieu

- [x] `P0-01` Tao glossary cho device, capability, telemetry, state, command, ACK, rule va execution.
- [x] `P0-02` Chot JSON schema cho manifest, telemetry, state, command, ACK va presence.
- [x] `P0-03` Tao payload mau hop le va khong hop le cho tung MQTT message.
- [x] `P0-04` Chot REST request/response DTO, status code va error envelope.
- [x] `P0-05` Chot pagination, sorting, filtering va UTC timestamp convention.
- [ ] `P0-06` Ve Architecture Diagram va bon Sequence Diagram ban dau.
- [ ] `P0-07` Ve ERD, Class Diagram, Use Case Diagram va hai State Diagram.

### P0.2 Solution va coding foundation

- [x] `P0-08` Tao solution va cac project Domain, Application, Infrastructure, Api, Worker, DeviceApp, Wpf.
- [x] `P0-09` Tao test projects va project references dung dependency direction.
- [x] `P0-10` Cau hinh nullable reference types, analyzers va warning policy.
- [x] `P0-11` Cau hinh dependency injection, options validation va structured logging.
- [x] `P0-12` Tao configuration template cho SQL, JWT va HiveMQ khong chua secret.
- [x] `P0-13` Tao CI build/test co ban.

### P0.3 Database foundation

- [x] `P0-14` Chot data type, PK/FK, unique constraint va index cho ERD.
- [x] `P0-15` Tao versioned migration scripts cho cac bang cot loi.
- [x] `P0-16` Tao database initializer/migration runner idempotent.
- [x] `P0-17` Tao Dapper connection factory va transaction abstraction.
- [x] `P0-18` Kiem thu khoi tao database rong va chay lai migration an toan.

**P0 complete khi:** solution build/test thanh cong, database khoi tao lap lai duoc, MQTT/REST contract va diagram ban dau da review.

## P1 - Device identity va capability discovery

- [x] `P1-01` Tao serial duy nhat va luu ben vung theo DeviceApp instance.
- [x] `P1-02` Xay runtime manifest editor cho number, boolean va string capability.
- [x] `P1-03` Validate unique key, range, kind, writable va supported command.
- [x] `P1-04` Cau hinh MQTTnet client ket noi HiveMQ Cloud qua TLS.
- [x] `P1-05` Them reconnect exponential backoff va MQTT Last Will.
- [x] `P1-06` Publish retained manifest/presence va tang `manifestVersion` khi thay doi.
- [x] `P1-07` Worker subscribe cac topic va route message theo loai.
- [x] `P1-08` Luu/upsert unclaimed device va capability; inactive capability bi giu lich su.
- [x] `P1-09` Sinh pairing code ngau nhien, luu hash va het han sau 10 phut.
- [x] `P1-10` Test nhieu DeviceApp co manifest khac nhau va reconnect thanh cong.

**P1 complete khi:** it nhat ba DeviceApp dong thoi online, co serial va manifest khac nhau trong database.

## P2 - Tai khoan va claim thiet bi

- [x] `P2-01` Tao `Users` va `RefreshTokens` repositories/use cases.
- [x] `P2-02` Implement register voi normalized unique identity va password hashing.
- [x] `P2-03` Implement login, JWT access token va rotating refresh token.
- [x] `P2-04` Implement logout/revoke refresh token.
- [x] `P2-05` Implement claim transaction bang pairing code mot lan.
- [x] `P2-06` Implement device list/detail, rename, classify va unclaim.
- [x] `P2-07` Them `IUserContext` va ownership authorization cho moi device use case.
- [x] `P2-08` Ghi audit log cho auth, claim, unclaim va rename.
- [x] `P2-09` Test expired/used/invalid pairing code va concurrent claim.
- [x] `P2-10` Test hai user khong doc, sua hoac claim cheo tai nguyen.

**P2 complete khi:** hai tai khoan doc lap chi truy cap duoc thiet bi minh da claim.

## P3 - Telemetry pipeline

- [ ] `P3-01` DeviceApp ho tro telemetry tu sinh va nhap tay theo capability.
- [ ] `P3-02` Validate schema version, serial, timestamp, capability va data type tai Worker.
- [ ] `P3-03` Implement deduplication theo `messageId`.
- [ ] `P3-04` Insert `TelemetryRecords` va transactional upsert `DeviceLatestValues`.
- [ ] `P3-05` Cap nhat `LastSeenAt` va online/offline state tu presence.
- [ ] `P3-06` Implement device list/detail va latest-values API.
- [ ] `P3-07` Implement telemetry history API co pagination va time range.
- [ ] `P3-08` Test malformed payload, unknown capability, wrong type va duplicate message.
- [ ] `P3-09` Load test 100 device, moi device mot message moi 5 giay.

**P3 complete khi:** telemetry di end-to-end va duoc doc lai dung qua API ma khong bi trung.

## P4 - Dieu khien actuator

- [ ] `P4-01` Tao command use case voi ownership, writable va data type validation.
- [ ] `P4-02` Luu command o trang thai `Pending` va nguon `Manual`/`Automation`.
- [ ] `P4-03` Worker publish command va chuyen `Pending -> Sent`.
- [ ] `P4-04` DeviceApp subscribe command, validate va thuc thi actuator.
- [ ] `P4-05` DeviceApp publish `accepted`, `completed` hoac `failed` ACK.
- [ ] `P4-06` Worker xu ly ACK idempotent va enforce state transition hop le.
- [ ] `P4-07` Implement timeout va retry gioi han cho loi tam thoi.
- [ ] `P4-08` Implement command status API va reported state.
- [ ] `P4-09` Test offline device, delayed/duplicate ACK, invalid transition va retry.

**P4 complete khi:** command API thay doi DeviceApp va trang thai cuoi cung duoc quan sat chinh xac.

## P5 - Automation engine

- [ ] `P5-01` Tao CRUD API cho automation rule va enable/disable.
- [ ] `P5-02` Validate source sensor, target actuator, data type va ownership.
- [ ] `P5-03` Implement comparator `>`, `>=`, `<`, `<=`, `==`, `!=`.
- [ ] `P5-04` Implement edge trigger va cooldown.
- [ ] `P5-05` Danh gia rule sau khi telemetry transaction thanh cong.
- [ ] `P5-06` Tao automation command qua cung command service cua manual control.
- [ ] `P5-07` Luu `AutomationExecutions` voi trigger/skip/failure reason.
- [ ] `P5-08` Ngan duplicate execution khi telemetry bi giao lai.
- [ ] `P5-09` Test sensor A dieu khien actuator B, cooldown va disabled rule.

**P5 complete khi:** automation end-to-end on dinh va co execution history de giai thich moi command.

## P6 - WPF App

> Blocked cho den khi P1-P5 complete. WPF khong duoc chua business rule cot loi.

- [!] `P6-01` Tao WPF shell theo MVVM, DI va navigation.
- [!] `P6-02` Tao typed API client, token refresh va secure token storage.
- [!] `P6-03` Tao man hinh register/login/logout.
- [!] `P6-04` Tao device list voi online state, filter, claim, rename va classification.
- [!] `P6-05` Tao device detail voi capability va latest values.
- [!] `P6-06` Tao telemetry history view va bieu do toi thieu.
- [!] `P6-07` Tao actuator control va command status feedback.
- [!] `P6-08` Tao automation rule list/editor tu capability dong.
- [!] `P6-09` Them polling 3-5 giay, loading, empty, validation va error states.
- [!] `P6-10` Chay full MVP demo chi qua WPF va DeviceApp.

**P6 complete khi:** nguoi dung thuc hien duoc toan bo MVP ma khong can Swagger/Postman.

## P7 - Cloud, kiem thu va bao cao

- [ ] `P7-01` Provision Azure SQL va Azure App Service F1 cho Web API; Worker tiep tuc chay local.
- [ ] `P7-02` Cau hinh API secret trong Azure App Settings va Worker/HiveMQ secret bang local user-secrets hoac environment variables.
- [ ] `P7-03` Them readiness/liveness health checks va structured cloud logs.
- [ ] `P7-04` Chay migration va smoke test tren Azure environment.
- [ ] `P7-05` Hoan thien unit, integration, API, MQTT va end-to-end test suite.
- [ ] `P7-06` Tao seed/demo scenario khong chua secret.
- [ ] `P7-07` Viet script demo claim -> telemetry -> manual command -> automation.
- [ ] `P7-08` Hoan thien diagram va dong bo voi implementation thuc te.
- [ ] `P7-09` Viet deployment guide, test report, risk va limitation sections.
- [ ] `P7-10` Dien tap demo: danh thuc API F1, khoi dong Worker local, sau do test disconnect/reconnect va restart.

**P7 complete khi:** Web API deploy lap lai duoc tren Azure F1, Worker/DeviceApp/WPF chay local on dinh, HiveMQ va Azure SQL cloud hoat dong, va bao cao khop voi topology nay.

## P8 - Backlog sau MVP

- [ ] `P8-01` SignalR realtime thay polling.
- [ ] `P8-02` Rule nhieu condition AND/OR.
- [ ] `P8-03` Schedule/time-window trigger.
- [ ] `P8-04` Offline/threshold notification.
- [ ] `P8-05` Device group, room, area va tag.
- [ ] `P8-06` CSV export va aggregate dashboard.
- [ ] `P8-07` Admin role va device sharing.
- [ ] `P8-08` Rate limiting, retention job va telemetry aggregation.
- [ ] `P8-09` Per-device credential va certificate authentication.
- [ ] `P8-10` OTA configuration.

## Cong viec tiep theo

Bat dau theo thu tu:

1. `P3-01` DeviceApp ho tro telemetry tu sinh va nhap tay theo capability.
2. `P3-02` Validate schema version, serial, timestamp, capability va data type tai Worker.
3. `P3-03` Implement deduplication theo `messageId`.

`P0-06` va `P0-07` duoc hoan theo yeu cau, khong chan phan trien khai ky thuat P1.
