using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal static class Native
{
    internal static readonly Guid HidGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    internal const int HidpStatusSuccess = 0x00110000;

    [Flags]
    internal enum DeviceFlags : uint
    {
        Present = 0x2,
        DeviceInterface = 0x10,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInterfaceData
    {
        public int CbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidAttributes
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        public ushort Reserved0;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public ushort Reserved4;
        public ushort Reserved5;
        public ushort Reserved6;
        public ushort Reserved7;
        public ushort Reserved8;
        public ushort Reserved9;
        public ushort Reserved10;
        public ushort Reserved11;
        public ushort Reserved12;
        public ushort Reserved13;
        public ushort Reserved14;
        public ushort Reserved15;
        public ushort Reserved16;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, string? enumerator, IntPtr hwndParent, DeviceFlags flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
        uint memberIndex, ref DeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet, ref DeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize,
        out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HidAttributes attributes);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] report, int reportLength);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_SetOutputReport(SafeFileHandle hidDeviceObject, byte[] report, int reportLength);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_SetNumInputBuffers(SafeFileHandle hidDeviceObject, int numberBuffers);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteFile(SafeFileHandle file, byte[] buffer, int numberOfBytesToWrite,
        out int numberOfBytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[] inputBuffer,
        uint inputBufferSize,
        byte[] outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint FileShareRead = 0x1;
    internal const uint FileShareWrite = 0x2;
    internal const uint OpenExisting = 3;
    internal const uint FileFlagOverlapped = 0x40000000;
}

internal readonly record struct Rgb(byte R, byte G, byte B);
internal sealed record LedDefinition(string Name, int LightIndex);

internal static class Zh980Layout
{
    // The 97 physical keys and light_index values from the installed ZH980
    // layout. The firmware colour table is 1-based and reserves slot zero.
    internal static readonly IReadOnlyList<LedDefinition> Leds =
    [
        new("Keyboard_Escape", 1), new("Keyboard_F1", 2), new("Keyboard_F2", 3),
        new("Keyboard_F3", 4), new("Keyboard_F4", 5), new("Keyboard_F5", 6),
        new("Keyboard_F6", 7), new("Keyboard_F7", 8), new("Keyboard_F8", 9),
        new("Keyboard_F9", 10), new("Keyboard_F10", 11), new("Keyboard_F11", 12),
        new("Keyboard_F12", 13), new("Keyboard_Insert", 116), new("Keyboard_Delete", 119),
        new("Keyboard_PageUp", 118), new("Keyboard_PageDown", 121),

        new("Keyboard_GraveAccentAndTilde", 19), new("Keyboard_1", 20), new("Keyboard_2", 21),
        new("Keyboard_3", 22), new("Keyboard_4", 23), new("Keyboard_5", 24),
        new("Keyboard_6", 25), new("Keyboard_7", 26), new("Keyboard_8", 27),
        new("Keyboard_9", 28), new("Keyboard_0", 29), new("Keyboard_MinusAndUnderscore", 30),
        new("Keyboard_EqualsAndPlus", 31), new("Keyboard_Backspace", 103),
        new("Keyboard_NumLock", 32), new("Keyboard_NumSlash", 33), new("Keyboard_NumAsterisk", 34),
        new("Keyboard_NumMinus", 122),

        new("Keyboard_Tab", 37), new("Keyboard_Q", 38), new("Keyboard_W", 39),
        new("Keyboard_E", 40), new("Keyboard_R", 41), new("Keyboard_T", 42),
        new("Keyboard_Y", 43), new("Keyboard_U", 44), new("Keyboard_I", 45),
        new("Keyboard_O", 46), new("Keyboard_P", 47), new("Keyboard_BracketLeft", 48),
        new("Keyboard_BracketRight", 49), new("Keyboard_Backslash", 67),
        new("Keyboard_Num7", 50), new("Keyboard_Num8", 51), new("Keyboard_Num9", 52),
        new("Keyboard_NumPlus", 123),

        new("Keyboard_CapsLock", 55), new("Keyboard_A", 56), new("Keyboard_S", 57),
        new("Keyboard_D", 58), new("Keyboard_F", 59), new("Keyboard_G", 60),
        new("Keyboard_H", 61), new("Keyboard_J", 62), new("Keyboard_K", 63),
        new("Keyboard_L", 64), new("Keyboard_SemicolonAndColon", 65),
        new("Keyboard_ApostropheAndDoubleQuote", 66), new("Keyboard_Enter", 85),
        new("Keyboard_Num4", 68), new("Keyboard_Num5", 69), new("Keyboard_Num6", 70),

        new("Keyboard_LeftShift", 73), new("Keyboard_Z", 74), new("Keyboard_X", 75),
        new("Keyboard_C", 76), new("Keyboard_V", 77), new("Keyboard_B", 78),
        new("Keyboard_N", 79), new("Keyboard_M", 80), new("Keyboard_CommaAndLessThan", 81),
        new("Keyboard_PeriodAndBiggerThan", 82), new("Keyboard_SlashAndQuestionMark", 83),
        new("Keyboard_RightShift", 84), new("Keyboard_ArrowUp", 101),
        new("Keyboard_Num1", 86), new("Keyboard_Num2", 87), new("Keyboard_Num3", 88),
        new("Keyboard_NumEnter", 106),

        new("Keyboard_LeftCtrl", 91), new("Keyboard_LeftGui", 92), new("Keyboard_LeftAlt", 93),
        new("Keyboard_Space", 94), new("Keyboard_RightAlt", 95), new("Keyboard_RightFn", 96),
        new("Keyboard_ArrowLeft", 99), new("Keyboard_ArrowDown", 100),
        new("Keyboard_ArrowRight", 102), new("Keyboard_Num0", 104),
        new("Keyboard_NumPeriodAndDelete", 105),
    ];

