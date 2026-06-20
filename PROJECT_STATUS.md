# Project Status

**Cap nhat lan cuoi:** 2026-06-18  
**Trang thai tong the:** P6 functional MVP complete; P7 cloud/test/report hardening va P9 UI polish in progress  
**Phase hien tai:** P9 UI polish in progress, P7 hardening van dang mo  
**Muc tieu tiep theo:** Publish lai API de co health/capability endpoints, mo WPF de visual QA va chay smoke test checklist

## 1. Tien do phase

| Phase | Noi dung | Trang thai | Tien do |
| --- | --- | --- | --- |
| P0 | Dac ta va nen mong | Complete | 100% |
| P1 | Device identity va capability discovery | Complete | 100% |
| P2 | Tai khoan va claim thiet bi | Complete | 100% |
| P3 | Telemetry pipeline | Complete | 100% |
| P4 | Dieu khien actuator | Complete | 100% |
| P5 | Automation engine | Complete | 100% |
| P6 | WPF App | Functional MVP complete; polish deferred | 100% |
| P7 | Cloud, kiem thu va bao cao | In progress | 35% |
| P8 | Mo rong sau MVP | P8-03/P8-04/P8-08 backend MVP complete; remaining items deferred | 30% |
| P9 | UI polish | Impeccable direction implemented in WPF code; visual QA pending | 65% |

P0 da hoan thanh day du. Architecture, bon sequence, ERD, class, use case va hai state diagrams duoc luu tai `docs/diagrams` bang Mermaid va phan anh implementation hien tai.

P1 da hoan thanh: persistent profile/serial, runtime manifest, TLS MQTT, Last Will, reconnect, Worker routing, database upsert, pairing hash va verification tren Azure SQL.

P2 da hoan thanh: account register/login/refresh/logout, password hashing, JWT token service, rotating refresh token, pairing-code claim, device list/detail/rename/classify/unclaim, ownership authorization va audit log cho auth/device operations.

P3 da hoan thanh: DeviceApp publish telemetry tu dong/nhap tay, Worker subscribe telemetry, validate payload/capability/type/range, dedupe theo `ProcessedMessages`, insert `TelemetryRecords`, upsert `DeviceLatestValues`, cap nhat online/last seen va API doc latest/history theo ownership.

P4 da hoan thanh: API tao manual command, validate ownership/writable actuator/value type/supported command, luu `pending`, Worker publish MQTT command va chuyen `sent`, DeviceApp subscribe/thuc thi actuator dong va publish ACK, Worker xu ly ACK idempotent, retry command da sent neu chua co ACK va timeout khi het han.

P5 da hoan thanh: CRUD automation rule, validation source sensor/target actuator/ownership/data type, comparator `> >= < <= == !=`, edge trigger, cooldown, evaluation sau telemetry transaction, tao command source `automation`, ghi execution history va chong duplicate execution khi telemetry bi giao lai.

P6 da hoan thanh o muc functional MVP: MVVM shell, typed API client, DPAPI token storage, login/register/logout, device list/claim/rename, telemetry latest/history, actuator command form, automation rule list/editor/execution history va polling 5 giay. Demo thuc te da xac nhan claim, telemetry va command end-to-end qua WPF + Worker + DeviceApp.

P7 da bat dau: topology Azure F1 + Azure SQL + HiveMQ + Worker local da duoc xac nhan, them API liveness/readiness health checks, tao deployment guide, demo script va smoke test checklist trong `docs/operations`.

P8 da lam dung ba muc duoc yeu cau: automation co schedule/time-window optional, notification backend cho offline/threshold, API rate limiting, Worker maintenance job cho retention va hourly telemetry aggregation. Cac muc P8 khac giu nguyen backlog, chua dong den.

P9 da bat dau theo Impeccable: dung palette xanh control, shell sidebar, dashboard gọn, device detail render theo capability dong, actuator control don gian va Advanced shortcuts cho Worker/DeviceApp chi khi chay tu source workspace. Can mo WPF de visual QA va publish lai API de Azure co endpoint capabilities moi.

