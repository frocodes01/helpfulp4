using System;
using System.Numerics;
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
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(390, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
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
        DrawOutputCard(
            "FirstNeoResults",
            () =>
            {
                DrawResultLine(
                    "1st spread is",
                    GetSpreadCallout(
                        neo1Truth));

                DrawResultLine(
                    "Your accel is",
                    GetAccelCalloutForTiming(
                        AccelTiming.Short));

                DrawResultLine(
                    "",
                    $"{GetGazeCallout(neo1Truth)} from 1st shrieks");
            },
            3);

        DrawManaInputBreak(
            "Mana Charge: Lightning",
            "ManaChargeLightning",
            ref manaChargeLightning);

        DrawOutputCard(
            "MiddleResults",
            () =>
            {
                DrawResultLine(
                    "Inferno",
                    GetInfernoCallout());

                DrawResultLine(
                    "2nd spread is",
                    GetSpreadCallout(
                        neo2Truth));

                DrawResultLine(
                    "Your accel is",
                    GetAccelCalloutForTiming(
                        AccelTiming.Long));
            },
            3);

        DrawManaInputBreak(
            "Mana Charge: Blizzard",
            "ManaChargeIce",
            ref manaChargeIce);

        DrawOutputCard(
            "SecondNeoResults",
            () =>
            {
                DrawResultLine(
                    "",
                    $"{GetGazeCallout(neo2Truth)} from 2nd shrieks");

                DrawResultLine(
                    "Tsunami",
                    GetTsunamiCallout());
            },
            2);

        DrawManaInputBreak(
            "Mana Release: Lightning",
            "ManaReleaseLightning",
            ref manaReleaseLightning);

        DrawManaInputBreak(
            "Mana Release: Blizzard",
            "ManaReleaseIce",
            ref manaReleaseIce);

        var lightningResult =
            ResolveMana(
                manaChargeLightning,
                manaReleaseLightning);

        var blizzardResult =
            ResolveMana(
                manaChargeIce,
                manaReleaseIce);

        DrawOutputCard(
            "FinalManaResults",
            () =>
            {
                DrawResultLine(
                    "Lightning",
                    TruthToString(
                        lightningResult));

                DrawResultLine(
                    "Blizzard",
                    TruthToString(
                        blizzardResult));

                ImGui.Spacing();

                var manaCallout =
                    GetFinalManaCallout(
                        lightningResult,
                        blizzardResult);

                var tsunamiCallout =
                    GetFinalTsunamiCallout();

                ImGui.Text(
                    $"Final Mana: {manaCallout} + {tsunamiCallout}");
            },
            4);
    }

    // ============================================================
    // OUTPUT CARDS
    // ============================================================

    private void DrawOutputCard(
        string id,
        Action contents,
        int lineCount)
    {
        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            CardBackground);

        ImGui.PushStyleColor(
            ImGuiCol.Border,
            CardBorder);

        var lineHeight =
            ImGui.GetTextLineHeightWithSpacing();

        var cardHeight =
            (lineHeight * lineCount)
            + (18 * ImGuiHelpers.GlobalScale);

        ImGui.BeginChild(
            id,
            new Vector2(
                0,
                cardHeight),
            true);

        contents();

        ImGui.EndChild();

        ImGui.PopStyleColor(2);

        ImGui.Spacing();
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