# Local Configuration

Worker va DeviceApp chay local. Khong commit SQL password hoac HiveMQ credential vao repository.

## Worker

```powershell
dotnet user-secrets set "ConnectionStrings:IoTPlatform" "<AZURE_SQL_CONNECTION_STRING>" --project src/IoTPlatform.Worker
dotnet user-secrets set "HiveMq:Username" "<HIVEMQ_USERNAME>" --project src/IoTPlatform.Worker
dotnet user-secrets set "HiveMq:Password" "<HIVEMQ_PASSWORD>" --project src/IoTPlatform.Worker
```

Host `f1f0f565b8564081b04217852589bb65.s1.eu.hivemq.cloud` va port `8883` da nam trong `appsettings.json` vi khong phai secret.

## DeviceApp

```powershell
dotnet user-secrets set "HiveMq:Username" "<HIVEMQ_USERNAME>" --project src/IoTPlatform.DeviceApp
dotnet user-secrets set "HiveMq:Password" "<HIVEMQ_PASSWORD>" --project src/IoTPlatform.DeviceApp
```

Chay nhieu thiet bi bang profile khac nhau:

```powershell
dotnet run --project src/IoTPlatform.DeviceApp -- --profile garden-pump
dotnet run --project src/IoTPlatform.DeviceApp -- --profile living-room
```

Moi profile co serial va manifest rieng tai `%LOCALAPPDATA%\OmniConnect\DeviceApp\<profile>`.
De thay doi capability ma van giu serial, chay them `--reconfigure`; DeviceApp se tang `manifestVersion`:

```powershell
dotnet run --project src/IoTPlatform.DeviceApp -- --profile garden-pump --reconfigure
```

## Worker startup

```powershell
dotnet run --project src/IoTPlatform.Worker
```

Worker tu chay database migrations, sau do ket noi HiveMQ va subscribe manifest/presence. Azure SQL firewall phai cho phep IP hien tai cua laptop.

## P1 verification

Sau khi chay Worker va cac DeviceApp, co the kiem tra ba profile P1 trong Azure SQL:

```powershell
dotnet run --project src/IoTPlatform.Worker -- --verify-p1
```

Lenh nay chi in serial, status, manifest version va so active capability; khong in credential.
