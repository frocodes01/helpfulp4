using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.DutyState;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Windows;

public sealed class ShotCallerWindow : Window, IDisposable
{
    private enum Truth { Unknown, Real, Fake }
    private enum Duration { Unknown, Short, Long }
    private enum Element { Unknown, Water, Lightning }

    private sealed class PlayerElementAssignment
    {
        public string Key = string.Empty;
        public string Name = string.Empty;
        public Element Element;
        public Duration Duration;
    }

    private const uint BossTellStatusId = 2056;
    private const uint WaterStatusId = 5545;
    private const uint LightningStatusId = 5544;

    private const float ShortLongThresholdSeconds = 60.0f;
    private static readonly TimeSpan TellFreshness = TimeSpan.FromSeconds(20);

    private readonly Plugin plugin;

    private readonly HashSet<string> activeBossTellKeysLastFrame =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> activeElementKeysLastFrame =
        new(StringComparer.Ordinal);

    // Key by a stable party identity string instead of EntityId.
    // This avoids dropping members if EntityId is unavailable/zero in PartyList.
    private readonly Dictionary<string, PlayerElementAssignment> playerAssignments =
        new(StringComparer.Ordinal);

    private readonly List<string> partyDebugLines = new();

    private int neoTellCount;
    private int latestNeoTellIndex;
    private DateTime latestNeoTellAtUtc = DateTime.MinValue;

    private Truth neo1Truth = Truth.Unknown;
    private Truth neo2Truth = Truth.Unknown;
    private Duration neo1Duration = Duration.Unknown;
    private Duration neo2Duration = Duration.Unknown;

    private string lastEvent = "Waiting for Neo Exdeath...";
    private int visiblePartyMembers;

