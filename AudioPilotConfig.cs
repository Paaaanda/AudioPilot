using System.Text.Json;

namespace AudioPilot;

internal sealed record AudioPilotConfig
{
    public string HeadsetNameContains { get; init; } = "ROG";
    public string FallbackNameContains { get; init; } = "Speakers";
    public int SwitchDelayMilliseconds { get; init; } = 750;

    public static AudioPilotConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到配置文件：{path}");
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AudioPilotConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("appsettings.json 内容无效。");
    }
}
