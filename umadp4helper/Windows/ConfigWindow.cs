using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("UMAD P4 Helper Settings###UMADP4Config")
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(700, 700)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("UMAD P4 Helper Settings");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Expanded Helper");
        ImGui.TextDisabled("Optional automatic helpers shown only in Expanded view.");

        ImGui.Spacing();

        var antilightEnabled = configuration.AntilightEnabled;
        if (ImGui.Checkbox("Enable Antilight Helper", ref antilightEnabled))
        {
            configuration.AntilightEnabled = antilightEnabled;
            configuration.Save();
        }

        var autoDebug = configuration.AutoDebug;
        if (ImGui.Checkbox("Show Auto / Antilight debug info", ref autoDebug))
        {
            configuration.AutoDebug = autoDebug;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Shot Caller");
        ImGui.TextDisabled("Optional window that lists the party members who need to spread on the 1st and 2nd resolves.");

        ImGui.Spacing();

        var enabled = configuration.ShotCallerEnabled;
        if (ImGui.Checkbox("Enable Shot Caller", ref enabled))
        {
            configuration.ShotCallerEnabled = enabled;
            if (!enabled)
            {
                plugin.CloseShotCallerUi();
            }
            configuration.Save();
        }

        if (!configuration.ShotCallerEnabled)
            ImGui.BeginDisabled();

        var autoOpen = configuration.ShotCallerAutoOpen;
        if (ImGui.Checkbox("Auto-open when P4 Neo tells begin", ref autoOpen))
        {
            configuration.ShotCallerAutoOpen = autoOpen;
            configuration.Save();
        }

        var debug = configuration.ShotCallerShowDebug;
        if (ImGui.Checkbox("Show Shot Caller debug info", ref debug))
        {
            configuration.ShotCallerShowDebug = debug;
            configuration.Save();
        }

        ImGui.Spacing();

        if (ImGui.Button("Open Shot Caller"))
        {
            plugin.OpenShotCallerUi();
        }

        if (!configuration.ShotCallerEnabled)
            ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextDisabled("You can also use /p4shotcaller to toggle the Shot Caller window.");
    }
}
