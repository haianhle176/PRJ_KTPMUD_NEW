# MQTT Contract v1

## Topic policy

| Topic | Publisher | Subscriber | QoS | Retain |
| --- | --- | --- | --- | --- |
| `iot/devices/{serial}/manifest` | DeviceApp | Worker | 1 | Yes |
| `iot/devices/{serial}/telemetry` | DeviceApp | Worker | 1 | No |
| `iot/devices/{serial}/state` | DeviceApp | Worker | 1 | Yes |
| `iot/devices/{serial}/commands` | Worker | DeviceApp | 1 | No |
| `iot/devices/{serial}/commands/ack` | DeviceApp | Worker | 1 | No |
| `iot/devices/{serial}/presence` | DeviceApp/Last Will | Worker | 1 | Yes |

Topic serial va payload serial phai trung nhau. Worker reject message neu schema, topic identity, capability type hoac timestamp khong hop le. QoS 1 cho phep duplicate delivery, vi vay moi handler phai idempotent theo `messageId`; command con idempotent theo `commandId`.

## Valid examples

### Manifest

```json
{"messageId":"8a9853de-1037-4e25-85d2-976d53eb8e77","serial":"DEV-A1B2C3D4","timestampUtc":"2026-06-15T10:30:00Z","schemaVersion":"1.0","manifestVersion":1,"pairingCode":"ABCD2345","capabilities":[{"key":"temperature","displayName":"Temperature","kind":"sensor","dataType":"number","unit":"C","minValue":-20,"maxValue":80,"isWritable":false,"supportedCommands":[]},{"key":"pump_power","displayName":"Pump Power","kind":"actuator","dataType":"boolean","unit":null,"minValue":null,"maxValue":null,"isWritable":true,"supportedCommands":["ON","OFF"]}]}
```

### Telemetry

```json
{"messageId":"69564c72-3785-4684-b66e-321f4d3bd7f0","serial":"DEV-A1B2C3D4","timestampUtc":"2026-06-15T10:31:00Z","schemaVersion":"1.0","readings":[{"capabilityKey":"temperature","value":31.5}]}
```

### Command and ACK

```json
{"messageId":"9ca173ae-8bf5-492f-8f87-669955c2f10f","serial":"DEV-A1B2C3D4","timestampUtc":"2026-06-15T10:32:00Z","schemaVersion":"1.0","commandId":"347a0671-a620-4ffb-91ee-7ea84e985236","capabilityKey":"pump_power","commandName":"ON","desiredValue":true,"expiresAtUtc":"2026-06-15T10:32:30Z"}
```

```json
{"messageId":"1c23f12d-050b-4c98-a96f-38c6430ebf31","serial":"DEV-A1B2C3D4","timestampUtc":"2026-06-15T10:32:01Z","schemaVersion":"1.0","commandId":"347a0671-a620-4ffb-91ee-7ea84e985236","status":"completed","reportedValue":true,"errorCode":null,"errorMessage":null}
```

## Invalid examples

- Manifest co hai capability trung `key` (business validation reject).
- Pairing code khong gom dung 8 ky tu `A-Z`/`2-9` (schema reject).
- Sensor co `isWritable: true` (business validation reject).
- Telemetry gui object/array/null lam `value` (schema reject).
- Topic serial khac payload serial (identity validation reject).
- Command het `expiresAtUtc`, capability khong writable hoac sai data type (command validation reject).
- ACK cho command khong ton tai hoac state transition khong hop le (state validation reject).
