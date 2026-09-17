using System;
using Dalamud.Configuration;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Main helper
    public bool AutoMode { get; set; } = false;

    // Expanded helper / automatic mechanic detection
    public bool AntilightEnabled { get; set; } = false;
    public bool AutoDebug { get; set; } = true;

    // Shot Caller
    public bool ShotCallerEnabled { get; set; } = false;
    public bool ShotCallerAutoOpen { get; set; } = false;
    public bool ShotCallerShowDebug { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
