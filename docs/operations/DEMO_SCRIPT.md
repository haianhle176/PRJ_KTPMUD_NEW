# Demo Script

## 1. Wake And Check Cloud API

Open these URLs before presenting:

```text
https://omniconnect-b5gmdffba4a4dkeq.southeastasia-01.azurewebsites.net/health/live
https://omniconnect-b5gmdffba4a4dkeq.southeastasia-01.azurewebsites.net/health/ready
```

Expected result:

```text
Healthy
```

`/health/ready` confirms that the API can reach Azure SQL.

## 2. Start Worker

```powershell
dotnet run --project src\IoTPlatform.Worker
```

Expected:

```text
Database migrations are current.
Connected to HiveMQ ...
```

## 3. Start DeviceApp

```powershell
dotnet run --project src\IoTPlatform.DeviceApp -- --profile demo-device
```

If the profile is not clean, recreate it:

```powershell
dotnet run --project src\IoTPlatform.DeviceApp -- --profile demo-device --reconfigure
```

Create:

```text
temperature = sensor number C 0..100
power = actuator boolean writable commands ON,OFF
```

Copy the pairing code printed by DeviceApp.

## 4. Start WPF

```powershell
dotnet run --project src\IoTPlatform.Wpf
```

Demo flow:

1. Register or login.
2. Claim device with the pairing code.
3. Rename/classify the device.
4. Select the device.
5. Open Telemetry and verify `temperature`.
6. In DeviceApp, manually publish:

```text
set temperature 35
```

7. Open Commands and send:

```text
Capability key: power
Command name: ON
Desired value: true
```

8. Confirm DeviceApp logs the command.
9. Create automation:

```text
IF temperature > 30
THEN power ON true
```

10. Publish telemetry above threshold again and verify automation execution.

## 5. Disconnect/Reconnect Test

Stop DeviceApp with `Ctrl+C`.

Expected Worker log:

```text
Device <serial> is offline.
```

Start DeviceApp again.

Expected Worker log:

```text
Registered manifest ...
Device <serial> is online.
```