    internal static string SdkName(string layoutName)
    {
        var key = layoutName["Keyboard_".Length..];
        return key switch
        {
            "Escape" => "Key: Escape",
            "GraveAccentAndTilde" => "Key: `",
            "MinusAndUnderscore" => "Key: -",
            "EqualsAndPlus" => "Key: =",
            "Backspace" => "Key: Backspace",
            "NumLock" => "Key: Num Lock",
            "NumSlash" => "Key: Number Pad /",
            "NumAsterisk" => "Key: Number Pad *",
            "NumMinus" => "Key: Number Pad -",
            "Tab" => "Key: Tab",
            "BracketLeft" => "Key: [",
            "BracketRight" => "Key: ]",
            "Backslash" => "Key: \\ (ANSI)",
            "CapsLock" => "Key: Caps Lock",
            "SemicolonAndColon" => "Key: ;",
            "ApostropheAndDoubleQuote" => "Key: '",
            "Enter" => "Key: Enter",
            "LeftShift" => "Key: Left Shift",
            "RightShift" => "Key: Right Shift",
            "ArrowUp" => "Key: Up Arrow",
            "ArrowLeft" => "Key: Left Arrow",
            "ArrowDown" => "Key: Down Arrow",
            "ArrowRight" => "Key: Right Arrow",
            "LeftCtrl" => "Key: Left Control",
            "LeftGui" => "Key: Left Windows",
            "LeftAlt" => "Key: Left Alt",
            "RightAlt" => "Key: Right Alt",
            "RightFn" => "Key: Right Fn",
            "Space" => "Key: Space",
            "NumPlus" => "Key: Number Pad +",
            "NumEnter" => "Key: Number Pad Enter",
            "NumPeriodAndDelete" => "Key: Number Pad .",
            "Num0" => "Key: Number Pad 0",
            "Num1" => "Key: Number Pad 1",
            "Num2" => "Key: Number Pad 2",
            "Num3" => "Key: Number Pad 3",
            "Num4" => "Key: Number Pad 4",
            "Num5" => "Key: Number Pad 5",
            "Num6" => "Key: Number Pad 6",
            "Num7" => "Key: Number Pad 7",
            "Num8" => "Key: Number Pad 8",
            "Num9" => "Key: Number Pad 9",
            "Insert" => "Key: Insert",
            "Delete" => "Key: Delete",
            "PageUp" => "Key: Page Up",
            "PageDown" => "Key: Page Down",
            _ when key.Length == 1 || key.StartsWith("F", StringComparison.Ordinal) => $"Key: {key}",
            _ => $"Key: {key}",
        };
    }
}