## 2. Da hoan thanh

- [x] Xac dinh muc tieu nen tang IoT dong.
- [x] Chot mo hinh da nguoi dung.
- [x] Chot HiveMQ Cloud lam MQTT broker.
- [x] Chot runtime capability manifest thay cho device type dong cung.
- [x] Chot pairing code lam co che claim thiet bi.
- [x] Chot API va MQTT Worker la hai process logic rieng.
- [x] Chot automation v1 gom mot condition va mot action.
- [x] Chot chien luoc backend-first, UI-last.
- [x] Chot deployment: API tren Azure App Service F1; Worker, DeviceApp va WPF chay local; HiveMQ va Azure SQL tren cloud.
- [x] Da tao App Service F1, Azure SQL Database va HiveMQ Cloud cluster; thong tin secret khong luu trong repository.
- [x] Tao implementation plan P0-P8.
- [x] Xac dinh noi dung Architecture, Use Case, Class, ERD, Sequence va State diagrams.
- [x] Tao MQTT JSON Schema va valid/invalid examples cho sau message type.
- [x] Chot REST error envelope, status code va pagination convention.
- [x] Tao solution gom 7 source projects va 5 test projects.
- [x] Cau hinh dependency direction, nullable, warnings-as-errors va deterministic build.
- [x] Tao typed options validation cho SQL, JWT va HiveMQ.
- [x] Tao GitHub Actions workflow build/test tren Windows.
- [x] Build toan solution: 0 warnings, 0 errors.
- [x] Chay 2 domain unit tests thanh cong.
- [x] Chot schema contract gom 13 bang nghiep vu, PK/FK, check constraint va index.
- [x] Tao hai SQL migration co version cho identity/device va telemetry/command/automation.
- [x] Tao embedded migration discovery, SHA-256 checksum va migration history.
- [x] Tao Dapper SQL connection factory va transaction scope commit/rollback.
- [x] Kiem thu migration tren SQL Server LocalDB rong va chay lai idempotent.
- [x] Don code template WeatherForecast va Class1 khong thuoc du an.
- [x] DeviceApp profile cho phep nhieu device tren cung laptop bang `--profile`.
- [x] `--reconfigure` giu serial va tang `manifestVersion`.
- [x] DeviceApp publish retained manifest/presence qua MQTT TLS, QoS 1 va Last Will.
- [x] Worker reconnect, subscribe manifest/presence va validate topic serial.
- [x] Worker upsert device/capability, inactive capability cu va luu pairing-code hash.
- [x] Them DeviceApp va Worker tests; tong cong 9 tests thuc dang pass.
- [x] P1-10 da chay voi ba DeviceApp qua HiveMQ Cloud vao Azure SQL.
- [x] Reconnect/Last Will da duoc kiem tra bang cach kill va khoi dong lai `environment-sensor`.
- [x] `--verify-p1` xac nhan trong Azure SQL: `DEV-P1ENV000001` co 2 capabilities, `DEV-P1PUMP00001` co 1, `DEV-P1ROOM00001` co 2.
- [x] Implement auth use cases: register, login, refresh token rotation va logout/revoke.
- [x] Implement password hashing, JWT access token va bearer-token user extraction cho API.
- [x] Implement claim device bang pairing code mot lan, co expiry va transaction lock.
- [x] Implement device management API/use case: list, detail, rename/classify va unclaim.
- [x] Enforce ownership: user chi doc/sua/unclaim duoc device minh da claim.
- [x] Ghi audit log cho `auth.register`, `auth.login`, `auth.logout`, `device.claim`, `device.update`, `device.unclaim`.
- [x] Them integration tests cho duplicate/invalid credential, refresh rotation, used/expired/concurrent pairing code va cross-user device access.
- [x] Them `DeviceTelemetryMessage` va telemetry publish tu DeviceApp moi 5 giay cho sensor capability.
- [x] Them manual telemetry command trong DeviceApp: `set <capabilityKey> <value>`.
- [x] Worker subscribe `iot/devices/+/telemetry`, route va luu telemetry.
- [x] Them SQL telemetry ingestion voi validation schema, serial, active sensor capability, value type va min/max.
- [x] Dedupe MQTT QoS 1 bang `ProcessedMessages.MessageId`.
- [x] Insert telemetry history va upsert latest values trong cung transaction.
- [x] Them API `GET /api/v1/devices/{id}/latest-values`.
- [x] Them API `GET /api/v1/devices/{id}/telemetry` co filter capability/time range va pagination.
- [x] Them LocalDB integration tests cho duplicate, unknown capability, wrong type, actuator reading, cross-user read isolation va 100-device load baseline.
- [x] Them command DTO/use case va repository cho manual actuator control.
- [x] Them API `POST /api/v1/devices/{id}/commands` va `GET /api/v1/devices/{id}/commands/{commandId}`.
- [x] Worker polling command pending/sent retry, publish MQTT command va cap nhat `sent`.
- [x] DeviceApp subscribe command topic, validate capability dong, cap nhat actuator state mo phong va publish `accepted`/`completed`/`failed`.
- [x] Worker subscribe command ACK va ghi `CommandAcknowledgements` idempotent.
- [x] Cap nhat command status theo ACK: `accepted`, `completed`, `failed`, `timed_out`.
- [x] Them P4 integration tests cho create/dispatch/completed ACK duplicate/retry/timeout va reject cross-user/sensor/wrong-type/unsupported command.
- [x] Them migration `V003_AutomationRuleCommandName.sql` de AutomationRules luu command actuator.
- [x] Them Automation application service/repository va API CRUD `/api/v1/automation-rules`.
- [x] Them execution API `/api/v1/automation-rules/{id}/executions`.
- [x] Telemetry ingestion danh gia automation rule trong cung transaction sau khi ghi history/latest.
- [x] Automation tao `DeviceCommands` source `automation` dung chung pipeline P4.
- [x] Luu `AutomationExecutions` voi outcome `triggered`/`skipped` va reason.
- [x] Them P5 integration tests cho trigger, skipped, duplicate telemetry, edge/cooldown, disabled rule va invalid ownership/capability.
- [x] Tao WPF functional shell bang `TabControl`: Auth, Devices, Telemetry, Commands, Automation.
- [x] Tao typed API client trong WPF voi token refresh va DPAPI token storage theo Windows user.
- [x] WPF goi API auth/device/telemetry/command/automation va polling 5 giay.
- [x] WPF build thanh cong trong solution.
- [x] Them API `GET /api/v1/devices/{id}/capabilities` de WPF render UI dong theo manifest.
- [x] Polish WPF theo Impeccable: sidebar shell, Home dashboard, sensor/actuator cards, empty states, status bar va Advanced local launchers cho Worker/DeviceApp.

