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

## 2. Start DeviceApp

```powershell
dotnet run --project src\IoTPlatform.DeviceApp
```

In the simulator:

1. Choose a preset device type.
2. Click `Add device`.
3. Click `Start` on that device card.
4. Copy the pairing code shown on the card.

## 3. Start WPF

```powershell
dotnet run --project src\IoTPlatform.Wpf
```

Demo flow:

1. Register or login.
2. Claim the available device with the pairing code.
3. Rename/classify the device.
4. Select the device.
5. Open Telemetry and verify sensor values if the selected preset is a sensor.
6. Open Commands and send a command if the selected preset is an actuator:

```text
Capability key: power
Command name: ON
Desired value: true
```

7. Confirm DeviceApp logs the command.
8. Create automation:

```text
IF temperature > 30
THEN power ON true
```

9. Use a sensor preset and verify automation execution when telemetry crosses the threshold.

## 4. Disconnect/Reconnect Test

Click `Stop` on the simulated device card, or close DeviceApp.

Expected API-hosted Worker log:

```text
Device <serial> is offline.
```

Click `Start` on the simulated device card again.

Expected API-hosted Worker log:

```text
Registered manifest ...
Device <serial> is online.
```