    public ShotCallerWindow(Plugin plugin)
        : base("UMAD P4 Shot Caller##P4ShotCaller")
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 220),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.DutyState.DutyStarted += OnDutyReset;
        Plugin.DutyState.DutyWiped += OnDutyReset;
        Plugin.DutyState.DutyRecommenced += OnDutyReset;
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
        if (!plugin.Configuration.ShotCallerEnabled)
        {
            ImGui.TextDisabled(
                "Shot Caller is disabled. Enable it in UMAD P4 Helper settings.");
            return;
        }

        DrawResolveSection("1st Resolve (Short)", Duration.Short);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawResolveSection("2nd Resolve (Long)", Duration.Long);

        if (plugin.Configuration.ShotCallerShowDebug)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextDisabled(
                $"PartyList.Length: {Plugin.PartyList.Length} | Readable members: {visiblePartyMembers}");

            ImGui.TextDisabled(lastEvent);

            if (ImGui.CollapsingHeader("Debug"))
            {
                ImGui.Text(
                    $"Neo #1: {TruthText(neo1Truth)} / {DurationText(neo1Duration)}");
                ImGui.Text(
                    $"Neo #2: {TruthText(neo2Truth)} / {DurationText(neo2Duration)}");
                ImGui.Text(
                    $"Captured element players: {playerAssignments.Count}");

                ImGui.Spacing();
                ImGui.Text("Party status scan:");

                foreach (var line in partyDebugLines)
                {
                    ImGui.TextWrapped(line);
                }

                if (playerAssignments.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text("Captured:");

                    foreach (var a in
                        playerAssignments.Values.OrderBy(x => x.Name))
                    {
                        ImGui.BulletText(
                            $"{a.Name}: {a.Duration} {a.Element}");
                    }
                }
            }
        }
    }

    private void DrawResolveSection(
        string title,
        Duration duration)
    {
        ImGui.Text(title);

        var truth = GetNeoTruthForDuration(duration);

        if (truth == Truth.Unknown)
        {
            ImGui.TextDisabled(
                "Waiting for Neo truth / timing...");
            return;
        }

        var spreadingElement =
            truth == Truth.Real
                ? Element.Lightning
                : Element.Water;

        ImGui.TextDisabled($"{spreadingElement} spreads");

        var spreaders =
            playerAssignments.Values
                .Where(
                    x =>
                        x.Duration == duration &&
                        x.Element == spreadingElement)
                .OrderBy(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (spreaders.Count == 0)
        {
            ImGui.TextDisabled(
                "Waiting for spread players...");
            return;
        }

        ImGui.SetWindowFontScale(1.15f);

        foreach (var player in spreaders)
        {
            ImGui.BulletText(player.Name);
        }

        ImGui.SetWindowFontScale(1.00f);

        if (spreaders.Count > 2)
        {
            ImGui.TextDisabled(
                $"Warning: detected {spreaders.Count} spread players; expected at most 2.");
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!plugin.Configuration.ShotCallerEnabled)
        {
            return;
        }

        RefreshNeoTells();
        RefreshPartyElementStatuses();
    }

    private void OnDutyReset(IDutyStateEventArgs args)
    {
        ResetTracking();

        if (plugin.Configuration.ShotCallerAutoOpen)
        {
            IsOpen = false;
        }
    }

    private void ResetTracking()
    {
        activeBossTellKeysLastFrame.Clear();
        activeElementKeysLastFrame.Clear();
        playerAssignments.Clear();
        partyDebugLines.Clear();

        neoTellCount = 0;
        latestNeoTellIndex = 0;
        latestNeoTellAtUtc = DateTime.MinValue;

        neo1Truth = Truth.Unknown;
        neo2Truth = Truth.Unknown;
        neo1Duration = Duration.Unknown;
        neo2Duration = Duration.Unknown;

        visiblePartyMembers = 0;
        lastEvent = "Waiting for Neo Exdeath...";
    }

    private void RefreshNeoTells()
    {
        var activeTellKeys =
            new HashSet<string>(StringComparer.Ordinal);

        var now = DateTime.UtcNow;

        foreach (var gameObject in Plugin.ObjectTable)
        {
            if (gameObject == null ||
                !gameObject.IsValid() ||
                gameObject is not ICharacter character ||
                character is not IBattleChara battleChara)
            {
                continue;
            }

            if (!string.Equals(
                    character.Name.TextValue,
                    "Neo Exdeath",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var status in battleChara.StatusList)
            {
                if (status.StatusId != BossTellStatusId)
                {
                    continue;
                }

                var activeKey =
                    $"Neo:{status.Param}";

                activeTellKeys.Add(activeKey);

                if (!activeBossTellKeysLastFrame.Contains(activeKey))
                {
                    neoTellCount++;

                    if (neoTellCount <= 2)
                    {
                        latestNeoTellIndex = neoTellCount;
                        latestNeoTellAtUtc = now;

                        var truth =
                            TruthFromTellParam(status.Param);

                        ApplyNeoTruth(
                            neoTellCount,
                            truth);

                        lastEvent =
                            $"Neo #{neoTellCount} {TruthText(truth)} captured.";

                        if (neoTellCount == 1 &&
                            plugin.Configuration.ShotCallerAutoOpen)
                        {
                            IsOpen = true;
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

    private void RefreshPartyElementStatuses()
    {
        var now = DateTime.UtcNow;

        var activeElementKeys =
            new HashSet<string>(StringComparer.Ordinal);

        partyDebugLines.Clear();
        visiblePartyMembers = 0;

        // Use Length + indexer directly instead of foreach. This is the most
        // literal path exposed by IPartyList and avoids depending on EntityId.
        var partyLength = Plugin.PartyList.Length;

        for (var i = 0; i < partyLength; i++)
        {
            var member = Plugin.PartyList[i];

            if (member == null)
            {
                partyDebugLines.Add(
                    $"[{i}] <null party member>");
                continue;
            }

            visiblePartyMembers++;

            var playerName =
                member.Name.TextValue;

            // ContentId is preferred for the key, then EntityId, then party slot.
            var playerKey =
                member.ContentId != 0
                    ? $"CID:{member.ContentId}"
                    : member.EntityId != 0
                        ? $"EID:{member.EntityId}"
                        : $"SLOT:{i}:{playerName}";

            var statusSummary =
                new List<string>();

            // Read the party member StatusList directly by index.
            // We intentionally do not require EntityId or ObjectTable lookup.
            var statuses = member.Statuses;

            for (var s = 0; s < statuses.Length; s++)
            {
                var status = statuses[s];

                if (status == null ||
                    status.StatusId == 0)
                {
                    continue;
                }

                statusSummary.Add(
                    $"{status.StatusId}({status.RemainingTime:0.0}s)");

                if (status.StatusId is not
                    (WaterStatusId or LightningStatusId))
                {
                    continue;
                }

                CaptureElementStatus(
                    playerKey,
                    playerName,
                    status.StatusId,
                    status.RemainingTime,
                    now,
                    activeElementKeys);
            }

            partyDebugLines.Add(
                $"[{i}] {playerName} | CID {member.ContentId} | " +
                $"EID {member.EntityId:X8} | StatusSlots {statuses.Length} | " +
                (statusSummary.Count > 0
                    ? string.Join(", ", statusSummary)
                    : "no statuses"));
        }

        // Keep local-player direct scan as a proven fallback.
        if (Plugin.ObjectTable.LocalPlayer is IBattleChara localBattleChara)
        {
            var localName =
                localBattleChara.Name.TextValue;

            var localKey =
                Plugin.PlayerState.ContentId != 0
                    ? $"CID:{Plugin.PlayerState.ContentId}"
                    : $"LOCAL:{localName}";

            foreach (var status in localBattleChara.StatusList)
            {
                if (status.StatusId is not
                    (WaterStatusId or LightningStatusId))
                {
                    continue;
                }

                CaptureElementStatus(
                    localKey,
                    localName,
                    status.StatusId,
                    status.RemainingTime,
                    now,
                    activeElementKeys);
            }
        }

        activeElementKeysLastFrame.Clear();

        foreach (var key in activeElementKeys)
        {
            activeElementKeysLastFrame.Add(key);
        }
    }

    private void CaptureElementStatus(
        string playerKey,
        string playerName,
        uint statusId,
        float remainingTime,
        DateTime now,
        HashSet<string> activeElementKeys)
    {
        var element =
            statusId == WaterStatusId
                ? Element.Water
                : Element.Lightning;

        var activeKey =
            $"{playerKey}:{statusId}";

        activeElementKeys.Add(activeKey);

        if (activeElementKeysLastFrame.Contains(activeKey))
        {
            return;
        }

        var duration =
            remainingTime <
            ShortLongThresholdSeconds
                ? Duration.Short
                : Duration.Long;

        playerAssignments[playerKey] =
            new PlayerElementAssignment
            {
                Key = playerKey,
                Name = playerName,
                Element = element,
                Duration = duration
            };

        if (HasFreshNeoTell(now))
        {
            ApplyNeoDuration(
                latestNeoTellIndex,
                duration);
        }

        lastEvent =
            $"{playerName}: {duration} {element} captured.";
    }

    private bool HasFreshNeoTell(
        DateTime now)
    {
        return
            latestNeoTellIndex is 1 or 2 &&
            now - latestNeoTellAtUtc <=
            TellFreshness;
    }

    private void ApplyNeoTruth(
        int index,
        Truth truth)
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

    private void ApplyNeoDuration(
        int index,
        Duration duration)
    {
        if (duration == Duration.Unknown)
        {
            return;
        }

        if (index == 1)
        {
            neo1Duration = duration;

            neo2Duration =
                duration == Duration.Short
                    ? Duration.Long
                    : Duration.Short;
        }
        else if (index == 2)
        {
            neo2Duration = duration;

            neo1Duration =
                duration == Duration.Short
                    ? Duration.Long
                    : Duration.Short;
        }
    }

    private Truth GetNeoTruthForDuration(
        Duration duration)
    {
        if (neo1Duration == duration)
        {
            return neo1Truth;
        }

        if (neo2Duration == duration)
        {
            return neo2Truth;
        }

        return Truth.Unknown;
    }

    private static Truth TruthFromTellParam(
        ushort param)
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

    private static string TruthText(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real => "REAL",
            Truth.Fake => "FAKE",
            _ => "?"
        };
    }

    private static string DurationText(
        Duration duration)
    {
        return duration switch
        {
            Duration.Short => "SHORT",
            Duration.Long => "LONG",
            _ => "?"
        };
    }
}