## 3. Chua bat dau

- [ ] API can publish lai len Azure de co `/health/live`, `/health/ready` va `/api/v1/devices/{id}/capabilities`.
- [ ] API/Worker can chay migration V004 tren Azure SQL truoc khi test notification/aggregation.
- [ ] Test coverage hien bao phu P0-P5 cot loi; P6/P9 moi duoc verify bang build, chua co UI automation test.
- [ ] Chua xac minh ket noi runtime tu source hien tai toi HiveMQ Cloud, Azure SQL va App Service F1.
- [x] Da co bo diagram artifact hoan chinh tai `docs/diagrams`.
- [ ] Chua mo WPF de visual QA layout P9 tren man hinh that.
- [ ] P7 smoke test checklist can duoc chay lai day du truoc demo/bao cao.

## 4. Verification gan nhat

- `dotnet build IoTPlatform.sln --configuration Debug`: thanh cong, 0 warnings, 0 errors.
- `dotnet test IoTPlatform.sln --configuration Debug`: thanh cong; 23 tests passed. `IoTPlatform.Application.Tests` va `IoTPlatform.Api.Tests` hien la scaffold chua co test nen test runner bao `No test is available` nhung exit code thanh cong.
- `dotnet build IoTPlatform.sln --configuration Debug`: thanh cong sau khi them WPF UI.
- `dotnet build IoTPlatform.sln --configuration Debug`: thanh cong sau P9 UI polish va capability endpoint, 0 warnings, 0 errors.
- `dotnet test IoTPlatform.sln --configuration Debug --no-build`: thanh cong sau P9; 25 tests co thuc thi pass, Application/API test projects van la scaffold khong co test.
- WPF + Worker + DeviceApp demo thuc te: claim device, xem telemetry va gui command actuator thanh cong.
- `dotnet test IoTPlatform.sln --configuration Debug`: thanh cong sau P8-03/P8-04/P8-08; 25 tests co thuc thi pass, Application/API test projects van la scaffold khong co test.
- Tat ca JSON schema va example files parse thanh cong nhu JSON.
- LocalDB integration: tao database rong, apply 2 migrations, apply lai, kiem tra 13 bang, rollback/commit va xoa database test thanh cong.
- P1 cloud integration: Worker ket noi Azure SQL + HiveMQ; ba DeviceApp publish manifest/presence; Last Will offline va reconnect online duoc ghi nhan.
- `dotnet run --project src\IoTPlatform.Worker -- --verify-p1`: thanh cong, doc duoc ba device P1 trong Azure SQL.
- P2 integration tests: auth register/login/refresh/logout, pairing code used/expired/concurrent, device ownership list/detail/update/unclaim va audit log deu pass tren LocalDB.
- P3 integration tests: telemetry ingest/latest/history/dedup/reject malformed va 100-device local load baseline deu pass tren LocalDB.
- P4 integration tests: command create/dispatch/ACK idempotency/retry/timeout va validation cross-user/sensor/wrong-type/unsupported command deu pass tren LocalDB.
- P5 integration tests: automation CRUD validation, sensor-to-actuator trigger, cooldown/edge/disabled rule va duplicate telemetry protection deu pass tren LocalDB.

