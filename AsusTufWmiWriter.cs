using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class AsusTufWmiWriter : IDisposable
{
    private const string AuraDeviceName = "WDL_NB_KB_4ZONE_RGB_LIGHTING";
    private const string IteLampInterface = "ite5570&col03";
    private const byte LampRangeUpdateReportId = 0x05;
    private const byte LampArrayControlReportId = 0x0B;
    private const byte LampFeatureManualMode = 0x00;
    private const uint IoControlCode = 0x0022240C;
    private const uint DeviceSetCommand = 0x53564544;
    private const uint Fx608LmRgbMode = 0x00100066;
    private const uint TufRgbMode = 0x00100056;
    private const uint TufRgbModeFallback = 0x0010005A;
    private const byte SaveValue = 0xB4;
    private const byte FallbackValue = 0xB3;
    private const byte StaticMode = 0x00;
    private const byte NormalSpeed = 0x02;

    private readonly object _sync = new();
    private SafeFileHandle? _handle;
    private SafeFileHandle? _lampHandle;
    private int _lampFeatureReportLength;
    private dynamic? _auraSdk;
    private dynamic? _auraService;
    private int _successfulApplies;

    internal bool IsOpen => _lampHandle is { IsInvalid: false } ||
                            _auraSdk is not null ||
                            _auraService is not null ||
                            _handle is { IsInvalid: false };

    internal void TryOpen()
    {
        lock (_sync)
        {
            if (IsOpen)
                return;

            TryOpenIteLampArray();
            if (_lampHandle is { IsInvalid: false })
                return;

            TryOpenAuraSdk();
            if (_auraSdk is not null)
                return;

            TryOpenAuraService();
            if (_auraService is not null)
                return;

            TryOpenAtkAcpi();
        }
    }

    private void TryOpenAtkAcpi()
    {
        lock (_sync)
        {
            if (_handle is { IsInvalid: false })
                return;

            var candidate = Native.CreateFile(
                "\\\\.\\ATKACPI",
                Native.GenericRead | Native.GenericWrite,
                Native.FileShareRead | Native.FileShareWrite,
                IntPtr.Zero,
                Native.OpenExisting,
                0,
                IntPtr.Zero);

            if (candidate.IsInvalid)
            {
                candidate.Dispose();
                Console.WriteLine("ASUS TUF ATKACPI is not open; controller will stay available and retry on the next color update.");
                return;
            }

            _handle = candidate;
            Console.WriteLine("ASUS TUF ATKACPI opened for FX608LM WMI lighting.");
        }
    }

    internal bool Apply(IReadOnlyList<Rgb> colors)
    {
        lock (_sync)
        {
            if (!IsOpen)
                TryOpen();
            if (!IsOpen)
                return false;

            if (_lampHandle is { IsInvalid: false })
            {
                if (ApplyIteLampArray(colors))
                    return true;

                _lampHandle.Dispose();
                _lampHandle = null;
                _lampFeatureReportLength = 0;
            }

            if (_auraSdk is not null)
            {
                if (ApplyAuraSdk(colors))
                    return true;

                TryReleaseAuraSdk();
            }

            if (_auraService is not null)
            {
                if (ApplyAuraZones(colors))
                    return true;

                TryOpenAtkAcpi();
            }

            // FX608LM exposes a single TUF keyboard RGB zone through ATKACPI.
            // Artemis may send the 105-key visual layout, so collapse it to
            // the zone color instead of silently using only one key.
            var color = Average(colors);
            var setting = new byte[]
            {
                SaveValue,
                StaticMode,
                color.R,
                color.G,
                color.B,
                NormalSpeed,
            };

            // ASUS has shipped two DEVS payload formats. The native OpenRGB
            // controller uses the legacy three-DWORD form; G-Helper uses the
            // variable-length form. Try the native form first, then the
            // model-specific and generic variable-length forms.
            var success = DeviceSetLegacy(Fx608LmRgbMode, setting);
            if (!success)
                success = DeviceSetLegacy(TufRgbMode, setting);
            if (!success)
                success = DeviceSet(Fx608LmRgbMode, setting);
            if (!success)
                success = DeviceSet(TufRgbMode, setting);
            if (!success)
            {
                // Newer ASUS firmware exposes the same TUF RGB endpoint
                // under 0x0010005A and accepts the legacy B3/B4 sequence.
                setting[0] = FallbackValue;
                _ = DeviceSet(TufRgbModeFallback, setting);
                setting[0] = SaveValue;
                success = DeviceSet(TufRgbModeFallback, setting);
            }
            if (success && Interlocked.Increment(ref _successfulApplies) <= 3)
                Console.WriteLine($"ASUS FX608LM WMI lighting update applied ({color.R},{color.G},{color.B})");
            return success;
        }
    }

    private void TryOpenIteLampArray()
    {
        foreach (var path in Zh980Writer.EnumerateHidPaths().Where(path =>
                     path.Contains(IteLampInterface, StringComparison.OrdinalIgnoreCase)))
        {
            var candidate = Native.CreateFile(
                path,
                Native.GenericRead | Native.GenericWrite,
                Native.FileShareRead | Native.FileShareWrite,
                IntPtr.Zero,
                Native.OpenExisting,
                0,
                IntPtr.Zero);

            if (candidate.IsInvalid)
            {
                candidate.Dispose();
                continue;
            }

            if (!Native.HidD_GetPreparsedData(candidate, out var preparsed) || preparsed == IntPtr.Zero)
            {
                candidate.Dispose();
                continue;
            }

            try
            {
                if (Native.HidP_GetCaps(preparsed, out var caps) != Native.HidpStatusSuccess ||
                    caps.FeatureReportByteLength < 10)
                {
                    candidate.Dispose();
                    continue;
                }

                _lampHandle = candidate;
                _lampFeatureReportLength = caps.FeatureReportByteLength;
                Console.WriteLine($"ASUS ITE5570 LampArray opened: {path}; feature={_lampFeatureReportLength}");
                return;
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }
    }

    private bool ApplyIteLampArray(IReadOnlyList<Rgb> colors)
    {
        if (_lampHandle is null || _lampHandle.IsInvalid || _lampFeatureReportLength < 10)
            return false;

        var color = Average(colors);
        var control = new byte[_lampFeatureReportLength];
        control[0] = LampArrayControlReportId;
        control[1] = LampFeatureManualMode;
        if (!Native.HidD_SetFeature(_lampHandle, control, control.Length))
        {
            Console.WriteLine($"ASUS ITE5570 LampArray control failed: Win32={Marshal.GetLastWin32Error()}");
            return false;
        }

        // FX608LM's standard LampArray descriptor uses report 5 for a range
        // update and report 0x0B for the autonomous/manual control report.
        // It exposes one lamp, so the inclusive range is lamp 0..0.
        var range = new byte[_lampFeatureReportLength];
        range[0] = LampRangeUpdateReportId;
        range[1] = 0x01;
        range[2] = 0x00;
        range[3] = 0x00;
        range[4] = 0x00;
        range[5] = 0x00;
        range[6] = color.R;
        range[7] = color.G;
        range[8] = color.B;
        range[9] = 0xFF;
        if (!Native.HidD_SetFeature(_lampHandle, range, range.Length))
        {
            Console.WriteLine($"ASUS ITE5570 LampArray color failed: Win32={Marshal.GetLastWin32Error()}");
            return false;
        }

        if (Interlocked.Increment(ref _successfulApplies) <= 3)
            Console.WriteLine($"ASUS FX608LM ITE5570 LampArray update applied ({color.R},{color.G},{color.B})");
        return true;
    }

    private void TryOpenAuraService()
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("AsRogAuraService.ServiceMediator.1");
            if (serviceType is null)
                return;

            dynamic? service = Activator.CreateInstance(serviceType);
            if (service is null)
                return;

            service.Acquire_MatrixControl(AuraDeviceName, AuraDeviceName, 1);
            _auraService = service;
            Console.WriteLine("ASUS Aura service opened for FX608LM 4-zone keyboard.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ASUS Aura service unavailable; falling back to ATKACPI: {ex.Message}");
            _auraService = null;
        }
    }

    private void TryOpenAuraSdk()
    {
        try
        {
            var sdkType = Type.GetTypeFromProgID("aura.sdk.1");
            if (sdkType is null)
                return;

            dynamic? sdk = Activator.CreateInstance(sdkType);
            if (sdk is null)
                return;

            sdk.SwitchMode();
            dynamic devices = sdk.Enumerate(0);
            var count = Convert.ToInt32(devices.Count);
            var hasWindowsLighting = false;
            for (var index = 0; index < count; index++)
            {
                dynamic device = devices.Item(index);
                var name = Convert.ToString(device.Name) ?? string.Empty;
                if (name.Contains("WindowsLighting", StringComparison.OrdinalIgnoreCase))
                {
                    hasWindowsLighting = true;
                    break;
                }
            }

            if (!hasWindowsLighting)
            {
                TryReleaseAuraSdk(sdk);
                return;
            }

            _auraSdk = sdk;
            Console.WriteLine("ASUS Aura SDK opened for FX608LM WindowsLighting keyboard.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ASUS Aura SDK unavailable; trying Aura service: {ex.Message}");
            _auraSdk = null;
        }
    }

    private bool ApplyAuraSdk(IReadOnlyList<Rgb> colors)
    {
        try
        {
            dynamic devices = _auraSdk!.Enumerate(0);
            var count = Convert.ToInt32(devices.Count);
            var applied = false;
            for (var index = 0; index < count; index++)
            {
                dynamic device = devices.Item(index);
                var name = Convert.ToString(device.Name) ?? string.Empty;
                if (!name.Contains("WindowsLighting", StringComparison.OrdinalIgnoreCase))
                    continue;

                dynamic lights = device.Lights;
                var lightCount = Convert.ToInt32(lights.Count);
                for (var lightIndex = 0; lightIndex < lightCount; lightIndex++)
                {
                    var start = lightIndex * colors.Count / Math.Max(1, lightCount);
                    var end = (lightIndex + 1) * colors.Count / Math.Max(1, lightCount);
                    var color = Average(colors, start, end);
                    dynamic light = lights.Item(lightIndex);
                    light.Red = color.R;
                    light.Green = color.G;
                    light.Blue = color.B;
                }

                // Aura SDK buffers color properties until Apply() is called.
                // Without this call the COM setters succeed but the keyboard
                // never receives a lighting update.
                device.Apply();
                applied = lightCount > 0;
            }

            if (!applied)
                return false;

            if (Interlocked.Increment(ref _successfulApplies) <= 3)
                Console.WriteLine("ASUS FX608LM Aura SDK WindowsLighting update applied.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ASUS Aura SDK update failed; switching to Aura service: {ex.Message}");
            return false;
        }
    }

    private static uint PackAuraSdkColor(Rgb color) =>
        (uint)(color.R | (color.B << 8) | (color.G << 16));

    private void TryReleaseAuraSdk(dynamic? sdk = null)
    {
        try
        {
            (sdk ?? _auraSdk)?.ReleaseControl(0);
        }
        catch
        {
            // The ASUS SDK can disappear during shutdown.
        }

        if (sdk is null)
            _auraSdk = null;
    }

    private bool ApplyAuraZones(IReadOnlyList<Rgb> colors)
    {
        try
        {
            var zones = new uint[4];
            for (var zone = 0; zone < zones.Length; zone++)
            {
                var start = zone * colors.Count / zones.Length;
                var end = (zone + 1) * colors.Count / zones.Length;
                zones[zone] = PackAuraColor(Average(colors, start, end));
            }

            dynamic service = _auraService!;
            service.SetLedMatrix(zones, AuraDeviceName);
            if (Interlocked.Increment(ref _successfulApplies) <= 3)
                Console.WriteLine("ASUS FX608LM Aura 4-zone lighting update applied.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ASUS Aura 4-zone update failed; switching to ATKACPI: {ex.Message}");
            _auraService = null;
            return false;
        }
    }

    private static uint PackAuraColor(Rgb color) =>
        (uint)(color.R | (color.G << 8) | (color.B << 16));

    private bool DeviceSet(uint deviceId, byte[] setting)
    {
        if (_handle is null || _handle.IsInvalid)
            return false;

        var args = new byte[4 + setting.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(args.AsSpan(0, 4), deviceId);
        Buffer.BlockCopy(setting, 0, args, 4, setting.Length);

        return SendDeviceSet(deviceId, args);
    }

    private bool DeviceSetLegacy(uint deviceId, byte[] setting)
    {
        var args = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(args.AsSpan(0, 4), deviceId);
        Buffer.BlockCopy(setting, 0, args, 4, setting.Length);

        return SendDeviceSet(deviceId, args);
    }

    private bool SendDeviceSet(uint deviceId, byte[] args)
    {
        if (_handle is null || _handle.IsInvalid)
            return false;

        var input = new byte[8 + args.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(0, 4), DeviceSetCommand);
        BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(4, 4), (uint)args.Length);
        Buffer.BlockCopy(args, 0, input, 8, args.Length);

        // ATKACPI accepts the DEVS request with the large response buffer
        // used by OpenRGB's native ASUS controller. The result is still the
        // first DWORD (1 = accepted).
        var output = new byte[1024];
        if (!Native.DeviceIoControl(
                _handle,
                IoControlCode,
                input,
                (uint)input.Length,
                output,
                (uint)output.Length,
                out var returned,
                IntPtr.Zero) || returned < 4)
        {
            Console.WriteLine($"ASUS FX608LM WMI lighting endpoint 0x{deviceId:X8} failed: Win32={Marshal.GetLastWin32Error()}");
            return false;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(output) == 1;
    }

    private static Rgb Average(IReadOnlyList<Rgb> colors)
    {
        return Average(colors, 0, colors.Count);
    }

    private static Rgb Average(IReadOnlyList<Rgb> colors, int start, int end)
    {
        if (colors.Count == 0 || start >= end)
            return new Rgb(0, 0, 0);

        ulong red = 0;
        ulong green = 0;
        ulong blue = 0;
        for (var index = start; index < end; index++)
        {
            var color = colors[index];
            red += color.R;
            green += color.G;
            blue += color.B;
        }

        return new Rgb(
            (byte)(red / (ulong)(end - start)),
            (byte)(green / (ulong)(end - start)),
            (byte)(blue / (ulong)(end - start)));
    }

    private void CloseHandle()
    {
        _lampHandle?.Dispose();
        _lampHandle = null;
        _lampFeatureReportLength = 0;
        _handle?.Dispose();
        _handle = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_auraService is not null)
            {
                try
                {
                    _auraService.ReleaseControl(0);
                }
                catch
                {
                    // The ASUS service can disappear during shutdown.
                }
            }
            TryReleaseAuraSdk();
            _auraService = null;
            CloseHandle();
        }
    }
}
