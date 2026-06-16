# Project Status

**Cap nhat lan cuoi:** 2026-06-16  
**Trang thai tong the:** P2 complete; P3 ready  
**Phase hien tai:** P3 ready to start  
**Muc tieu tiep theo:** Telemetry pipeline

## 1. Tien do phase

| Phase | Noi dung | Trang thai | Tien do |
| --- | --- | --- | --- |
| P0 | Dac ta va nen mong | Technical work complete; diagrams deferred | 90% |
| P1 | Device identity va capability discovery | Complete | 100% |
| P2 | Tai khoan va claim thiet bi | Complete | 100% |
| P3 | Telemetry pipeline | Not started | 0% |
| P4 | Dieu khien actuator | Not started | 0% |
| P5 | Automation engine | Not started | 0% |
| P6 | WPF App | Blocked by P1-P5 | 0% |
| P7 | Cloud, kiem thu va bao cao | Not started | 0% |
| P8 | Mo rong sau MVP | Deferred | 0% |

Toan bo phan ky thuat P0 da hoan thanh. Phan con lai chi la `P0-06` va `P0-07` ve Architecture/Sequence/ERD/Class/Use Case/State diagrams, duoc hoan theo yeu cau hien tai.

P1 da hoan thanh: persistent profile/serial, runtime manifest, TLS MQTT, Last Will, reconnect, Worker routing, database upsert, pairing hash va verification tren Azure SQL.

P2 da hoan thanh: account register/login/refresh/logout, password hashing, JWT token service, rotating refresh token, pairing-code claim, device list/detail/rename/classify/unclaim, ownership authorization va audit log cho auth/device operations.

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

## 3. Chua bat dau

- [ ] Telemetry, command, automation va WPF use cases P3-P6 chua duoc trien khai.
- [ ] Test coverage hien bao phu P0-P2 cot loi; P3-P6 chua co test nghiep vu.
- [ ] Chua xac minh ket noi runtime tu source hien tai toi HiveMQ Cloud, Azure SQL va App Service F1.
- [ ] Chua co diagram artifact hoan chinh.
- [ ] P3-P6 use cases chua duoc trien khai.

## 4. Verification gan nhat

- `dotnet build IoTPlatform.sln --configuration Debug`: thanh cong, 0 warnings, 0 errors.
- `dotnet test IoTPlatform.sln --configuration Debug`: thanh cong; 13 tests passed. `IoTPlatform.Application.Tests` va `IoTPlatform.Api.Tests` hien la scaffold chua co test nen test runner bao `No test is available` nhung exit code thanh cong.
- Tat ca JSON schema va example files parse thanh cong nhu JSON.
- LocalDB integration: tao database rong, apply 2 migrations, apply lai, kiem tra 13 bang, rollback/commit va xoa database test thanh cong.
- P1 cloud integration: Worker ket noi Azure SQL + HiveMQ; ba DeviceApp publish manifest/presence; Last Will offline va reconnect online duoc ghi nhan.
- `dotnet run --project src\IoTPlatform.Worker -- --verify-p1`: thanh cong, doc duoc ba device P1 trong Azure SQL.
- P2 integration tests: auth register/login/refresh/logout, pairing code used/expired/concurrent, device ownership list/detail/update/unclaim va audit log deu pass tren LocalDB.

## 5. Dieu kien mo khoa P3

P3 chi bat dau sau khi:

- P1 discovery da ghi device/capability vao database.
- P2 ownership da dam bao moi telemetry/query deu co ranh gioi user.
- Database migration da co `TelemetryRecords`, `DeviceLatestValues` va `ProcessedMessages`.
- MQTT telemetry contract da co schema/example trong `docs/contracts`.
- Solution build/test thanh cong sau P2.

## 6. Blocker va rui ro hien tai

### Blocker

- Chua co blocker dang chan P3 local development.

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
