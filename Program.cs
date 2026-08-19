using AudioPilot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("AudioPilot 只能在 Windows 上运行。");
    return 1;
}

if (args.Contains("--agent", StringComparer.OrdinalIgnoreCase) ||
    !WindowsServiceHelpers.IsWindowsService())
{
    await RunAgentAsync();
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "AudioPilot");
builder.Services.AddHostedService<InteractiveAgentSupervisor>();

await builder.Build().RunAsync();
return 0;

static async Task RunAgentAsync()
{
    using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
    var sessionId = currentProcess.SessionId;
    using var singleInstance = new Mutex(
        initiallyOwned: true,
        name: $"Local\\AudioPilot.Agent.{sessionId}",
        createdNew: out var createdNew);

    if (!createdNew)
    {
        return;
    }

    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var config = AudioPilotConfig.Load(configPath);
    var log = new AudioPilotLog();
    using var pilot = new AudioDevicePilot(config, log.Info, log.Error);
    using var stopped = new CancellationTokenSource();

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stopped.Cancel();
    };

    pilot.Start();
    log.Info("AudioPilot 用户会话代理已启动。");
    Console.WriteLine("AudioPilot 正在运行。按 Ctrl+C 退出。");

    try
    {
        await Task.Delay(Timeout.Infinite, stopped.Token);
    }
    catch (OperationCanceledException)
    {
    }
}
