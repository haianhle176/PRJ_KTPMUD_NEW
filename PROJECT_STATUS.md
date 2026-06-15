# Project Status

**Cap nhat lan cuoi:** 2026-06-15  
**Trang thai tong the:** P0 technical foundation complete; diagrams deferred  
**Phase hien tai:** P1 ready to start  
**Muc tieu tiep theo:** Device identity va runtime capability manifest

## 1. Tien do phase

| Phase | Noi dung | Trang thai | Tien do |
| --- | --- | --- | --- |
| P0 | Dac ta va nen mong | Technical work complete; diagrams deferred | 90% |
| P1 | Device identity va capability discovery | Not started | 0% |
| P2 | Tai khoan va claim thiet bi | Not started | 0% |
| P3 | Telemetry pipeline | Not started | 0% |
| P4 | Dieu khien actuator | Not started | 0% |
| P5 | Automation engine | Not started | 0% |
| P6 | WPF App | Blocked by P1-P5 | 0% |
| P7 | Cloud, kiem thu va bao cao | Not started | 0% |
| P8 | Mo rong sau MVP | Deferred | 0% |

Toan bo phan ky thuat P0 da hoan thanh. Phan con lai chi la `P0-06` va `P0-07` ve Architecture/Sequence/ERD/Class/Use Case/State diagrams, duoc hoan theo yeu cau hien tai.

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

## 3. Chua bat dau

- [ ] DeviceApp, API, Worker va WPF moi o muc scaffold, chua co use case P1-P6.
- [ ] Test coverage hien bao phu capability invariant va database foundation; P1-P6 chua co test.
- [ ] Chua cau hinh HiveMQ Cloud, Azure SQL va Azure App Service F1.
- [ ] Chua co diagram artifact hoan chinh.

## 4. Verification gan nhat

- `dotnet build IoTPlatform.sln --configuration Debug`: thanh cong, 0 warnings, 0 errors.
- `dotnet test IoTPlatform.sln --configuration Debug --no-build`: thanh cong, 5 tests passed.
- Tat ca JSON schema va example files parse thanh cong nhu JSON.
- LocalDB integration: tao database rong, apply 2 migrations, apply lai, kiem tra 13 bang, rollback/commit va xoa database test thanh cong.

## 5. Dieu kien mo khoa P1

P1 chi bat dau sau khi:

- MQTT topics, QoS, retain va payload schema duoc chot.
- REST error format, pagination va authentication contract duoc chot.
- Database schema contract da duoc review; diagram P0 duoc phep hoan.
- Solution build thanh cong tren may cua tat ca thanh vien.
- Database khoi tao duoc tu migration script tren database rong.
- Configuration template khong chua secret da san sang.

## 6. Blocker va rui ro hien tai

### Blocker

- Chua co thong tin HiveMQ Cloud cluster va credential. Day se chan integration MQTT o P1.
- Chua co Azure SQL connection/environment. LocalDB van dung cho test; Azure SQL can cho integration theo topology da chot.

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
