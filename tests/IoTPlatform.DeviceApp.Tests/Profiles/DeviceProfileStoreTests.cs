using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.Domain.Capabilities;
using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Tests.Profiles;

public sealed class DeviceProfileStoreTests
{
    [Fact]
    public async Task LoadOrCreateAsync_PersistsSerialAndManifestPerProfile()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"OmniConnect_ProfileTest_{Guid.NewGuid():N}");

        try
        {
            var store = new DeviceProfileStore(rootDirectory);
            var createCalls = 0;
            Task<DeviceProfile> Create(string serial)
            {
                createCalls++;
                var capability = CapabilityDefinition.Create(
                    "temperature",
                    "Temperature",
                    CapabilityKind.Sensor,
                    CapabilityDataType.Number,
                    "C",
                    -20,
                    80);
                return Task.FromResult(new DeviceProfile(serial, 1, [CapabilityManifestItem.FromDefinition(capability)]));
            }

            var first = await store.LoadOrCreateAsync("garden-pump", Create);
            var second = await store.LoadOrCreateAsync("garden-pump", Create);
            var otherProfile = await store.LoadOrCreateAsync("living-room", Create);
            var reconfigured = await store.LoadOrCreateAsync("garden-pump", Create, reconfigure: true);

            Assert.Equal(3, createCalls);
            Assert.Equal(first.Serial, second.Serial);
            Assert.NotEqual(first.Serial, otherProfile.Serial);
            Assert.Equal("temperature", second.Capabilities.Single().Key);
            Assert.Equal(first.Serial, reconfigured.Serial);
            Assert.Equal(2, reconfigured.ManifestVersion);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
