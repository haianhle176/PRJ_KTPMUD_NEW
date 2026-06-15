# REST Contract v1

## Conventions

- Base path: `/api/v1`.
- JSON property naming: camelCase.
- Authentication: `Authorization: Bearer {accessToken}`.
- Timestamp: ISO 8601 UTC ending in `Z`.
- Resource ID: UUID string.
- Create returns `201 Created`; successful read/update returns `200 OK`; delete/revoke may return `204 No Content`.
- Validation errors return `400`; missing/invalid token `401`; ownership failure `404` to avoid resource enumeration; conflict `409`.

## Error envelope

```json
{
  "type": "https://iot-platform.local/problems/validation-error",
  "title": "Request validation failed",
  "status": 400,
  "code": "validation_error",
  "traceId": "00-699e...-01",
  "errors": {
    "displayName": ["displayName is required"]
  }
}
```

All API errors use RFC 7807-compatible problem details with stable machine-readable `code`.

## Pagination and filtering

Request:

```text
GET /api/v1/devices/{deviceId}/telemetry?page=1&pageSize=50&fromUtc=2026-06-01T00:00:00Z&toUtc=2026-06-15T23:59:59Z&capabilityKey=temperature&sort=recordedAtUtc:desc
```

Rules:

- `page` starts at 1; default 1.
- `pageSize` default 50, minimum 1, maximum 200.
- Unsupported sort/filter field returns `400`.
- Stable secondary sort uses resource ID.

Response:

```json
{"items":[],"page":1,"pageSize":50,"totalItems":0,"totalPages":0}
```

## Endpoint contracts

| Method and path | Request | Success | Common errors |
| --- | --- | --- | --- |
| `POST /auth/register` | email, password, displayName | 201 user + tokens | 400, 409 |
| `POST /auth/login` | email, password | 200 user + tokens | 400, 401 |
| `POST /auth/refresh` | refreshToken | 200 rotated tokens | 401 |
| `POST /auth/logout` | refreshToken | 204 | 401 |
| `POST /devices/claim` | pairingCode, displayName | 201 device | 400, 404, 409 |
| `GET /devices` | page/filter/sort | 200 paged devices | 400, 401 |
| `GET /devices/{id}` | none | 200 device detail | 401, 404 |
| `PATCH /devices/{id}` | displayName, category | 200 device | 400, 404 |
| `DELETE /devices/{id}/claim` | none | 204 | 404, 409 |
| `GET /devices/{id}/latest-values` | none | 200 values | 404 |
| `GET /devices/{id}/telemetry` | paging/time filters | 200 page | 400, 404 |
| `POST /devices/{id}/commands` | capabilityKey, commandName, desiredValue | 202 command | 400, 404, 409 |
| `GET /commands/{id}` | none | 200 command | 404 |
| `GET /automation-rules` | page/enabled | 200 page | 400 |
| `POST /automation-rules` | rule definition | 201 rule | 400, 404, 409 |
| `PUT /automation-rules/{id}` | complete rule definition | 200 rule | 400, 404, 409 |
| `PATCH /automation-rules/{id}/enabled` | enabled | 200 rule | 400, 404 |
| `DELETE /automation-rules/{id}` | none | 204 | 404 |
| `GET /automation-rules/{id}/executions` | paging/time filters | 200 page | 400, 404 |

All paths in the table are relative to `/api/v1`. Request/response DTOs will be represented by Application contracts; WPF consumes only these public DTOs, never persistence models.

