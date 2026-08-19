using HidSharp;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace AudioPilot;

internal sealed class RogDeltaIIConnectionProbe(int vendorId, int productId)
{
    private const byte ReportId = 0xCC;
    private const byte ProtocolGroup = 0x12;
    private const byte ConnectionCommand = 0x00;

    public bool? TryGetConnectionState()
    {
        try
        {
            var device = DeviceList.Local
                .GetHidDevices(vendorId, productId)
                .FirstOrDefault(candidate => candidate.DevicePath.Contains(
                    "col04",
                    StringComparison.OrdinalIgnoreCase));

            if (device is null || !device.TryOpen(out var stream))
            {
                return null;
            }

            using (stream)
            {
                stream.ReadTimeout = 250;
                var query = new byte[device.GetMaxOutputReportLength()];
                query[0] = ReportId;
                query[1] = ProtocolGroup;
                query[2] = ConnectionCommand;

                SendOutputReport(device.DevicePath, query);

                var deadline = DateTime.UtcNow.AddMilliseconds(900);
                while (DateTime.UtcNow < deadline)
                {
                    var response = new byte[device.GetMaxInputReportLength()];
                    try
                    {
                        var count = stream.Read(response, 0, response.Length);
                        if (IsConnectionResponse(response, count))
                        {
                            return response[6] != 0 || response[7] != 0;
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                }

                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static bool IsConnectionResponse(byte[] response, int count) =>
        count >= 8 &&
        response[0] == ReportId &&
        response[1] == ProtocolGroup &&
        response[2] == ConnectionCommand &&
        response[3] == 0 &&
        response[4] == 0 &&
        response[5] == 0;

    private static void SendOutputReport(string path, byte[] report)
    {
        const uint genericRead = 0x80000000;
        const uint genericWrite = 0x40000000;
        const uint shareRead = 0x00000001;
        const uint shareWrite = 0x00000002;
        const uint openExisting = 3;

        using var handle = CreateFile(
            path,
            genericRead | genericWrite,
            shareRead | shareWrite,
            IntPtr.Zero,
            openExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid || !HidD_SetOutputReport(handle, report, report.Length))
        {
            throw new IOException($"ROG HID query failed: {Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_SetOutputReport(
        SafeFileHandle device,
        byte[] report,
        int reportLength);
}
