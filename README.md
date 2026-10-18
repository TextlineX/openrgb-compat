# openrgb-compat

OpenRGB/Artemis compatibility layer for the ZORNHER ZH980 RGB keyboard.

## Current support

- ZH980 2.4G mode through the keyboard's HID lighting interface (`MI_03`)
- OpenRGB SDK server on `127.0.0.1:6742`
- 97 physical keys, including the full numpad
- Direct RGB updates from Artemis/OpenRGB-compatible clients

## Build and run

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build -c Release
.\bin\Release\net8.0-windows\ZH980Compat.exe
```

Start the compatibility executable before starting Artemis. Artemis can then discover:

`ZORNHER ZH980 Physical ANSI (OpenRGB)`

## Scope

This is an independent compatibility implementation based on observed ZH980 HID behavior. It is not the manufacturer's driver and does not include the manufacturer's software or binaries.
