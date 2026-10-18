using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class RazerBasiliskWriter : IDisposable
{
    private const ushort VendorId = 0x1532;
    private const ushort ProductId = 0x00B9;
    private const int ReportLength = 91;
    private const int ArgumentsOffset = 9;
    private const byte TransactionId = 0x01;

    private readonly object _sync = new();
    private SafeFileHandle? _handle;
    private string? _path;
    private int _featureReportLength;
    private int _successfulApplies;

    private readonly record struct HidOpenAttempt(string Name, uint Access, uint ShareMode, uint Flags);

    private static readonly HidOpenAttempt[] OpenAttempts =
    [
        new("read-write/synchronous/shared", Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, 0),
        new("read-write/overlapped/shared", Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, Native.FileFlagOverlapped),
        new("no-access/synchronous/shared", 0,
            Native.FileShareRead | Native.FileShareWrite, 0),
        new("write-only/synchronous/shared", Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, 0),
        new("write-only/overlapped/shared", Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, Native.FileFlagOverlapped),
    ];

    internal bool IsOpen => _handle is { IsInvalid: false };
    internal string? Path => _path;

    internal void TryOpen()
    {
        lock (_sync)
        {
            if (IsOpen)
                return;

            foreach (var path in Zh980Writer.EnumerateHidPaths()
                         .Where(IsLightingInterface)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var attempt in OpenAttempts)
                {
                    var candidate = Native.CreateFile(
                        path,
                        attempt.Access,
                        attempt.ShareMode,
                        IntPtr.Zero,
                        Native.OpenExisting,
                        attempt.Flags,
                        IntPtr.Zero);

                    if (candidate.IsInvalid)
                    {
                        var error = Marshal.GetLastWin32Error();
                        Console.WriteLine($"Razer Basilisk HID open failed: {attempt.Name}; Win32={error}");
                        candidate.Dispose();
                        continue;
                    }

                    var attributes = new Native.HidAttributes
                    {
                        Size = Marshal.SizeOf<Native.HidAttributes>(),
                    };
                    if (!Native.HidD_GetAttributes(candidate, ref attributes) ||
                        attributes.VendorID != VendorId || attributes.ProductID != ProductId)
                    {
                        Console.WriteLine($"Razer Basilisk HID attributes failed: {attempt.Name}; Win32={Marshal.GetLastWin32Error()}");
                        candidate.Dispose();
                        continue;
                    }

                    ReadCaps(candidate);
                    _handle = candidate;
                    _path = path;
                    Console.WriteLine($"Razer Basilisk HID opened: {_path} via {attempt.Name}; feature={_featureReportLength}");
                    return;
                }
            }

            Console.WriteLine("Razer Basilisk HID is not open; controller will stay available and retry on the next color update.");
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

            var color = colors.Count > BasiliskV3XLayout.ScrollWheelIndex
                ? colors[BasiliskV3XLayout.ScrollWheelIndex]
                : colors.Count == 0 ? new Rgb(0, 0, 0) : colors[0];

            // Razer's extended-matrix custom frame for the one-cell mouse
            // region: row 0, columns 0..0, followed by RGB bytes.
            var frame = CreateReport(0x0F, 0x03, 0x08);
            frame[ArgumentsOffset + 2] = 0;
            frame[ArgumentsOffset + 3] = 0;
            frame[ArgumentsOffset + 4] = 0;
            frame[ArgumentsOffset + 5] = color.R;
            frame[ArgumentsOffset + 6] = color.G;
            frame[ArgumentsOffset + 7] = color.B;

            if (!SendFeature(frame, "custom frame"))
                return false;

            Thread.Sleep(1);

            // Activate the custom frame on the scroll-wheel region.
            var mode = CreateReport(0x0F, 0x02, 0x0C);
            mode[ArgumentsOffset + 0] = 0x00;
            mode[ArgumentsOffset + 1] = 0x00;
            mode[ArgumentsOffset + 2] = 0x08;

            var success = SendFeature(mode, "custom mode");
            if (success && Interlocked.Increment(ref _successfulApplies) <= 3)
                Console.WriteLine($"Razer Basilisk HID lighting update applied ({color.R},{color.G},{color.B})");
            return success;
        }
    }

    internal static bool IsLightingInterface(string path) =>
        path.Contains($"vid_{VendorId:X4}&pid_{ProductId:X4}", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("mi_00", StringComparison.OrdinalIgnoreCase);

    private static byte[] CreateReport(byte commandClass, byte commandId, byte dataSize)
    {
        var report = new byte[ReportLength];
        report[0] = 0x00; // report index
        report[1] = 0x00; // status
        report[2] = TransactionId;
        report[3] = 0x00; // remaining packets, little-endian
        report[4] = 0x00;
        report[5] = 0x00; // protocol type
        report[6] = dataSize;
        report[7] = commandClass;
        report[8] = (byte)(commandId & 0x7F);
        return report;
    }

    private bool SendFeature(byte[] report, string label)
    {
        if (_handle is null || _handle.IsInvalid)
            return false;

        report[89] = CalculateCrc(report);
        var length = Math.Max(report.Length, _featureReportLength);
        var buffer = report.Length == length ? report : new byte[length];
        if (!ReferenceEquals(buffer, report))
            Buffer.BlockCopy(report, 0, buffer, 0, report.Length);

        if (Native.HidD_SetFeature(_handle, buffer, buffer.Length))
            return true;

        Console.WriteLine($"Razer Basilisk HID feature report failed at {label}: Win32={Marshal.GetLastWin32Error()}");
        CloseHandle();
        return false;
    }

    private static byte CalculateCrc(byte[] report)
    {
        byte crc = 0;
        for (var i = 3; i < 89; i++)
            crc ^= report[i];
        return crc;
    }

    private void ReadCaps(SafeFileHandle handle)
    {
        if (!Native.HidD_GetPreparsedData(handle, out var preparsed) || preparsed == IntPtr.Zero)
            return;

        try
        {
            if (Native.HidP_GetCaps(preparsed, out var caps) == Native.HidpStatusSuccess)
                _featureReportLength = caps.FeatureReportByteLength;
        }
        finally
        {
            Native.HidD_FreePreparsedData(preparsed);
        }
    }

    private void CloseHandle()
    {
        _handle?.Dispose();
        _handle = null;
        _path = null;
        _featureReportLength = 0;
    }

    public void Dispose()
    {
        lock (_sync)
            CloseHandle();
    }
}
