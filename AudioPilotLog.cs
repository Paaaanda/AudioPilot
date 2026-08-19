namespace AudioPilot;

internal sealed class AudioPilotLog
{
    private readonly object _gate = new();
    private readonly string _path;

    public AudioPilotLog()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AudioPilot");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "AudioPilot.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            try
            {
                File.AppendAllText(_path, line);
            }
            catch
            {
                // Logging must never stop audio switching.
            }
        }
    }
}
