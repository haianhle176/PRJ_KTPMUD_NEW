# Glossary

| Term | Definition |
| --- | --- |
| Device | Mot DeviceApp instance co serial ben vung, ket noi broker va khai bao capability. |
| Serial | Dinh danh public duy nhat cua device, do DeviceApp tao mot lan va luu ben vung. |
| Capability | Kha nang dong cua device, duoc mo ta trong runtime manifest. |
| Sensor | Capability chi doc, publish telemetry. |
| Actuator | Capability co the nhan command va publish reported state. |
| Manifest | Danh sach capability hien tai cua device, co `manifestVersion` tang dan. |
| Telemetry | Gia tri quan sat theo thoi gian do sensor publish. |
| Desired state | Gia tri backend yeu cau actuator dat toi. |
| Reported state | Gia tri actuator bao cao sau khi xu ly command. |
| Command | Yeu cau thay doi mot writable capability, co `commandId` duy nhat. |
| ACK | Phan hoi cua device cho command: `accepted`, `completed` hoac `failed`. |
| Presence | Trang thai ket noi `online`/`offline`; offline duoc publish bang Last Will. |
| Pairing code | Ma mot lan, het han sau 10 phut, dung de user claim device. |
| Claim | Gan ownership cua device chua co chu cho mot user da xac thuc. |
| Automation rule | Mot dieu kien sensor va mot hanh dong actuator cua cung owner. |
| Automation execution | Ban ghi ket qua danh gia rule: triggered, skipped hoac failed. |
| Message ID | UUID dung de xu ly MQTT QoS 1 theo cach idempotent. |
| Schema version | Phien ban contract payload; MVP dung `1.0`. |

## Invariants

- Serial khong thay doi khi DeviceApp restart.
- Capability key la duy nhat trong mot device va khong duoc tai su dung voi y nghia khac.
- Timestamp truyen tren wire la ISO 8601 UTC, vi du `2026-06-15T10:30:00Z`.
- Dynamic value chi nhan JSON number, boolean hoac string; `null`, object va array khong hop le.
- Sensor khong writable. Actuator writable phai khai bao it nhat mot supported command.
- Moi truy van user-owned resource phai loc theo authenticated user ID.

