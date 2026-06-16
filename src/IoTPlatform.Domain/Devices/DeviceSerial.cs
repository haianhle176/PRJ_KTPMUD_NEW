using System.Text.RegularExpressions;

namespace IoTPlatform.Domain.Devices;

public static partial class DeviceSerial
{
    public static string Create() => $"DEV-{Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12]}";

    public static bool IsValid(string serial) => SerialRegex().IsMatch(serial);

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{7,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SerialRegex();
}
