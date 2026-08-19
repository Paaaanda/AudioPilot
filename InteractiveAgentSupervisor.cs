using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace AudioPilot;

internal sealed class InteractiveAgentSupervisor(
    ILogger<InteractiveAgentSupervisor> logger) : BackgroundService
{
    private const uint NoActiveSession = 0xFFFFFFFF;
    private Process? _agent;
    private uint _agentSessionId = NoActiveSession;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var activeSessionId = WTSGetActiveConsoleSessionId();
                var agentStopped = _agent is null || _agent.HasExited;
                var sessionChanged = activeSessionId != _agentSessionId;

                if (activeSessionId == NoActiveSession)
                {
                    StopAgent();
                }
                else if (agentStopped || sessionChanged)
                {
                    StopAgent();
                    TryStartAgent(activeSessionId);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            StopAgent();
        }
    }

    private void TryStartAgent(uint sessionId)
    {
        try
        {
            _agent = StartAgent(sessionId);
            _agentSessionId = sessionId;
            logger.LogInformation(
                "AudioPilot agent started in session {SessionId} with process {ProcessId}.",
                sessionId,
                _agent.Id);
        }
        catch (Exception exception)
        {
            _agent = null;
            _agentSessionId = NoActiveSession;
            logger.LogError(exception, "Unable to start AudioPilot agent in session {SessionId}.", sessionId);
        }
    }

    private static Process StartAgent(uint sessionId)
    {
        if (!WTSQueryUserToken(sessionId, out var userToken))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken failed.");
        }

        using (userToken)
        {
            const uint maximumAllowed = 0x02000000;
            if (!DuplicateTokenEx(
                    userToken,
                    maximumAllowed,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out var primaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed.");
            }

            using (primaryToken)
            {
                if (!CreateEnvironmentBlock(out var environment, primaryToken, false))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateEnvironmentBlock failed.");
                }

                try
                {
                    var executable = Environment.ProcessPath
                        ?? throw new InvalidOperationException("Unable to locate AudioPilot executable.");
                    var startupInfo = new StartupInfo
                    {
                        Size = Marshal.SizeOf<StartupInfo>(),
                        Desktop = "winsta0\\default"
                    };
                    var commandLine = $"\"{executable}\" --agent";

                    const uint createUnicodeEnvironment = 0x00000400;
                    const uint createNoWindow = 0x08000000;
                    if (!CreateProcessAsUser(
                            primaryToken,
                            executable,
                            commandLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            createUnicodeEnvironment | createNoWindow,
                            environment,
                            AppContext.BaseDirectory,
                            ref startupInfo,
                            out var processInfo))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed.");
                    }

                    using (processInfo.ThreadHandle)
                    using (processInfo.ProcessHandle)
                    {
                        return Process.GetProcessById((int)processInfo.ProcessId);
                    }
                }
                finally
                {
                    DestroyEnvironmentBlock(environment);
                }
            }
        }
    }

    private void StopAgent()
    {
        if (_agent is not null)
        {
            try
            {
                if (!_agent.HasExited)
                {
                    _agent.Kill(entireProcessTree: true);
                    _agent.WaitForExit(5000);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Unable to stop AudioPilot agent cleanly.");
            }
            finally
            {
                _agent.Dispose();
            }
        }

        _agent = null;
        _agentSessionId = NoActiveSession;
    }

    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public SafeFileHandle ProcessHandle;
        public SafeFileHandle ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(uint sessionId, out SafeFileHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        SafeFileHandle existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out SafeFileHandle newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(
        out IntPtr environment,
        SafeFileHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(
        SafeFileHandle token,
        string applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);
}
