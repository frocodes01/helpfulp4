using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private enum Truth
    {
        Unknown,
        Real,
        Fake
    }

    private enum Duration
    {
        Unknown,
        Short,
        Long
    }

    private enum AccelTiming
    {
        Unknown,
        Short,
        Long,
        None
    }

    private enum ChaosElement
    {
        Unknown,
        Fire,
        Water
    }

    // ============================================================
    // AUTO MODE
    // ============================================================

    private const uint BossTellStatusId = 2056;
    private const uint WaterStatusId = 5545;
    private const uint LightningStatusId = 5544;
    private const uint GazeStatusId = 5543;
    private const uint AccelStatusId = 5546;
    private const uint TsunamiStatusId = 5548;
    private const uint InfernoStatusId = 5547;

    // Embedded playback icons. These are built into the plugin DLL so the
    // custom-repo ZIP does not need to ship loose PNG files.
    private const string IconResourcePrefix = "Helpfulp4.Icons.";
    private const string AccelIcon = "acceleration.png";
    private const string ForkedLightningIcon = "forked_lightning.png";
    private const string CompressedWaterIcon = "compressed_water.png";
    private const string GazeIcon = "gaze.png";
    private const string TsunamiIcon = "tsunami.png";
    private const string InfernoIcon = "inferno.png";
    private const string DonutIcon = "donut.png";
    private const string BaitMidIcon = "bait_mid_chariot.png";

    // The reference helper treats a tell as relevant for 20 seconds after it is seen.
    private static readonly TimeSpan AutoTellFreshness = TimeSpan.FromSeconds(20);

    // DMU short Water/Lightning/Accel timers are 50s or below when applied,
    // while the long versions are over one minute. 60s cleanly separates them.
    private const float ShortLongThresholdSeconds = 60.0f;

    private readonly Plugin plugin;
    private readonly HashSet<string> activeBossTellKeysLastFrame = new(StringComparer.Ordinal);
    private readonly HashSet<uint> activeLocalStatusesLastFrame = new();

    private int neoTellCount;
    private int chaosTellCount;
    private int gazeCount;
    private int latestNeoTellIndex;
    private int latestChaosTellIndex;
    private DateTime latestNeoTellAtUtc = DateTime.MinValue;
    private DateTime latestChaosTellAtUtc = DateTime.MinValue;
    private bool autoWasEnabled;
    private string autoLastEvent = "Waiting for P4...";
    private string autoActiveDebuffs = "No watched debuffs detected.";

    // ============================================================
    // CURRENT STATE
    // ============================================================

    // Neo #1
    private Truth neo1Truth = Truth.Unknown;
    private Duration neo1Duration = Duration.Unknown;
    private AccelTiming neo1Accel = AccelTiming.Unknown;

    // Chaos #1
    private Truth chaos1Truth = Truth.Unknown;
    private ChaosElement chaos1Element = ChaosElement.Unknown;

    // Neo #2
    private Truth neo2Truth = Truth.Unknown;
    private Duration neo2Duration = Duration.Unknown;
    private AccelTiming neo2Accel = AccelTiming.Unknown;

    // Chaos #2
    private Truth chaos2Truth = Truth.Unknown;
    private ChaosElement chaos2Element = ChaosElement.Unknown;

    // Mana
    private Truth manaChargeLightning = Truth.Unknown;
    private Truth manaChargeIce = Truth.Unknown;

    private Truth manaReleaseLightning = Truth.Unknown;
    private Truth manaReleaseIce = Truth.Unknown;

    // ============================================================
    // UNDO STATE
    // ============================================================

    private sealed class StateSnapshot
    {
        public Truth Neo1Truth;
        public Duration Neo1Duration;
        public AccelTiming Neo1Accel;

        public Truth Chaos1Truth;
        public ChaosElement Chaos1Element;

        public Truth Neo2Truth;
        public Duration Neo2Duration;
        public AccelTiming Neo2Accel;

        public Truth Chaos2Truth;
        public ChaosElement Chaos2Element;

        public Truth ManaChargeLightning;
        public Truth ManaChargeIce;

        public Truth ManaReleaseLightning;
        public Truth ManaReleaseIce;
    }

    private StateSnapshot? undoState;

    private void SaveUndoState()
    {
        undoState = new StateSnapshot
        {
            Neo1Truth = neo1Truth,
            Neo1Duration = neo1Duration,
            Neo1Accel = neo1Accel,

            Chaos1Truth = chaos1Truth,
            Chaos1Element = chaos1Element,

            Neo2Truth = neo2Truth,
            Neo2Duration = neo2Duration,
            Neo2Accel = neo2Accel,

            Chaos2Truth = chaos2Truth,
            Chaos2Element = chaos2Element,

            ManaChargeLightning = manaChargeLightning,
            ManaChargeIce = manaChargeIce,

            ManaReleaseLightning = manaReleaseLightning,
            ManaReleaseIce = manaReleaseIce
        };
    }

    private void UndoLastAction()
    {
        if (undoState == null)
        {
            return;
        }

        neo1Truth = undoState.Neo1Truth;
        neo1Duration = undoState.Neo1Duration;
        neo1Accel = undoState.Neo1Accel;

        chaos1Truth = undoState.Chaos1Truth;
        chaos1Element = undoState.Chaos1Element;

        neo2Truth = undoState.Neo2Truth;
        neo2Duration = undoState.Neo2Duration;
        neo2Accel = undoState.Neo2Accel;

        chaos2Truth = undoState.Chaos2Truth;
        chaos2Element = undoState.Chaos2Element;

        manaChargeLightning = undoState.ManaChargeLightning;
        manaChargeIce = undoState.ManaChargeIce;

        manaReleaseLightning = undoState.ManaReleaseLightning;
        manaReleaseIce = undoState.ManaReleaseIce;

        // One-level undo.
        undoState = null;
    }

    // ============================================================
    // COLOURS
    // ============================================================

    private static readonly Vector4 RealSelected =
        new(0.04f, 0.12f, 0.24f, 1.00f);

    private static readonly Vector4 RealHovered =
        new(0.07f, 0.19f, 0.36f, 1.00f);

    private static readonly Vector4 RealUnselected =
        new(0.07f, 0.10f, 0.16f, 1.00f);

    private static readonly Vector4 FakeSelected =
        new(0.60f, 0.03f, 0.06f, 1.00f);

    private static readonly Vector4 FakeHovered =
        new(0.78f, 0.05f, 0.08f, 1.00f);

    private static readonly Vector4 FakeUnselected =
        new(0.18f, 0.07f, 0.08f, 1.00f);

    private static readonly Vector4 White =
        new(1.00f, 1.00f, 1.00f, 1.00f);

    private static readonly Vector4 Yellow =
        new(1.00f, 0.88f, 0.10f, 1.00f);

    private static readonly Vector4 GenericSelected =
        new(0.12f, 0.30f, 0.50f, 1.00f);

    private static readonly Vector4 GenericHovered =
        new(0.16f, 0.38f, 0.62f, 1.00f);

    private static readonly Vector4 CardBackground =
        new(0.025f, 0.04f, 0.07f, 0.65f);

    private static readonly Vector4 CardBorder =
        new(0.22f, 0.31f, 0.43f, 1.00f);

    // ============================================================
    // WINDOW
    // ============================================================

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("UMAD P4 Helper##P4HelperMain")
    {
        this.plugin = plugin;
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.DutyState.DutyStarted += OnDutyReset;
        Plugin.DutyState.DutyWiped += OnDutyReset;
        Plugin.DutyState.DutyRecommenced += OnDutyReset;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(390, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
        Plugin.DutyState.DutyRecommenced -= OnDutyReset;
        Plugin.DutyState.DutyWiped -= OnDutyReset;
        Plugin.DutyState.DutyStarted -= OnDutyReset;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    public override void Draw()
    {
        DrawTopBar();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var availableWidth =
            ImGui.GetContentRegionAvail().X;

        var useTwoColumns =
            availableWidth >=
            720 * ImGuiHelpers.GlobalScale;

        if (useTwoColumns)
        {
            DrawTwoColumnLayout();
        }
        else
        {
            DrawOneColumnLayout();
        }
    }

    private void DrawTopBar()
    {
        ImGui.Text("UMAD P4 Helper");

        // Undo
        ImGui.SameLine();

        var undoWidth =
            80 * ImGuiHelpers.GlobalScale;

        var resetWidth =
            80 * ImGuiHelpers.GlobalScale;

        var spacing =
            8 * ImGuiHelpers.GlobalScale;

        var available =
            ImGui.GetContentRegionAvail().X;

        var totalButtonWidth =
            undoWidth + resetWidth + spacing;

        if (available > totalButtonWidth)
        {
            ImGui.SetCursorPosX(
                ImGui.GetCursorPosX()
                + available
                - totalButtonWidth);
        }

        if (ImGui.Button(
            undoState != null
                ? "Undo"
                : "Undo",
            new Vector2(undoWidth, 0)))
        {
            UndoLastAction();
        }

        ImGui.SameLine();

        if (ImGui.Button(
            "Reset",
            new Vector2(resetWidth, 0)))
        {
            SaveUndoState();
            ResetAll(false);
            ResetAutoTracking();
        }

        ImGui.Spacing();
        ImGui.Text("Mode:");
        ImGui.SameLine();

        var autoMode = plugin.Configuration.AutoMode;

        if (ImGui.RadioButton("Manual", !autoMode))
        {
            plugin.Configuration.AutoMode = false;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();

        if (ImGui.RadioButton("Auto (Experimental)", autoMode))
        {
            if (!plugin.Configuration.AutoMode)
            {
                plugin.Configuration.AutoMode = true;
                plugin.Configuration.Save();
                BeginAutoMode();
            }
        }

        if (plugin.Configuration.AutoMode)
        {
            ImGui.TextDisabled($"Auto: {autoLastEvent}");
            ImGui.TextDisabled($"Detected: {autoActiveDebuffs}");
        }
    }

    // ============================================================
    // AUTO DETECTION
    // ============================================================

    private void BeginAutoMode()
    {
        ResetAutoTracking();
        ResetAll(false);
        autoWasEnabled = true;
        autoLastEvent = "Auto enabled - waiting for Neo/Chaos tells.";
    }

    private void ResetAutoTracking()
    {
        activeBossTellKeysLastFrame.Clear();
        activeLocalStatusesLastFrame.Clear();
        neoTellCount = 0;
        chaosTellCount = 0;
        gazeCount = 0;
        latestNeoTellIndex = 0;
        latestChaosTellIndex = 0;
        latestNeoTellAtUtc = DateTime.MinValue;
        latestChaosTellAtUtc = DateTime.MinValue;
        autoLastEvent = "Waiting for P4...";
        autoActiveDebuffs = "No watched debuffs detected.";
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var enabled = plugin.Configuration.AutoMode;

        if (!enabled)
        {
            autoWasEnabled = false;
            return;
        }

        if (!autoWasEnabled)
        {
            BeginAutoMode();
        }

        RefreshAutoBossTells();
        RefreshAutoLocalStatuses();
    }

    private void OnDutyReset(IDutyStateEventArgs args)
    {
        ResetAutoTracking();

        if (plugin.Configuration.AutoMode)
        {
            ResetAll(false);
            autoWasEnabled = true;
            autoLastEvent = "Pull reset - waiting for P4...";
        }
    }

    private void RefreshAutoBossTells()
    {
        var activeTellKeys = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        foreach (var gameObject in Plugin.ObjectTable)
        {
            if (gameObject == null || !gameObject.IsValid() || gameObject is not ICharacter character || character is not IBattleChara battleChara)
            {
                continue;
            }

            var name = character.Name.TextValue;
            var isNeo = string.Equals(name, "Neo Exdeath", StringComparison.OrdinalIgnoreCase);
            var isChaos = string.Equals(name, "Chaos", StringComparison.OrdinalIgnoreCase);

            if (!isNeo && !isChaos)
            {
                continue;
            }

            foreach (var status in battleChara.StatusList)
            {
                if (status.StatusId != BossTellStatusId)
                {
                    continue;
                }

                var truth = TruthFromTellParam(status.Param);
                var bossName = isNeo ? "Neo" : "Chaos";
                var activeKey = $"{bossName}:{status.Param}";
                activeTellKeys.Add(activeKey);

                if (!activeBossTellKeysLastFrame.Contains(activeKey))
                {
                    if (isNeo)
                    {
                        neoTellCount++;
                        if (neoTellCount <= 2)
                        {
                            latestNeoTellIndex = neoTellCount;
                            latestNeoTellAtUtc = now;
                            ApplyNeoTruth(neoTellCount, truth);
                            autoLastEvent = $"Neo #{neoTellCount} {TruthToString(truth)} (tell {status.Param}).";
                        }
                    }
                    else
                    {
                        chaosTellCount++;
                        if (chaosTellCount <= 2)
                        {
                            latestChaosTellIndex = chaosTellCount;
                            latestChaosTellAtUtc = now;
                            ApplyChaosTruth(chaosTellCount, truth);
                            autoLastEvent = $"Chaos #{chaosTellCount} {TruthToString(truth)} (tell {status.Param}).";
                        }
                    }
                }

                break;
            }
        }

        activeBossTellKeysLastFrame.Clear();
        foreach (var key in activeTellKeys)
        {
            activeBossTellKeysLastFrame.Add(key);
        }
    }

    private void RefreshAutoLocalStatuses()
    {
        var now = DateTime.UtcNow;
        var currentStatusIds = new HashSet<uint>();
        var display = new List<string>();

        var localContentId = Plugin.PlayerState.ContentId;
        var localEntityId = Plugin.ObjectTable.LocalPlayer?.EntityId ?? 0;

        foreach (var member in Plugin.PartyList)
        {
            var isLocal =
                (localContentId != 0 && member.ContentId == localContentId) ||
                (localEntityId != 0 && member.EntityId == localEntityId);

            if (!isLocal)
            {
                continue;
            }

            foreach (var status in member.Statuses)
            {
                if (!IsAutoWatchedStatus(status.StatusId))
                {
                    continue;
                }

                currentStatusIds.Add(status.StatusId);
                display.Add($"{GetAutoStatusName(status.StatusId)} {status.RemainingTime:0.0}s");

                if (!activeLocalStatusesLastFrame.Contains(status.StatusId))
                {
                    HandleNewAutoStatus(status.StatusId, status.RemainingTime, now);
                }
            }

            break;
        }

        autoActiveDebuffs = display.Count > 0
            ? string.Join(" | ", display)
            : "No watched debuffs detected.";

        activeLocalStatusesLastFrame.Clear();
        foreach (var statusId in currentStatusIds)
        {
            activeLocalStatusesLastFrame.Add(statusId);
        }
    }

    private void HandleNewAutoStatus(uint statusId, float remainingTime, DateTime now)
    {
        if (statusId is WaterStatusId or LightningStatusId)
        {
            if (!HasFreshNeoTell(now))
            {
                autoLastEvent = $"{GetAutoStatusName(statusId)} appeared, but no fresh Neo tell was captured.";
                return;
            }

            var duration = remainingTime < ShortLongThresholdSeconds
                ? Duration.Short
                : Duration.Long;

            ApplyNeoDuration(latestNeoTellIndex, duration);
            autoLastEvent = $"Neo #{latestNeoTellIndex}: {GetAutoStatusName(statusId)} {duration} ({remainingTime:0.0}s).";
            return;
        }

        if (statusId == AccelStatusId)
        {
            if (!HasFreshNeoTell(now))
            {
                autoLastEvent = "Accel appeared, but no fresh Neo tell was captured.";
                return;
            }

            var timing = remainingTime < ShortLongThresholdSeconds
                ? AccelTiming.Short
                : AccelTiming.Long;

            ApplyNeoAccel(latestNeoTellIndex, timing);
            autoLastEvent = $"Neo #{latestNeoTellIndex}: Accel {timing} ({remainingTime:0.0}s).";
            return;
        }

        if (statusId is TsunamiStatusId or InfernoStatusId)
        {
            if (!HasFreshChaosTell(now))
            {
                autoLastEvent = $"{GetAutoStatusName(statusId)} appeared, but no fresh Chaos tell was captured.";
                return;
            }

            var element = statusId == TsunamiStatusId
                ? ChaosElement.Water
                : ChaosElement.Fire;

            ApplyChaosElement(latestChaosTellIndex, element);
            autoLastEvent = $"Chaos #{latestChaosTellIndex}: {GetAutoStatusName(statusId)}.";
            return;
        }

        if (statusId == GazeStatusId)
        {
            gazeCount++;
            var gazeIndex = Math.Min(gazeCount, 2);
            autoLastEvent = $"Gaze #{gazeIndex} detected ({remainingTime:0.0}s).";
        }
    }

    private bool HasFreshNeoTell(DateTime now)
    {
        return latestNeoTellIndex is 1 or 2 &&
            now - latestNeoTellAtUtc <= AutoTellFreshness;
    }

    private bool HasFreshChaosTell(DateTime now)
    {
        return latestChaosTellIndex is 1 or 2 &&
            now - latestChaosTellAtUtc <= AutoTellFreshness;
    }

    private static bool IsAutoWatchedStatus(uint statusId)
    {
        return statusId is WaterStatusId or LightningStatusId or GazeStatusId or AccelStatusId or TsunamiStatusId or InfernoStatusId;
    }

    private static string GetAutoStatusName(uint statusId)
    {
        return statusId switch
        {
            WaterStatusId => "Water",
            LightningStatusId => "Lightning",
            GazeStatusId => "Gaze",
            AccelStatusId => "Accel",
            TsunamiStatusId => "Tsunami",
            InfernoStatusId => "Inferno",
            _ => $"Status {statusId}"
        };
    }

    private static Truth TruthFromTellParam(ushort param)
    {
        return param switch
        {
            1119 => Truth.Fake,
            1120 => Truth.Real,
            1121 => Truth.Fake,
            1122 => Truth.Real,
            _ => Truth.Unknown
        };
    }

    private void ApplyNeoTruth(int index, Truth truth)
    {
        if (truth == Truth.Unknown)
        {
            return;
        }

        if (index == 1)
        {
            neo1Truth = truth;
        }
        else if (index == 2)
        {
            neo2Truth = truth;
        }
    }

    private void ApplyChaosTruth(int index, Truth truth)
    {
        if (truth == Truth.Unknown)
        {
            return;
        }

        if (index == 1)
        {
            chaos1Truth = truth;
        }
        else if (index == 2)
        {
            chaos2Truth = truth;
        }
    }

    private void ApplyNeoDuration(int index, Duration duration)
    {
        if (index == 1)
        {
            neo1Duration = duration;
            neo2Duration = duration == Duration.Short ? Duration.Long : Duration.Short;
        }
        else if (index == 2)
        {
            neo2Duration = duration;
            neo1Duration = duration == Duration.Short ? Duration.Long : Duration.Short;
        }
    }

    private void ApplyNeoAccel(int index, AccelTiming timing)
    {
        if (index == 1)
        {
            neo1Accel = timing;
            neo2Accel = AccelTiming.None;
        }
        else if (index == 2)
        {
            neo2Accel = timing;
            neo1Accel = AccelTiming.None;
        }
    }

    private void ApplyChaosElement(int index, ChaosElement element)
    {
        if (index == 1)
        {
            chaos1Element = element;
            chaos2Element = element == ChaosElement.Fire ? ChaosElement.Water : ChaosElement.Fire;
        }
        else if (index == 2)
        {
            chaos2Element = element;
            chaos1Element = element == ChaosElement.Fire ? ChaosElement.Water : ChaosElement.Fire;
        }
    }

    // ============================================================
    // RESPONSIVE LAYOUT
    // ============================================================

    private void DrawTwoColumnLayout()
    {
        if (!ImGui.BeginTable(
                "P4MainTable",
                2,
                ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        ImGui.TableSetupColumn(
            "Inputs",
            ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableSetupColumn(
            "Playback",
            ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        DrawInputs();

        ImGui.TableSetColumnIndex(1);
        DrawResults();

        ImGui.EndTable();
    }

    private void DrawOneColumnLayout()
    {
        DrawInputs();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("PLAYBACK");

        ImGui.Spacing();

        DrawResults();
    }

    // ============================================================
    // INPUT SIDE
    // ============================================================

    private void DrawInputs()
    {
        DrawNeoSection(
            "Neo Exdeath #1",
            ref neo1Truth,
            ref neo1Duration,
            ref neo1Accel,
            true);

        SectionBreak();

        DrawChaosSection(
            "Chaos #1",
            ref chaos1Truth,
            ref chaos1Element,
            true);

        SectionBreak();

        DrawNeoSection(
            "Neo Exdeath #2",
            ref neo2Truth,
            ref neo2Duration,
            ref neo2Accel,
            false);

        SectionBreak();

        DrawChaosSection(
            "Chaos #2",
            ref chaos2Truth,
            ref chaos2Element,
            false);
    }

    private void DrawNeoSection(
        string title,
        ref Truth truth,
        ref Duration duration,
        ref AccelTiming accel,
        bool isNeo1)
    {
        ImGui.Text(title);
        ImGui.Spacing();

        DrawLabel("Cast");

        DrawTruthButtons(
            $"{title}_Truth",
            ref truth);

        DrawLabel("Water / Lightning");

        DrawDurationButtons(
            title,
            ref duration,
            isNeo1);

        DrawLabel("Accel");

        DrawAccelButtons(
            title,
            ref accel,
            isNeo1);
    }

    private void DrawChaosSection(
        string title,
        ref Truth truth,
        ref ChaosElement element,
        bool isChaos1)
    {
        ImGui.Text(title);
        ImGui.Spacing();

        DrawLabel("Cast");

        DrawTruthButtons(
            $"{title}_Truth",
            ref truth);

        DrawLabel("Fire / Water");

        DrawChaosElementButtons(
            title,
            ref element,
            isChaos1);
    }

    private void DrawLabel(string text)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text(text);

        ImGui.SameLine(
            145 * ImGuiHelpers.GlobalScale);
    }

    // ============================================================
    // REAL / FAKE BUTTONS
    // ============================================================

    private void DrawTruthButtons(
        string id,
        ref Truth value)
    {
        if (DrawRealButton(
            id,
            value == Truth.Real))
        {
            if (value != Truth.Real)
            {
                SaveUndoState();
                value = Truth.Real;
            }
        }

        ImGui.SameLine();

        if (DrawFakeButton(
            id,
            value == Truth.Fake))
        {
            if (value != Truth.Fake)
            {
                SaveUndoState();
                value = Truth.Fake;
            }
        }
    }

    private bool DrawRealButton(
        string id,
        bool selected)
    {
        var label =
            selected
                ? $"✓ Real##{id}_Real"
                : $"Real##{id}_Real";

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            selected
                ? RealSelected
                : RealUnselected);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            RealHovered);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            RealSelected);

        ImGui.PushStyleColor(
            ImGuiCol.Text,
            White);

        var pressed =
            ImGui.Button(label);

        ImGui.PopStyleColor(4);

        return pressed;
    }

    private bool DrawFakeButton(
        string id,
        bool selected)
    {
        var label =
            selected
                ? $"✓ Fake##{id}_Fake"
                : $"Fake##{id}_Fake";

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            selected
                ? FakeSelected
                : FakeUnselected);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            FakeHovered);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            FakeSelected);

        ImGui.PushStyleColor(
            ImGuiCol.Text,
            Yellow);

        var pressed =
            ImGui.Button(label);

        ImGui.PopStyleColor(4);

        return pressed;
    }

    // ============================================================
    // WATER / LIGHTNING DURATION
    // ============================================================

    private void DrawDurationButtons(
        string title,
        ref Duration duration,
        bool isNeo1)
    {
        if (DrawChoiceButton(
            $"Short##{title}_Short",
            duration == Duration.Short))
        {
            if (duration != Duration.Short)
            {
                SaveUndoState();

                duration =
                    Duration.Short;

                if (isNeo1)
                {
                    neo2Duration =
                        Duration.Long;
                }
                else
                {
                    neo1Duration =
                        Duration.Long;
                }
            }
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Long##{title}_Long",
            duration == Duration.Long))
        {
            if (duration != Duration.Long)
            {
                SaveUndoState();

                duration =
                    Duration.Long;

                if (isNeo1)
                {
                    neo2Duration =
                        Duration.Short;
                }
                else
                {
                    neo1Duration =
                        Duration.Short;
                }
            }
        }
    }

    // ============================================================
    // ACCEL
    // ============================================================

    private void DrawAccelButtons(
        string title,
        ref AccelTiming accel,
        bool isNeo1)
    {
        if (DrawChoiceButton(
            $"Short##{title}_AccelShort",
            accel == AccelTiming.Short))
        {
            if (accel != AccelTiming.Short)
            {
                SaveUndoState();

                accel =
                    AccelTiming.Short;

                // If this Neo owns the accel,
                // automatically mark the other Neo as None.
                if (isNeo1)
                {
                    neo2Accel =
                        AccelTiming.None;
                }
                else
                {
                    neo1Accel =
                        AccelTiming.None;
                }
            }
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Long##{title}_AccelLong",
            accel == AccelTiming.Long))
        {
            if (accel != AccelTiming.Long)
            {
                SaveUndoState();

                accel =
                    AccelTiming.Long;

                // If this Neo owns the accel,
                // automatically mark the other Neo as None.
                if (isNeo1)
                {
                    neo2Accel =
                        AccelTiming.None;
                }
                else
                {
                    neo1Accel =
                        AccelTiming.None;
                }
            }
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"None##{title}_AccelNone",
            accel == AccelTiming.None))
        {
            if (accel != AccelTiming.None)
            {
                SaveUndoState();

                accel =
                    AccelTiming.None;
            }
        }
    }

    // ============================================================
    // CHAOS ELEMENT BUTTONS
    // ============================================================

    private void DrawChaosElementButtons(
        string title,
        ref ChaosElement element,
        bool isChaos1)
    {
        if (DrawChoiceButton(
            $"Fire##{title}_Fire",
            element == ChaosElement.Fire))
        {
            if (element != ChaosElement.Fire)
            {
                SaveUndoState();

                element =
                    ChaosElement.Fire;

                if (isChaos1)
                {
                    chaos2Element =
                        ChaosElement.Water;
                }
                else
                {
                    chaos1Element =
                        ChaosElement.Water;
                }
            }
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Water##{title}_Water",
            element == ChaosElement.Water))
        {
            if (element != ChaosElement.Water)
            {
                SaveUndoState();

                element =
                    ChaosElement.Water;

                if (isChaos1)
                {
                    chaos2Element =
                        ChaosElement.Fire;
                }
                else
                {
                    chaos1Element =
                        ChaosElement.Fire;
                }
            }
        }
    }

    // ============================================================
    // GENERIC SELECTED BUTTON
    // ============================================================

    private bool DrawChoiceButton(
        string label,
        bool selected)
    {
        if (!selected)
        {
            return ImGui.Button(label);
        }

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            GenericSelected);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            GenericHovered);

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            GenericSelected);

        var visibleLabel =
            "✓ " + label;

        var pressed =
            ImGui.Button(visibleLabel);

        ImGui.PopStyleColor(3);

        return pressed;
    }

    // ============================================================
    // PLAYBACK
    // ============================================================

    private void DrawResults()
    {
        // Dense playback layout: the same shared state is used by Manual and Auto.
        // Cards intentionally avoid child windows so there are no per-card scrollbars.

        DrawCompactPairCard(
            "PlaybackNeo1",
            "NEO #1",
            () => DrawSpreadCompact("1st Spread", neo1Truth),
            () => DrawCompactMechanic(
                AccelIcon,
                "1st Accel",
                GetAccelCalloutForTiming(AccelTiming.Short)));

        DrawCompactPairCard(
            "PlaybackGaze1Mana1",
            "GAZE #1 + MANA CHARGE #1",
            () => DrawCompactMechanic(
                GazeIcon,
                "Gaze",
                GetGazeCallout(neo1Truth)),
            () => DrawCompactMana(
                "Lightning",
                "ManaChargeLightningCompact",
                ref manaChargeLightning));

        DrawCompactChaosCard(
            "PlaybackInferno",
            "CHAOS #1",
            InfernoIcon,
            "INFERNO / ENTROPY",
            GetInfernoCallout());

        DrawCompactNeoTwoCard();

        DrawCompactGazeTwoChaosCard();

        DrawCompactPairCard(
            "PlaybackManaReleases",
            "MANA RELEASE",
            () => DrawCompactMana(
                "Lightning",
                "ManaReleaseLightningCompact",
                ref manaReleaseLightning),
            () => DrawCompactMana(
                "Blizzard",
                "ManaReleaseIceCompact",
                ref manaReleaseIce));

        var lightningResult = ResolveMana(
            manaChargeLightning,
            manaReleaseLightning);

        var blizzardResult = ResolveMana(
            manaChargeIce,
            manaReleaseIce);

        DrawCompactCard(
            "PlaybackFinal",
            "FINAL",
            () =>
            {
                if (ImGui.BeginTable(
                    "PlaybackFinalResults",
                    2,
                    ImGuiTableFlags.SizingStretchSame |
                    ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DrawCompactTextResult(
                        "Lightning",
                        TruthToString(lightningResult));

                    ImGui.TableSetColumnIndex(1);
                    DrawCompactTextResult(
                        "Blizzard",
                        TruthToString(blizzardResult));

                    ImGui.EndTable();
                }

                var manaCallout = GetFinalManaCallout(
                    lightningResult,
                    blizzardResult);

                var tsunamiCallout = GetFinalTsunamiCallout();

                ImGui.Spacing();
                ImGui.TextDisabled("Final movement");
                ImGui.Text($"{manaCallout} + {tsunamiCallout}");
            });
    }

    private void DrawCompactNeoTwoCard()
    {
        DrawCompactCard(
            "PlaybackNeo2",
            "NEO #2 + MANA CHARGE #2",
            () =>
            {
                if (ImGui.BeginTable(
                    "PlaybackNeo2TopRow",
                    2,
                    ImGuiTableFlags.SizingStretchSame |
                    ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DrawSpreadCompact("2nd Stack / Spread", neo2Truth);

                    ImGui.TableSetColumnIndex(1);
                    DrawCompactMechanic(
                        AccelIcon,
                        "2nd Accel",
                        GetAccelCalloutForTiming(AccelTiming.Long));

                    ImGui.EndTable();
                }

                ImGui.Spacing();
                DrawCompactMana(
                    "Blizzard Charge",
                    "ManaChargeIceCompact",
                    ref manaChargeIce);
            });
    }

    private void DrawCompactGazeTwoChaosCard()
    {
        DrawCompactCard(
            "PlaybackGaze2Chaos2",
            "GAZE #2 + TSUNAMI",
            () =>
            {
                DrawCompactMechanic(
                    GazeIcon,
                    "2nd Gaze",
                    GetGazeCallout(neo2Truth));

                ImGui.Spacing();

                var resolution = GetTsunamiCallout();
                var movementIcon = GetMovementIcon(resolution);
                var movementText = GetMovementText(resolution);

                if (ImGui.BeginTable(
                    "PlaybackGaze2Chaos2Bottom",
                    2,
                    ImGuiTableFlags.SizingStretchSame |
                    ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DrawCompactMechanic(
                        TsunamiIcon,
                        "Mechanic",
                        "TSUNAMI / DYNAMIC FLUID");

                    ImGui.TableSetColumnIndex(1);
                    DrawCompactMechanic(
                        movementIcon,
                        "Resolve",
                        movementText);

                    ImGui.EndTable();
                }
            });
    }

    private void DrawCompactChaosCard(
        string id,
        string title,
        string mechanicIcon,
        string mechanicText,
        string resolution)
    {
        var movementIcon = GetMovementIcon(resolution);
        var movementText = GetMovementText(resolution);

        DrawCompactPairCard(
            id,
            title,
            () => DrawCompactMechanic(
                mechanicIcon,
                "Mechanic",
                mechanicText),
            () => DrawCompactMechanic(
                movementIcon,
                "Resolve",
                movementText));
    }

    private static string? GetMovementIcon(string resolution)
    {
        return resolution switch
        {
            "STAY" => DonutIcon,
            "SPREAD" => BaitMidIcon,
            _ => null
        };
    }

    private static string GetMovementText(string resolution)
    {
        return resolution switch
        {
            "STAY" => "DONUT / STAY MID",
            "SPREAD" => "BAIT MID → CHARIOT",
            _ => "?"
        };
    }

    private void DrawSpreadCompact(string label, Truth truth)
    {
        var result = GetSpreadCallout(truth);
        var icon = truth switch
        {
            Truth.Real => ForkedLightningIcon,
            Truth.Fake => CompressedWaterIcon,
            _ => null
        };

        DrawCompactMechanic(icon, label, result);
    }

    // ============================================================
    // COMPACT PLAYBACK CARDS
    // ============================================================

    private void DrawCompactPairCard(
        string id,
        string title,
        Action left,
        Action right)
    {
        DrawCompactCard(
            id,
            title,
            () =>
            {
                if (!ImGui.BeginTable(
                    $"{id}_Pair",
                    2,
                    ImGuiTableFlags.SizingStretchSame |
                    ImGuiTableFlags.NoSavedSettings))
                {
                    return;
                }

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                left();

                ImGui.TableSetColumnIndex(1);
                right();

                ImGui.EndTable();
            });
    }

    private void DrawCompactCard(
        string id,
        string title,
        Action contents)
    {
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, CardBorder);
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, CardBorder);

        if (ImGui.BeginTable(
            id,
            1,
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.SizingStretchProp |
            ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            ImGui.TextDisabled(title);
            ImGui.Spacing();
            contents();

            ImGui.EndTable();
        }

        ImGui.PopStyleColor(2);
        ImGui.Spacing();
    }

    private void DrawCompactMechanic(
        string? iconFile,
        string label,
        string result)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = 26 * scale;

        if (!string.IsNullOrWhiteSpace(iconFile))
        {
            DrawEmbeddedIcon(iconFile, iconSize);
            ImGui.SameLine();
        }

        ImGui.BeginGroup();
        ImGui.TextDisabled(label);
        ImGui.Text(result);
        ImGui.EndGroup();
    }

    private void DrawCompactMana(
        string label,
        string id,
        ref Truth value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        DrawTruthButtons(id, ref value);
    }

    private void DrawCompactTextResult(string label, string result)
    {
        ImGui.TextDisabled(label);
        ImGui.Text(result);
    }

    private void DrawEmbeddedIcon(string fileName, float size)
    {
        var resourceName = IconResourcePrefix + fileName;
        var shared = Plugin.TextureProvider.GetFromManifestResource(
            typeof(MainWindow).Assembly,
            resourceName);

        var wrap = shared.GetWrapOrDefault();
        if (wrap != null)
        {
            ImGui.Image(wrap.Handle, new Vector2(size, size));
        }
        else
        {
            ImGui.Dummy(new Vector2(size, size));
        }
    }

    private void DrawResultLine(
        string label,
        string result)
    {
        if (string.IsNullOrEmpty(label))
        {
            ImGui.Text(result);
            return;
        }

        ImGui.Text($"{label}:");

        ImGui.SameLine();

        ImGui.Text(result);
    }

    private void DrawManaInputBreak(
        string title,
        string id,
        ref Truth value)
    {
        ImGui.Spacing();

        ImGui.Text(title);

        ImGui.Spacing();

        DrawTruthButtons(
            id,
            ref value);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void SectionBreak()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    // ============================================================
    // NEO CALCULATIONS
    // ============================================================

    private string GetSpreadCallout(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real =>
                "LIGHTNING",

            Truth.Fake =>
                "WATER",

            _ =>
                "?"
        };
    }

    private string GetGazeCallout(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real =>
                "LOOK AWAY",

            Truth.Fake =>
                "LOOK IN",

            _ =>
                "?"
        };
    }

    // ============================================================
    // ACCEL CALCULATIONS
    // ============================================================

    private string GetAccelCalloutForTiming(
        AccelTiming timing)
    {
        var neo1Matches =
            neo1Accel == timing;

        var neo2Matches =
            neo2Accel == timing;

        if (neo1Matches && neo2Matches)
        {
            return "CHECK ACCEL INPUTS";
        }

        if (neo1Matches)
        {
            return BuildAccelCallout(
                neo1Truth,
                timing);
        }

        if (neo2Matches)
        {
            return BuildAccelCallout(
                neo2Truth,
                timing);
        }

        return "No accel this set";
    }

    private string BuildAccelCallout(
        Truth truth,
        AccelTiming timing)
    {
        if (truth == Truth.Unknown)
        {
            return "?";
        }

        var timingText =
            timing == AccelTiming.Short
                ? "1st"
                : "2nd";

        var mechanic =
            truth == Truth.Real
                ? "STILLNESS"
                : "MOTION";

        return $"{timingText} {mechanic}";
    }

    // ============================================================
    // CHAOS CALCULATIONS
    // ============================================================

    private string GetInfernoCallout()
    {
        var truth =
            GetChaosTruthForElement(
                ChaosElement.Fire);

        return truth switch
        {
            Truth.Real =>
                "SPREAD",

            Truth.Fake =>
                "STAY",

            _ =>
                "?"
        };
    }

    private string GetTsunamiCallout()
    {
        var truth =
            GetChaosTruthForElement(
                ChaosElement.Water);

        return truth switch
        {
            Truth.Real =>
                "STAY",

            Truth.Fake =>
                "SPREAD",

            _ =>
                "?"
        };
    }

    private string GetFinalTsunamiCallout()
    {
        var tsunamiCallout =
            GetTsunamiCallout();

        return tsunamiCallout switch
        {
            "STAY" =>
                "DONUT",

            "SPREAD" =>
                "CHARIOT",

            _ =>
                "?"
        };
    }

    private Truth GetChaosTruthForElement(
        ChaosElement element)
    {
        if (chaos1Element == element)
        {
            return chaos1Truth;
        }

        if (chaos2Element == element)
        {
            return chaos2Truth;
        }

        return Truth.Unknown;
    }

    // ============================================================
    // MANA CALCULATIONS
    // ============================================================

    private Truth ResolveMana(
        Truth charge,
        Truth release)
    {
        if (charge == Truth.Unknown ||
            release == Truth.Unknown)
        {
            return Truth.Unknown;
        }

        return charge == release
            ? Truth.Real
            : Truth.Fake;
    }

    private string GetFinalManaCallout(
        Truth lightning,
        Truth blizzard)
    {
        if (lightning == Truth.Unknown ||
            blizzard == Truth.Unknown)
        {
            return "?";
        }

        if (lightning == Truth.Real &&
            blizzard == Truth.Real)
        {
            return "OUT OF BOTH";
        }

        if (lightning == Truth.Fake &&
            blizzard == Truth.Fake)
        {
            return "IN BOTH";
        }

        if (lightning == Truth.Real &&
            blizzard == Truth.Fake)
        {
            return "IN BLIZZARD";
        }

        return "IN LIGHTNING";
    }

    private string TruthToString(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real =>
                "REAL",

            Truth.Fake =>
                "FAKE",

            _ =>
                "?"
        };
    }

    // ============================================================
    // RESET
    // ============================================================

    private void ResetAll(bool clearUndo = true)
    {
        neo1Truth =
            Truth.Unknown;

        neo1Duration =
            Duration.Unknown;

        neo1Accel =
            AccelTiming.Unknown;

        chaos1Truth =
            Truth.Unknown;

        chaos1Element =
            ChaosElement.Unknown;

        neo2Truth =
            Truth.Unknown;

        neo2Duration =
            Duration.Unknown;

        neo2Accel =
            AccelTiming.Unknown;

        chaos2Truth =
            Truth.Unknown;

        chaos2Element =
            ChaosElement.Unknown;

        manaChargeLightning =
            Truth.Unknown;

        manaChargeIce =
            Truth.Unknown;

        manaReleaseLightning =
            Truth.Unknown;

        manaReleaseIce =
            Truth.Unknown;

        if (clearUndo)
        {
            undoState = null;
        }
    }
}