internal sealed class Zh980Writer : IDisposable
{
    private const int PacketDataLength = 64;
    private const int ColorBufferSize = 576;
    private SafeFileHandle? _handle;
    private string? _path;
    private ushort _featureReportLength;
    private int _successfulApplies;

    private readonly record struct HidOpenAttempt(string Name, uint Access, uint ShareMode, uint Flags);

    private static readonly HidOpenAttempt[] OpenAttempts =
    [
        // HidD_SetFeature is a synchronous control transfer. Some keyboard
        // composite interfaces reject an overlapped handle or a shared open.
        new("no-access/overlapped/shared", 0, Native.FileShareRead | Native.FileShareWrite,
            Native.FileFlagOverlapped),
        new("no-access/synchronous/shared", 0, Native.FileShareRead | Native.FileShareWrite, 0),
        new("write-only/synchronous/exclusive", Native.GenericWrite, 0, 0),
        new("read-write/synchronous/exclusive", Native.GenericRead | Native.GenericWrite, 0, 0),
        new("write-only/synchronous/shared", Native.GenericWrite, Native.FileShareRead | Native.FileShareWrite, 0),
        new("read-write/synchronous/shared", Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, 0),
        new("read-write/overlapped/shared", Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, Native.FileFlagOverlapped),
    ];

    internal bool IsOpen => _handle is { IsInvalid: false, IsClosed: false };

    internal void TryOpen()
    {
        if (IsOpen)
            return;

        CloseHandle();
        foreach (var path in EnumerateHidPaths()
                     .Where(IsSupportedLightingInterface)
                     .OrderByDescending(IsWirelessLightingInterface))
        {
            foreach (var attempt in OpenAttempts)
            {
                var candidate = Native.CreateFile(path, attempt.Access, attempt.ShareMode,
                    IntPtr.Zero, Native.OpenExisting, attempt.Flags, IntPtr.Zero);
                if (candidate.IsInvalid)
                {
                    Console.WriteLine($"ZH980 HID open failed: attempt={attempt.Name} Win32={Marshal.GetLastWin32Error()} path={path}");
                    candidate.Dispose();
                    continue;
                }

                var attributes = new Native.HidAttributes { Size = Marshal.SizeOf<Native.HidAttributes>() };
                if (!Native.HidD_GetAttributes(candidate, ref attributes) ||
                    attributes.VendorID != 0x05AC || attributes.ProductID != 0x024F)
                {
                    candidate.Dispose();
                    continue;
                }

                ReadReportCapabilities(candidate);

                _handle = candidate;
                _path = path;
                Console.WriteLine($"ZH980 HID opened: {_path} via {attempt.Name}; feature={_featureReportLength}");
                return;
            }
        }

        Console.WriteLine("ZH980 HID is not open yet; SDK service will stay available and retry on the next frame.");
    }