## 5. Dieu kien mo khoa P6

P6 chi bat dau sau khi:

- P1 discovery da ghi device/capability vao database.
- P2 ownership da dam bao moi telemetry/query deu co ranh gioi user.
- P3 telemetry da ghi history/latest va doc lai duoc qua API.
- P4 command service da tao command manual va DeviceApp co ACK.
- P5 automation da tao command source `automation` va co execution history.
- API endpoints cho auth, device, telemetry, command va automation da san sang de WPF goi.
- Solution build/test thanh cong sau P5.

## 6. Blocker va rui ro hien tai

### Blocker

- Chua co blocker dang chan P6 local development.

### Rui ro

| Rui ro | Anh huong | Giam thieu |
| --- | --- | --- |
| Capability dong lam contract qua mo | Cao | Chot JSON schema va validation matrix trong P0 |
| QoS 1 tao message trung | Cao | Unique message ID va bang `ProcessedMessages` |
| Worker xu ly telemetry va command dong thoi | Cao | Tach handler, transaction boundary va idempotency |
| Multi-user ro ri du lieu | Cao | Ownership filter trong repository/use case va integration test cheo user |
| WPF duoc lam qua som | Trung binh | Giu P6 blocked den khi P1-P5 dat acceptance criteria |
| Azure/HiveMQ loi khi demo | Trung binh | Reconnect, health check, scripted demo va test truoc buoi bao cao |
| Azure F1 dua API vao trang thai ngu | Trung binh | Gui health request truoc demo va chap nhan cold start |
| Laptop tat hoac mat mang lam Worker dung | Cao | Cam nguon, tat sleep, kiem tra mang va khoi dong Worker truoc demo |

## 7. Cach cap nhat tai lieu nay

- Cap nhat ngay va phase sau moi dot trien khai.
- Chi danh dau task complete khi dat Definition of Done trong `PROJECT_CONTEXT.md`.
- Ghi blocker moi ngay khi phat hien; xoa blocker khi co bang chung da giai quyet.
- Khong tu dong tang phan tram theo so task; danh gia theo acceptance criteria cua phase.
