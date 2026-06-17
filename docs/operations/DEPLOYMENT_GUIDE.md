# Deployment Guide

## Topology

```text
WPF (local) -> Web API (Azure App Service F1) -> Azure SQL
DeviceApp (local) <-> HiveMQ Cloud <-> API-hosted Worker -> Azure SQL
```

## Azure Web API

- Runtime: .NET 8 LTS.
- App Service OS: Windows.
- Pricing tier: Free F1.
- Public URL: `https://omniconnect-b5gmdffba4a4dkeq.southeastasia-01.azurewebsites.net/`.
- F1 can cold start. Open `/health/live` before a demo to wake the API.

Required App Service configuration:

```text
ASPNETCORE_ENVIRONMENT=Production
Jwt__Issuer=IoTPlatform.Api
Jwt__Audience=IoTPlatform.Clients
Jwt__SigningKey=<at-least-32-character-secret>
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=30
ConnectionStrings__IoTPlatform=<Azure SQL connection string>
Worker__Enabled=true
HiveMq__Host=f1f0f565b8564081b04217852589bb65.s1.eu.hivemq.cloud
HiveMq__Port=8883
HiveMq__Username=<HiveMQ credential username>
HiveMq__Password=<HiveMQ credential password>
HiveMq__ClientIdPrefix=api-worker
```

Health endpoints:

```text
GET /health       checks all registered health checks
GET /health/live  checks that the API process is alive
GET /health/ready checks dependencies required for requests, currently Azure SQL
```

## API-Hosted Worker

The Worker runs inside the Web API process when `Worker__Enabled=true`. It uses the same Azure SQL database as the API, connects to HiveMQ Cloud, subscribes to device messages, dispatches pending commands, and runs operations maintenance.

Required App Service configuration:

```text
Worker__Enabled=true
ConnectionStrings:IoTPlatform=<Azure SQL connection string>
HiveMq:Host=f1f0f565b8564081b04217852589bb65.s1.eu.hivemq.cloud
HiveMq:Port=8883
HiveMq:Username=<HiveMQ credential username>
HiveMq:Password=<HiveMQ credential password>
HiveMq:ClientIdPrefix=api-worker
```

Expected log:

```text
Database migrations are current.
Connected to HiveMQ ...
Registered manifest ...
Telemetry ...
```

For local API development, leave `Worker:Enabled` as `false` unless you intentionally want the API process to connect to HiveMQ.

## DeviceApp Local

Run the multi-device simulator:

```powershell
dotnet run --project src\IoTPlatform.DeviceApp
```

Use `Add device` to create one or more SmartHome preset devices, then `Start` each device independently. Each simulated device has its own serial, pairing code, MQTT session, manifest, telemetry loop, and command subscription.

## WPF Local

Run:

```powershell
dotnet run --project src\IoTPlatform.Wpf
```

The WPF client is configured to call the Azure Web API URL.
