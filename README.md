# openrgb-compat

OpenRGB/Artemis compatibility layer for the user's three lighting devices.

## Current support

- ZH980 2.4G mode through the keyboard's 33-byte HID OUTPUT report (`MI_03`)
- OpenRGB SDK server on `127.0.0.1:6742`
- 97 physical keys, including the full numpad
- Razer Basilisk V3 X HyperSpeed HID lighting (`MI_00`, one scroll-wheel RGB zone)
- ASUS TUF Gaming F16 / 天选 6 Pro `FX608LM` keyboard layout (105 logical keys)
- ASUS FX608LM ITE5570 HID LampArray output (single physical zone)
- One SDK controller for each of the three devices, so Artemis can keep them separate
- Direct RGB updates from Artemis/OpenRGB-compatible clients for all three devices

The ZH980 2.4G receiver uses the one-packet `05 10` static-RGB command with
`R,G,B` at offsets 5–7. The FX608LM's ITE5570 controller is exposed by Windows as
an HID LampArray with one physical lamp. The compatibility layer keeps 105 logical
keys for Artemis layout editing, collapses their color to that physical zone, sends
the standard feature reports, and keeps the installed Aura service and ATKACPI/WMI
paths as fallbacks.

## Build and run

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build -c Release
.\bin\Release\net8.0-windows\ZH980Compat.exe
```

Verify the registered layouts without opening hardware:

```powershell
dotnet run -c Release -- --verify-layouts
```

Start the compatibility executable before starting Artemis. Artemis can then discover:

`ZORNHER ZH980 Physical ANSI (OpenRGB)`

`Razer Basilisk V3 X HyperSpeed (OpenRGB)`

`ASUS TUF Gaming F16 / 天选 6 Pro FX608LM (OpenRGB)`

Use `--probe` to check the local HID and ASUS service paths. Use `--port <number>`
to choose another OpenRGB SDK port. Use `--demo-all-green` for two short green
hardware flashes across all three devices.

## Scope

This is an independent compatibility implementation based on observed ZH980 HID
behavior and public device information. It is not the manufacturers' drivers and
does not include any manufacturer's software or binaries.
