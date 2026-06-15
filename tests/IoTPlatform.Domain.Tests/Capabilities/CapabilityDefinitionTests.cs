using IoTPlatform.Domain.Capabilities;

namespace IoTPlatform.Domain.Tests.Capabilities;

public sealed class CapabilityDefinitionTests
{
    [Fact]
    public void Create_RejectsWritableSensor()
    {
        var action = () => CapabilityDefinition.Create(
            "temperature",
            "Temperature",
            CapabilityKind.Sensor,
            CapabilityDataType.Number,
            isWritable: true);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Create_AcceptsWritableBooleanActuatorWithCommands()
    {
        var capability = CapabilityDefinition.Create(
            "pump_power",
            "Pump Power",
            CapabilityKind.Actuator,
            CapabilityDataType.Boolean,
            isWritable: true,
            supportedCommands: ["ON", "OFF"]);

        Assert.Equal("pump_power", capability.Key);
        Assert.Equal(["ON", "OFF"], capability.SupportedCommands);
    }
}
