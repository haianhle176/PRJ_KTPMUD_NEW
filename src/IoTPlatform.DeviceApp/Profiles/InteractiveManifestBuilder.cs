using IoTPlatform.Domain.Capabilities;
using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Profiles;

public static class InteractiveManifestBuilder
{
    public static Task<DeviceProfile> CreateAsync(string serial)
    {
        Console.WriteLine($"Creating a new device profile. Serial: {serial}");
        var count = ReadInt("Number of capabilities", minimum: 1, maximum: 20);
        var capabilities = new List<CapabilityManifestItem>(count);

        for (var index = 0; index < count; index++)
        {
            Console.WriteLine($"{Environment.NewLine}Capability {index + 1}/{count}");
            var key = ReadRequired("Key (lower_case)");
            var displayName = ReadRequired("Display name");
            var kind = ReadEnum<CapabilityKind>("Kind", [CapabilityKind.Sensor, CapabilityKind.Actuator]);
            var dataType = ReadEnum<CapabilityDataType>("Data type", [CapabilityDataType.Number, CapabilityDataType.Boolean, CapabilityDataType.String]);
            var unit = ReadOptional("Unit (optional)");
            decimal? minValue = null;
            decimal? maxValue = null;

            if (dataType == CapabilityDataType.Number)
            {
                minValue = ReadOptionalDecimal("Minimum value (optional)");
                maxValue = ReadOptionalDecimal("Maximum value (optional)");
            }

            var isWritable = kind == CapabilityKind.Actuator && ReadYesNo("Writable actuator", defaultValue: true);
            var commands = isWritable
                ? ReadRequired("Supported commands, comma separated (example: ON,OFF)")
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(command => command.ToUpperInvariant())
                    .ToArray()
                : [];

            var definition = CapabilityDefinition.Create(
                key,
                displayName,
                kind,
                dataType,
                unit,
                minValue,
                maxValue,
                isWritable,
                commands);
            capabilities.Add(CapabilityManifestItem.FromDefinition(definition));
        }

        return Task.FromResult(new DeviceProfile(serial, 1, capabilities));
    }

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            var value = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("A value is required.");
        }
    }

    private static string? ReadOptional(string prompt)
    {
        Console.Write($"{prompt}: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static decimal? ReadOptionalDecimal(string prompt)
    {
        while (true)
        {
            var value = ReadOptional(prompt);
            if (value is null)
            {
                return null;
            }

            if (decimal.TryParse(value, out var number))
            {
                return number;
            }

            Console.WriteLine("Enter a valid decimal number or leave blank.");
        }
    }

    private static int ReadInt(string prompt, int minimum, int maximum)
    {
        while (true)
        {
            Console.Write($"{prompt} ({minimum}-{maximum}): ");
            if (int.TryParse(Console.ReadLine(), out var value) && value >= minimum && value <= maximum)
            {
                return value;
            }

            Console.WriteLine("Enter a valid number in range.");
        }
    }

    private static T ReadEnum<T>(string prompt, IReadOnlyList<T> values) where T : struct, Enum
    {
        while (true)
        {
            Console.WriteLine(prompt + ":");
            for (var index = 0; index < values.Count; index++)
            {
                Console.WriteLine($"  {index + 1}. {values[index]}");
            }

            if (int.TryParse(Console.ReadLine(), out var choice) && choice >= 1 && choice <= values.Count)
            {
                return values[choice - 1];
            }

            Console.WriteLine("Select a valid option.");
        }
    }

    private static bool ReadYesNo(string prompt, bool defaultValue)
    {
        Console.Write($"{prompt} [{(defaultValue ? "Y/n" : "y/N")}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(value) ? defaultValue : value.Equals("y", StringComparison.OrdinalIgnoreCase);
    }
}
