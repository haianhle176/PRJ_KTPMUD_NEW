# Smoke Test Checklist

Use this checklist before recording or presenting the project.

## Cloud

- [ ] API `/health/live` returns `Healthy`.
- [ ] API `/health/ready` returns `Healthy`.
- [ ] Azure App Service has JWT settings configured.
- [ ] Azure App Service has `ConnectionStrings__IoTPlatform` configured.
- [ ] Azure App Service has `Worker__Enabled=true`.
- [ ] Azure App Service has HiveMQ settings configured.
- [ ] Azure SQL firewall allows the App Service and the current laptop.

## Local Runtime

- [ ] DeviceApp connects to HiveMQ and prints a pairing code.
- [ ] WPF starts and can login/register through Azure API.

## End-To-End Flow

- [ ] WPF can claim the DeviceApp using pairing code.
- [ ] Claimed device appears in device list after refresh.
- [ ] Telemetry latest/history updates from DeviceApp sensor.
- [ ] Manual `set temperature 35` appears in WPF telemetry.
- [ ] WPF command `power ON true` reaches DeviceApp.
- [ ] DeviceApp logs command execution.
- [ ] Automation rule can be created.
- [ ] Automation execution creates a command when threshold is crossed.

## Known MVP Limitations

- WPF capability and command fields are typed manually.
- WPF command history is basic.
- API on Azure F1 may cold start.
- API-hosted Worker requires the Azure API process to be awake for MQTT, command dispatch and automation.
- DeviceApp profiles are local files under the Windows user profile.