    internal bool Apply(IReadOnlyList<Rgb> colors)
    {
        TryOpen();
        if (!IsOpen)
            return false;

        var report = new byte[PacketDataLength + 1];
        if (!SendCommand(report, 0x04, 0x18, "customization on"))
            return false;

        Array.Clear(report);
        report[1] = 0x04;
        report[2] = 0x13;
        report[9] = 0x12;
        if (!SendReport(report, "start effect page"))
            return false;

        for (var page = 0; page < 5; page++)
        {
            Array.Clear(report);
            for (var effect = 0; effect < 4; effect++)
            {
                var offset = 1 + effect * 16;
                report[offset] = (byte)(1 + page * 4 + effect);
                report[offset + 8] = 0;
                report[offset + 9] = 0x0F;
                report[offset + 10] = 0x00;
                report[offset + 11] = 0x00;
                report[offset + 14] = 0xAA;
                report[offset + 15] = 0x55;
            }
            if (!SendReport(report, $"effect page {page + 1}/5"))
                return false;
        }

        for (var padding = 0; padding < 3; padding++)
        {
            Array.Clear(report);
            if (!SendReport(report, $"effect padding {padding + 1}/3"))
                return false;
        }

        var colorBuffer = new byte[ColorBufferSize];
        for (var slot = 0; slot < ColorBufferSize; slot += 4)
            colorBuffer[slot] = 0x80;

        for (var led = 0; led < Math.Min(colors.Count, Zh980Layout.Leds.Count); led++)
        {
            var slot = Zh980Layout.Leds[led].LightIndex * 4;
            if (slot + 3 >= colorBuffer.Length)
                continue;
            colorBuffer[slot + 1] = colors[led].R;
            colorBuffer[slot + 2] = colors[led].G;
            colorBuffer[slot + 3] = colors[led].B;
        }

        for (var packet = 0; packet < 9; packet++)
        {
            Array.Clear(report);
            Buffer.BlockCopy(colorBuffer, packet * PacketDataLength, report, 1, PacketDataLength);
            if (!SendReport(report, $"colour packet {packet + 1}/9"))
                return false;
        }

        Array.Clear(report);
        report[1] = 0x80;
        report[10] = 0x0F;
        if (!SendReport(report, "select customization page"))
            return false;

        if (!SendCommand(report, 0x04, 0x02, "end communication"))
            return false;
        var success = SendCommand(report, 0x04, 0xF0, "start effect");
        if (success && Interlocked.Increment(ref _successfulApplies) <= 3)
            Console.WriteLine($"ZH980 HID lighting update applied ({colors.Count} LEDs)");
        return success;
    }

    internal bool DemoRed()
    {
        var colors = Enumerable.Repeat(new Rgb(255, 0, 0), Zh980Layout.Leds.Count).ToArray();
        return Apply(colors);
    }

    private bool SendCommand(byte[] report, byte header, byte command, string label)
    {
        Array.Clear(report);
        report[1] = header;
        report[2] = command;
        return SendReport(report, label);
    }

    private bool SendReport(byte[] report, string label)
    {
        if (!IsOpen || _handle is null)
            return false;
        var transport = "output";
        var ok = Native.HidD_SetOutputReport(_handle, report, report.Length);
        if (!ok)
        {
            transport = "write";
            ok = Native.WriteFile(_handle, report, report.Length, out var written, IntPtr.Zero) &&
                 written == report.Length;
        }
        if (!ok)
        {
            transport = "feature";
            // HidAPI pads to the collection's longest feature-report length
            // before calling HidD_SetFeature. The firmware uses a 64-byte
            // payload but its Windows HID collection advertises a longer
            // feature buffer, so passing only 65 bytes is rejected.
            var featureLength = Math.Max(report.Length, _featureReportLength);
            var featureReport = new byte[featureLength];
            Buffer.BlockCopy(report, 0, featureReport, 0, report.Length);
            ok = Native.HidD_SetFeature(_handle, featureReport, featureReport.Length);
        }

        if (!ok)
        {
            Console.WriteLine($"ZH980 HID {transport} report failed at {label}: Win32={Marshal.GetLastWin32Error()}");
            CloseHandle();
            return false;
        }
        Thread.Sleep(10);
        return true;
    }

    private void CloseHandle()
    {
        _handle?.Dispose();
        _handle = null;
        _path = null;
        _featureReportLength = 0;
    }

