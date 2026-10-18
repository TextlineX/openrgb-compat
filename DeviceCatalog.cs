internal enum ControllerKind
{
    Keyboard,
    Mouse,
}

internal sealed record DeviceDefinition(
    uint Id,
    string Name,
    string Manufacturer,
    string Description,
    string Serial,
    string Location,
    ControllerKind Kind,
    IReadOnlyList<LedDefinition> Leds,
    string Transport,
    Action<IReadOnlyList<Rgb>>? ApplyColors)
{
    internal string ZoneName => Kind == ControllerKind.Mouse ? "Mouse" : "Keyboard";
}

internal static class DeviceCatalog
{
    internal static IReadOnlyList<DeviceDefinition> Create(
        Zh980Writer? zh980Writer,
        RazerBasiliskWriter? razerWriter = null,
        AsusTufWmiWriter? asusWriter = null)
    {
        return
        [
            new DeviceDefinition(
                0,
                "ZORNHER ZH980 Physical ANSI (OpenRGB)",
                "ZORNHER",
                "ZORNHER ZH980 2.4G keyboard compatibility layer",
                "ZH980-compat-1",
                "HID: VID_05AC&PID_024F&MI_03",
                ControllerKind.Keyboard,
                Zh980Layout.Leds,
                "HID lighting implemented",
                zh980Writer is null ? null : colors => _ = zh980Writer.Apply(colors)),

            new DeviceDefinition(
                1,
                "Razer Basilisk V3 X HyperSpeed (OpenRGB)",
                "Razer",
                "Razer Basilisk V3 X HyperSpeed logical RGB layout",
                "BASILISK-V3X-compat-1",
                "HID: VID_1532&PID_00B9&MI_00",
                ControllerKind.Mouse,
                BasiliskV3XLayout.Leds,
                razerWriter is null ? "Razer HID writer unavailable" : "Razer HID lighting implemented",
                razerWriter is null ? null : colors => _ = razerWriter.Apply(colors)),

            new DeviceDefinition(
                2,
                "ASUS TUF Gaming F16 / Tianxuan 6 Pro FX608LM (OpenRGB)",
                "ASUS",
                "ASUS FX608LM full-size RGB keyboard layout backed by one ITE5570 lamp",
                "FX608LM-compat-1",
                "HID: ITE5570&COL03 LampArray",
                ControllerKind.Keyboard,
                Fx608LmLayout.Leds,
                asusWriter is null ? "ASUS ITE5570 writer unavailable" : "ASUS ITE5570 LampArray lighting implemented",
                asusWriter is null ? null : colors => _ = asusWriter.Apply(colors)),
        ];
    }

    internal static void Validate(IReadOnlyList<DeviceDefinition> devices)
    {
        if (devices.Count != 3)
            throw new InvalidOperationException($"Expected 3 controllers, got {devices.Count}.");

        var ids = devices.Select(device => device.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new InvalidOperationException("Controller IDs must be unique.");

        foreach (var device in devices)
        {
            if (device.Leds.Count == 0)
                throw new InvalidOperationException($"{device.Name} has no layout LEDs.");

            var duplicateNames = device.Leds
                .GroupBy(led => led.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateNames.Length > 0)
                throw new InvalidOperationException(
                    $"{device.Name} has duplicate LED names: {string.Join(", ", duplicateNames)}");
        }
    }

    internal static string SdkName(LedDefinition led)
    {
        if (led.Name.StartsWith("Keyboard_", StringComparison.Ordinal))
        {
            var key = led.Name["Keyboard_".Length..];
            return key switch
            {
                "VolumeDown" => "Key: Volume Down",
                "VolumeUp" => "Key: Volume Up",
                "MicMute" => "Key: Microphone Mute",
                "ArmouryCrate" => "Key: Armoury Crate",
                "Power" => "Key: Power",
                "PrintScreen" => "Key: Print Screen",
                "ScrollLock" => "Key: Scroll Lock",
                "Pause" => "Key: Pause",
                "Home" => "Key: Home",
                "Menu" => "Key: Menu",
                "RightCtrl" => "Key: Right Control",
                _ => Zh980Layout.SdkName(led.Name),
            };
        }

        return led.Name switch
        {
            "Mouse_ScrollWheel" => "LED: Scroll Wheel",
            "Mouse_RazerLogo" => "LED: Razer Logo",
            _ => led.Name,
        };
    }
}

internal static class BasiliskV3XLayout
{
    // OpenRGB's device definition exposes one controllable Chroma region:
    // the scroll wheel. The V3 X HyperSpeed does not expose a separately
    // controllable logo region through this protocol.
    internal static readonly IReadOnlyList<LedDefinition> Leds =
    [
        new("Mouse_ScrollWheel", 0),
    ];
}

internal static class Fx608LmLayout
{
    // FX608LM / Tianxuan 6 Pro: full-size keyboard with numpad, four
    // dedicated top shortcuts, and a separate power key.
    internal static readonly IReadOnlyList<LedDefinition> Leds = Build();

    private static IReadOnlyList<LedDefinition> Build()
    {
        var keys = new List<LedDefinition>();
        var index = 0;

        void Add(params string[] names)
        {
            foreach (var name in names)
                keys.Add(new LedDefinition($"Keyboard_{name}", index++));
        }

        Add("VolumeDown", "VolumeUp", "MicMute", "ArmouryCrate", "Power");
        Add("Escape", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12");
        Add("PrintScreen", "ScrollLock", "Pause", "Insert", "Home", "PageUp");

        Add("GraveAccentAndTilde", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
            "MinusAndUnderscore", "EqualsAndPlus", "Backspace");
        Add("NumLock", "NumSlash", "NumAsterisk", "NumMinus");

        Add("Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "BracketLeft", "BracketRight", "Backslash");
        Add("Num7", "Num8", "Num9", "NumPlus");

        Add("CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", "SemicolonAndColon",
            "ApostropheAndDoubleQuote", "Enter");
        Add("Num4", "Num5", "Num6");

        Add("LeftShift", "Z", "X", "C", "V", "B", "N", "M", "CommaAndLessThan",
            "PeriodAndBiggerThan", "SlashAndQuestionMark", "RightShift", "ArrowUp");
        Add("Num1", "Num2", "Num3", "NumEnter");

        Add("LeftCtrl", "LeftGui", "LeftAlt", "Space", "RightAlt", "Menu", "RightCtrl",
            "ArrowLeft", "ArrowDown", "ArrowRight");
        Add("Num0", "NumPeriodAndDelete");

        return keys;
    }
}
