# openrgb-compat

OpenRGB/Artemis compatibility layer for the user's three lighting devices.

## Current support

- ZH980 2.4G mode through the keyboard's HID lighting interface (`MI_03`)
- OpenRGB SDK server on `127.0.0.1:6742`
- 97 physical keys, including the full numpad
- Razer Basilisk V3 X HyperSpeed HID lighting (`MI_00`, one scroll-wheel RGB zone)
- ASUS TUF Gaming F16 / 天选 6 Pro `FX608LM` keyboard layout (105 logical keys)
- ASUS Aura service output for the laptop's four hardware keyboard zones
- One SDK controller for each of the three devices, so Artemis can keep them separate
- Direct RGB updates from Artemis/OpenRGB-compatible clients for all three devices

The FX608LM exposes a four-zone hardware keyboard through the installed ASUS Aura
service. The compatibility layer keeps 105 logical keys for Artemis layout editing,
then averages each quarter into one hardware zone. The ATKACPI/WMI path remains as a
firmware-variant fallback.

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
to choose another OpenRGB SDK port.

## Scope

This is an independent compatibility implementation based on observed ZH980 HID
behavior and public device information. It is not the manufacturers' drivers and
does not include any manufacturer's software or binaries.