    private void ReadReportCapabilities(SafeFileHandle handle)
    {
        _featureReportLength = 0;
        var buffersOk = Native.HidD_SetNumInputBuffers(handle, 64);
        var preparsedOk = Native.HidD_GetPreparsedData(handle, out var preparsed);
        if (!preparsedOk || preparsed == IntPtr.Zero)
        {
            Console.WriteLine($"ZH980 HID caps unavailable: input-buffers={buffersOk}, preparsed={preparsedOk}, Win32={Marshal.GetLastWin32Error()}");
            return;
        }
        try
        {
            var status = Native.HidP_GetCaps(preparsed, out var caps);
            Console.WriteLine($"ZH980 HID caps status=0x{status:X8}, input={caps.InputReportByteLength}, output={caps.OutputReportByteLength}, feature={caps.FeatureReportByteLength}");
            if (status == Native.HidpStatusSuccess)
                _featureReportLength = caps.FeatureReportByteLength;
        }
        finally
        {
            Native.HidD_FreePreparsedData(preparsed);
        }
    }

    public void Dispose() => CloseHandle();

    internal static IEnumerable<string> EnumerateHidPaths()
    {
        var guid = Native.HidGuid;
        var infoSet = Native.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero,
            Native.DeviceFlags.Present | Native.DeviceFlags.DeviceInterface);
        if (infoSet == IntPtr.Zero || infoSet == new IntPtr(-1))
            yield break;

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new Native.DeviceInterfaceData { CbSize = Marshal.SizeOf<Native.DeviceInterfaceData>() };
                if (!Native.SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref guid, index, ref data))
                    yield break;

                Native.SetupDiGetDeviceInterfaceDetail(infoSet, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
                if (required <= 0)
                    continue;

                var detail = Marshal.AllocHGlobal(required);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (Native.SetupDiGetDeviceInterfaceDetail(infoSet, ref data, detail, required, out _, IntPtr.Zero))
                    {
                        var path = Marshal.PtrToStringUni(detail + 4);
                        if (!string.IsNullOrWhiteSpace(path))
                            yield return path;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(infoSet);
        }
    }

    internal static IEnumerable<(string Path, string Attempt, bool Valid, int Error)> ProbeOpenAttempts()
    {
        foreach (var path in EnumerateHidPaths().Where(IsSupportedLightingInterface)
                     .OrderByDescending(IsWirelessLightingInterface))
        {
            foreach (var attempt in OpenAttempts)
            {
                using var handle = Native.CreateFile(path, attempt.Access, attempt.ShareMode,
                    IntPtr.Zero, Native.OpenExisting, attempt.Flags, IntPtr.Zero);
                var error = handle.IsInvalid ? Marshal.GetLastWin32Error() : 0;
                yield return (path, attempt.Name, !handle.IsInvalid, error);
            }
        }
    }

    private static bool IsSupportedLightingInterface(string path) =>
        path.Contains("vid_05ac&pid_024f", StringComparison.OrdinalIgnoreCase) &&
        (path.Contains("mi_03", StringComparison.OrdinalIgnoreCase) ||
         (path.Contains("mi_00", StringComparison.OrdinalIgnoreCase) &&
          path.Contains("\\kbd", StringComparison.OrdinalIgnoreCase)));

    private static bool IsWirelessLightingInterface(string path) =>
        path.Contains("mi_03", StringComparison.OrdinalIgnoreCase);
}

internal sealed class OpenRgbSdkServer : IAsyncDisposable
{
    private const uint ProtocolVersion = 5;
    private readonly TcpListener _listener;
    private readonly IReadOnlyList<DeviceDefinition> _devices;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _colorLock = new();
    private readonly Dictionary<uint, Rgb[]> _colors;

