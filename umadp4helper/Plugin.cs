using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SamplePlugin.Windows;
using System.Numerics;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/p4helper";
    private const string ShotCallerCommandName = "/p4shotcaller";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("UMAD P4 Helper");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private ShotCallerWindow ShotCallerWindow { get; init; }

    private Hook<ActionEffectHandler.Delegates.Receive>? actionEffectHook;

    public Plugin()
    {
        Configuration =
            PluginInterface.GetPluginConfig() as Configuration ??
            new Configuration();

        var goatImagePath = Path.Combine(
            PluginInterface.AssemblyLocation.Directory?.FullName!,
            "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        // ShotCallerWindow now requires the Plugin instance.
        ShotCallerWindow = new ShotCallerWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ShotCallerWindow);

        unsafe
        {
            actionEffectHook =
                GameInteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                    ActionEffectHandler.MemberFunctionPointers.Receive,
                    OnReceiveActionEffect);

            actionEffectHook.Enable();
        }

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the UMAD P4 Helper."
            });

        CommandManager.AddHandler(
            ShotCallerCommandName,
            new CommandInfo(OnShotCallerCommand)
            {
                HelpMessage = "Toggle the optional UMAD P4 Shot Caller window."
            });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"{PluginInterface.Manifest.Name} loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        actionEffectHook?.Dispose();

        WindowSystem.RemoveAllWindows();

        ShotCallerWindow.Dispose();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(ShotCallerCommandName);
        CommandManager.RemoveHandler(CommandName);
    }

    private unsafe void OnReceiveActionEffect(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (header is not null)
        {
            MainWindow.ProcessActionEffect(
                header->ActionId,
                casterEntityId);
        }

        actionEffectHook?.Original(
            casterEntityId,
            casterPtr,
            targetPos,
            header,
            effects,
            targetEntityIds);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnShotCallerCommand(string command, string args)
    {
        if (!Configuration.ShotCallerEnabled)
        {
            Log.Information(
                "Shot Caller is disabled. Enable it in UMAD P4 Helper settings.");
            return;
        }

        ShotCallerWindow.Toggle();
    }

    public void ToggleConfigUi()
    {
        ConfigWindow.Toggle();
    }

    public void ToggleMainUi()
    {
        MainWindow.Toggle();
    }

    public void OpenShotCallerUi()
    {
        if (Configuration.ShotCallerEnabled)
        {
            ShotCallerWindow.IsOpen = true;
        }
    }

    public void CloseShotCallerUi()
    {
        ShotCallerWindow.IsOpen = false;
    }
}
