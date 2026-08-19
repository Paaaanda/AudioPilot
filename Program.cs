using AudioPilot;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("AudioPilot 只能在 Windows 上运行。");
    return 1;
}

var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var config = AudioPilotConfig.Load(configPath);

using var pilot = new AudioDevicePilot(config);
using var stopped = new ManualResetEventSlim();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopped.Set();
};

pilot.Start();
Console.WriteLine("AudioPilot 正在运行。按 Ctrl+C 退出。");
stopped.Wait();
return 0;
