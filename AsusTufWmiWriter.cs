using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class AsusTufWmiWriter : IDisposable
{
    private const string AuraDeviceName = "WDL_NB_KB_4ZONE_RGB_LIGHTING";
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
    private dynamic? _auraService;
    private int _successfulApplies;

    internal bool IsOpen => _auraService is not null || _handle is { IsInvalid: false };

    internal void TryOpen()
    {
        lock (_sync)
        {
            if (IsOpen)
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
            _auraService = null;
            CloseHandle();
        }
    }
}
