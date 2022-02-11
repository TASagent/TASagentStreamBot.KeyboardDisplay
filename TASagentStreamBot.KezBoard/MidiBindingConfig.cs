using System.Text.Json;
using TASagentTwitchBot.Plugin.XInput;

namespace TASagentStreamBot.KezBoard;

public class MidiBindingConfig
{
    private static string ConfigFilePath => BGC.IO.DataManagement.PathForDataFile("Config", "MidiBindingConfig.json");
    private static readonly object _lock = new object();

    public Dictionary<int, DirectXKeyStrokes> KeyMapping { get; set; } = new Dictionary<int, DirectXKeyStrokes>();

    public string MidiDevice { get; set; } = "";

    public static MidiBindingConfig GetConfig()
    {
        MidiBindingConfig config;
        if (File.Exists(ConfigFilePath))
        {
            //Load existing config
            config = JsonSerializer.Deserialize<MidiBindingConfig>(File.ReadAllText(ConfigFilePath))!;
        }
        else
        {
            config = new MidiBindingConfig();
        }

        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config));

        return config;
    }

    public void Serialize()
    {
        lock (_lock)
        {
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(this));
        }
    }
}