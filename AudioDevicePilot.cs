using System.Runtime.InteropServices;

namespace AudioPilot;

internal sealed class AudioDevicePilot : IDisposable
{
    private readonly AudioPilotConfig _config;
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly Timer _timer;
    private readonly Timer _pollTimer;
    private readonly RogDeltaIIConnectionProbe _headsetProbe;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logError;
    private readonly object _gate = new();
    private bool _disposed;

    public AudioDevicePilot(
        AudioPilotConfig config,
        Action<string>? logInfo = null,
        Action<string>? logError = null)
    {
        _config = config;
        _logInfo = logInfo ?? Console.WriteLine;
        _logError = logError ?? Console.Error.WriteLine;
        _enumerator = ComFactory.Create<IMMDeviceEnumerator>(
            "BCDE0395-E52F-467C-8E3D-C4579291692E");
        _notificationClient = new DeviceNotificationClient(ScheduleEvaluation);
        _timer = new Timer(_ => Evaluate(), null, Timeout.Infinite, Timeout.Infinite);
        _pollTimer = new Timer(_ => Evaluate(), null, Timeout.Infinite, Timeout.Infinite);
        _headsetProbe = new RogDeltaIIConnectionProbe(config.HeadsetVendorId, config.HeadsetProductId);
    }

    public void Start()
    {
        Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(_notificationClient));
        Evaluate();
        var interval = Math.Max(500, _config.PollIntervalMilliseconds);
        _pollTimer.Change(interval, interval);
    }

    private void ScheduleEvaluation() =>
        _timer.Change(Math.Max(0, _config.SwitchDelayMilliseconds), Timeout.Infinite);

    private void Evaluate()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var devices = GetRenderDevices();
                var headset = Find(devices, _config.HeadsetNameContains, DeviceState.Active);
                var fallback = Find(devices, _config.FallbackNameContains, DeviceState.Active);
                var connected = headset is null ? false : _headsetProbe.TryGetConnectionState();

                if (connected is null)
                {
                    _logInfo("无法读取耳机无线链路，保留当前默认输出。");
                    return;
                }

                var target = connected.Value ? headset : fallback;

                if (target is null)
                {
                    _logInfo("未找到可用目标设备。");
                    return;
                }

                if (!IsDefaultDevice(target.Id))
                {
                    SetDefaultDevice(target.Id);
                    _logInfo($"默认输出：{target.Name}");
                }
            }
            catch (Exception exception)
            {
                _logError($"切换失败：{exception.Message}");
            }
        }
    }

    private List<AudioDevice> GetRenderDevices()
    {
        Marshal.ThrowExceptionForHR(_enumerator.EnumAudioEndpoints(
            EDataFlow.Render,
            DeviceState.Active | DeviceState.Disabled | DeviceState.NotPresent | DeviceState.Unplugged,
            out var collection));

        try
        {
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            var devices = new List<AudioDevice>((int)count);

            for (uint index = 0; index < count; index++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(index, out var device));
                try
                {
                    Marshal.ThrowExceptionForHR(device.GetId(out var id));
                    Marshal.ThrowExceptionForHR(device.GetState(out var state));
                    devices.Add(new AudioDevice(id, GetFriendlyName(device), state));
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }

            return devices;
        }
        finally
        {
            Marshal.ReleaseComObject(collection);
        }
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StorageAccessMode.Read, out var store));
        try
        {
            var key = PropertyKeys.DeviceFriendlyName;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
            try
            {
                return value.GetString() ?? "(未命名音频设备)";
            }
            finally
            {
                value.Clear();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static AudioDevice? Find(IEnumerable<AudioDevice> devices, string fragment, DeviceState state) =>
        devices.FirstOrDefault(device =>
            (device.State & state) != 0 &&
            device.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static void SetDefaultDevice(string deviceId)
    {
        var policy = ComFactory.Create<IPolicyConfig>(
            "870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
        try
        {
            foreach (var role in Enum.GetValues<ERole>())
            {
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, role));
            }
        }
        finally
        {
            Marshal.ReleaseComObject(policy);
        }
    }

    private bool IsDefaultDevice(string deviceId)
    {
        Marshal.ThrowExceptionForHR(_enumerator.GetDefaultAudioEndpoint(
            EDataFlow.Render,
            ERole.Multimedia,
            out var current));

        try
        {
            Marshal.ThrowExceptionForHR(current.GetId(out var currentId));
            return string.Equals(currentId, deviceId, StringComparison.Ordinal);
        }
        finally
        {
            Marshal.ReleaseComObject(current);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
            _timer.Dispose();
            _pollTimer.Dispose();
            Marshal.ReleaseComObject(_enumerator);
        }
    }

    private sealed record AudioDevice(string Id, string Name, DeviceState State);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class DeviceNotificationClient(Action changed) : IMMNotificationClient
{
    public int OnDeviceStateChanged(string deviceId, DeviceState newState) { changed(); return 0; }
    public int OnDeviceAdded(string deviceId) { changed(); return 0; }
    public int OnDeviceRemoved(string deviceId) { changed(); return 0; }
    public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId) => 0;
    public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
}