    internal OpenRgbSdkServer(
        int port,
        Zh980Writer writer,
        RazerBasiliskWriter razerWriter,
        AsusTufWmiWriter asusWriter)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _devices = DeviceCatalog.Create(writer, razerWriter, asusWriter);
        DeviceCatalog.Validate(_devices);
        _colors = _devices.ToDictionary(
            device => device.Id,
            device => Enumerable.Repeat(new Rgb(0, 0, 0), device.Leds.Count).ToArray());
    }

    internal async Task RunAsync()
    {
        _listener.Start();
        Console.WriteLine($"Artemis/OpenRGB compatibility SDK listening on 127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}");
        foreach (var device in _devices)
        {
            var status = device.ApplyColors is null ? "layout-only" : "hardware-backed";
            Console.WriteLine($"Controller {device.Id}: {device.Name}, LEDs={device.Leds.Count}, {status}");
        }
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = Task.Run(() => HandleClientAsync(client), _stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            Console.WriteLine($"SDK client connected: {client.Client.RemoteEndPoint}");
            try
            {
                uint negotiatedProtocol = ProtocolVersion;
                while (!_stop.IsCancellationRequested)
                {
                    var header = new byte[16];
                    if (!await TryReadExactlyAsync(stream, header))
                        break;
                    if (header[0] != (byte)'O' || header[1] != (byte)'R' || header[2] != (byte)'G' || header[3] != (byte)'B')
                        break;

                    var deviceId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
                    var packetId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
                    var packetSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, 4));
                    if (packetSize > 4 * 1024 * 1024)
                        break;
                    var payload = new byte[packetSize];
                    if (!await TryReadExactlyAsync(stream, payload))
                        break;

                    negotiatedProtocol = await HandlePacketAsync(stream, deviceId, packetId, payload, negotiatedProtocol);
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                Console.WriteLine("SDK client disconnected");
            }
        }
    }

    private async Task<uint> HandlePacketAsync(NetworkStream stream, uint deviceId, uint packetId, byte[] payload, uint negotiatedProtocol)
    {
        switch (packetId)
        {
            case 40:
                var clientMax = payload.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)) : 0u;
                negotiatedProtocol = Math.Min(ProtocolVersion, clientMax);
                Console.WriteLine($"SDK protocol request: clientMax={clientMax}, selected={negotiatedProtocol}");
                await SendPacketAsync(stream, deviceId, 40, UInt32Payload(negotiatedProtocol));
                return negotiatedProtocol;
            case 0:
                await SendPacketAsync(stream, 0, 0, UInt32Payload((uint)_devices.Count));
                return negotiatedProtocol;
            case 1:
                var requestedProtocol = payload.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)) : negotiatedProtocol;
                negotiatedProtocol = Math.Min(ProtocolVersion, requestedProtocol);
                Console.WriteLine($"SDK controller-data request: negotiated={negotiatedProtocol}");
                var device = FindDevice(deviceId);
                if (device is not null)
                    await SendPacketAsync(stream, deviceId, 1, BuildControllerResponse(device, negotiatedProtocol));
                else
                    await SendPacketAsync(stream, deviceId, 1, UInt32Payload(0));
                return negotiatedProtocol;
            case 50:
                return negotiatedProtocol;
            case 1050:
                ApplyFullColors(deviceId, payload);
                return negotiatedProtocol;
            case 1051:
                ApplyZoneColors(deviceId, payload);
                return negotiatedProtocol;
            case 1052:
                ApplySingleColor(deviceId, payload);
                return negotiatedProtocol;
            case 1100:
            case 1101:
            case 140:
                return negotiatedProtocol;
            default:
                Console.WriteLine($"SDK packet ignored: id={packetId} bytes={payload.Length}");
                return negotiatedProtocol;
        }
    }

    private DeviceDefinition? FindDevice(uint deviceId) =>
        _devices.FirstOrDefault(device => device.Id == deviceId);

    private void ApplyFullColors(uint deviceId, byte[] payload)
    {
        var device = FindDevice(deviceId);
        if (device is null)
            return;
        if (payload.Length < 6)
            return;
        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4, 2));
        if (dataSize != payload.Length || count > device.Leds.Count || payload.Length < 6 + count * 4)
            return;

        var next = new Rgb[device.Leds.Count];
        for (var i = 0; i < count; i++)
            next[i] = DecodeColor(payload, 6 + i * 4);
        StoreAndApply(device, next);
    }

    private void ApplyZoneColors(uint deviceId, byte[] payload)
    {
        var device = FindDevice(deviceId);
        if (device is null)
            return;
        if (payload.Length < 10)
            return;
        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        if (dataSize != payload.Length)
            return;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(8, 2));
        if (payload.Length < 10 + count * 4)
            return;

        var next = new Rgb[device.Leds.Count];
        lock (_colorLock)
            Array.Copy(_colors[device.Id], next, device.Leds.Count);
        for (var i = 0; i < Math.Min(count, next.Length); i++)
            next[i] = DecodeColor(payload, 10 + i * 4);
        StoreAndApply(device, next);
    }

    private void ApplySingleColor(uint deviceId, byte[] payload)
    {
        var device = FindDevice(deviceId);
        if (device is null)
            return;
        if (payload.Length < 8)
            return;
        var index = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        if (index < 0 || index >= device.Leds.Count)
            return;
        var next = new Rgb[device.Leds.Count];
        lock (_colorLock)
            Array.Copy(_colors[device.Id], next, device.Leds.Count);
        next[index] = DecodeColor(payload, 4);
        StoreAndApply(device, next);
    }

    private void StoreAndApply(DeviceDefinition device, Rgb[] colors)
    {
        lock (_colorLock)
            _colors[device.Id] = colors;
        device.ApplyColors?.Invoke(colors);
    }

    private static Rgb DecodeColor(byte[] payload, int offset)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
        return new Rgb((byte)value, (byte)(value >> 8), (byte)(value >> 16));
    }

    private static byte[] UInt32Payload(uint value)
    {
        var result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

    private static byte[] BuildControllerResponse(DeviceDefinition deviceDefinition, uint protocol)
    {
        using var device = new PacketWriter();
        device.WriteInt32(5);
        device.WriteString(deviceDefinition.Name);
        device.WriteString(deviceDefinition.Manufacturer);
        device.WriteString(deviceDefinition.Description);
        device.WriteString(deviceDefinition.Serial);
        device.WriteString(string.Empty);
        device.WriteString(deviceDefinition.Location);

        device.WriteUInt16(1);
        device.WriteInt32(0);
        // Artemis/RGB.NET only loads OpenRGB controllers that expose a mode
        // named exactly "Direct". This is our per-LED custom transport mode.
        device.WriteString("Direct");
        device.WriteInt32(0);
        device.WriteUInt32(1u << 5);
        device.WriteUInt32(0);
        device.WriteUInt32(0);
        device.WriteUInt32(0);
        device.WriteUInt32(100);
        device.WriteUInt32(0);
        device.WriteUInt32(0);
        device.WriteUInt32(0);
        device.WriteUInt32(100);
        device.WriteUInt32(0);
        device.WriteUInt32(1);
        device.WriteUInt16(0);

        device.WriteUInt16(1);
        device.WriteString(deviceDefinition.ZoneName);
        device.WriteInt32(2); // ZONE_TYPE_MATRIX; names + matrix indices preserve the full key layout
        device.WriteUInt32((uint)deviceDefinition.Leds.Count);
        device.WriteUInt32((uint)deviceDefinition.Leds.Count);
        device.WriteUInt32((uint)deviceDefinition.Leds.Count);
        const int matrixHeight = 1;
        var matrixWidth = deviceDefinition.Leds.Count;
        device.WriteUInt16((ushort)(8 + matrixHeight * matrixWidth * 4));
        device.WriteUInt32(matrixHeight);
        device.WriteUInt32((uint)matrixWidth);

        // A continuous matrix keeps every index in the controller's LED
        // vector. The Artemis custom layout supplies the real physical key
        // positions, so the SDK matrix itself does not need empty cells.
        for (uint ledIndex = 0; ledIndex < deviceDefinition.Leds.Count; ledIndex++)
            device.WriteUInt32(ledIndex);
        device.WriteUInt16(0); // segments
        if (protocol >= 5)
            device.WriteUInt32(0); // zone flags

        device.WriteUInt16((ushort)deviceDefinition.Leds.Count);
        foreach (var led in deviceDefinition.Leds)
        {
            device.WriteString(DeviceCatalog.SdkName(led));
            device.WriteUInt32((uint)led.LightIndex);
        }

        device.WriteUInt16((ushort)deviceDefinition.Leds.Count);
        for (var i = 0; i < deviceDefinition.Leds.Count; i++)
            device.WriteUInt32(0);
        if (protocol >= 5)
        {
            device.WriteUInt16(0); // alternate LED display names
            device.WriteUInt32(0); // controller flags
        }

        using var response = new PacketWriter();
        response.WriteUInt32((uint)device.Length);
        response.Write(device.ToArray());
        return response.ToArray();
    }

    private static async Task SendPacketAsync(NetworkStream stream, uint deviceId, uint packetId, byte[] payload)
    {
        var header = new byte[16];
        header[0] = (byte)'O'; header[1] = (byte)'R'; header[2] = (byte)'G'; header[3] = (byte)'B';
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), deviceId);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), packetId);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), (uint)payload.Length);
        await stream.WriteAsync(header);
        if (payload.Length > 0)
            await stream.WriteAsync(payload);
    }

    private static async Task<bool> TryReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (n == 0)
                return false;
            read += n;
        }
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class PacketWriter : IDisposable
    {
        private readonly MemoryStream _stream = new();
        internal int Length => checked((int)_stream.Length);
        internal void Write(byte[] bytes) => _stream.Write(bytes);
        internal void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));
        internal void WriteUInt16(ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            _stream.Write(bytes);
        }
        internal void WriteUInt32(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            _stream.Write(bytes);
        }
        internal void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            if (bytes.Length > ushort.MaxValue)
                throw new InvalidOperationException("OpenRGB string is too long.");
            WriteUInt16((ushort)bytes.Length);
            _stream.Write(bytes);
        }
        internal byte[] ToArray() => _stream.ToArray();
        public void Dispose() => _stream.Dispose();
    }
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Any(a => a.Equals("--verify-layouts", StringComparison.OrdinalIgnoreCase)))
        {
            var devices = DeviceCatalog.Create(null, null, null);
            DeviceCatalog.Validate(devices);
            foreach (var device in devices)
                Console.WriteLine($"{device.Id}: {device.Name} ({device.Leds.Count} LEDs; {device.Transport})");
            return;
        }

        if (args.Any(a => a.Equals("--probe", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var result in Zh980Writer.ProbeOpenAttempts())
                Console.WriteLine($"candidate={result.Path} attempt={result.Attempt} valid={result.Valid} error={result.Error}");
            using var razerProbe = new RazerBasiliskWriter();
            razerProbe.TryOpen();
            using var asusProbe = new AsusTufWmiWriter();
            asusProbe.TryOpen();
            return;
        }

        using var writer = new Zh980Writer();
        writer.TryOpen();
        using var razerWriter = new RazerBasiliskWriter();
        razerWriter.TryOpen();
        using var asusWriter = new AsusTufWmiWriter();
        asusWriter.TryOpen();
        if (args.Any(a => a.Equals("--demo-red", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"ZH980 demo red: {writer.DemoRed()}");
            return;
        }

        var port = 6742;
        var portIndex = Array.FindIndex(args, a => a.Equals("--port", StringComparison.OrdinalIgnoreCase));
        if (portIndex >= 0 && portIndex + 1 < args.Length && int.TryParse(args[portIndex + 1], out var parsedPort))
            port = parsedPort;

        await using var server = new OpenRgbSdkServer(port, writer, razerWriter, asusWriter);
        Console.CancelKeyPress += (_, eventArgs) => eventArgs.Cancel = true;
        await server.RunAsync();
    }
}
