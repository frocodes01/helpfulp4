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

    private enum AccelOverride
    {
        Auto,
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

    // Neo #1
    private Truth neo1Truth = Truth.Unknown;
    private Duration neo1Duration = Duration.Unknown;
    private AccelOverride neo1AccelOverride = AccelOverride.Auto;

    // Chaos #1
    private Truth chaos1Truth = Truth.Unknown;
    private ChaosElement chaos1Element = ChaosElement.Unknown;

    // Neo #2
    private Truth neo2Truth = Truth.Unknown;
    private Duration neo2Duration = Duration.Unknown;
    private AccelOverride neo2AccelOverride = AccelOverride.Auto;

    // Chaos #2
    private Truth chaos2Truth = Truth.Unknown;
    private ChaosElement chaos2Element = ChaosElement.Unknown;

    // Mana
    private Truth manaChargeLightning = Truth.Unknown;
    private Truth manaChargeIce = Truth.Unknown;

    private Truth manaReleaseLightning = Truth.Unknown;
    private Truth manaReleaseIce = Truth.Unknown;

    // Colours
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

        var availableWidth = ImGui.GetContentRegionAvail().X;

        var useTwoColumns =
            availableWidth >= 720 * ImGuiHelpers.GlobalScale;

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

        ImGui.SameLine();

        var resetWidth =
            80 * ImGuiHelpers.GlobalScale;

        var available =
            ImGui.GetContentRegionAvail().X;

        if (available > resetWidth)
        {
            ImGui.SetCursorPosX(
                ImGui.GetCursorPosX()
                + available
                - resetWidth);
        }

        if (ImGui.Button(
            "Reset",
            new Vector2(resetWidth, 0)))
        {
            ResetAll();
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
            ref neo1AccelOverride,
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
            ref neo2AccelOverride,
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
        ref AccelOverride accelOverride,
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
            ref accelOverride,
            duration);
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
    // BUTTONS
    // ============================================================

    private void DrawTruthButtons(
        string id,
        ref Truth value)
    {
        var realSelected =
            value == Truth.Real;

        var fakeSelected =
            value == Truth.Fake;

        if (DrawRealButton(
            id,
            realSelected))
        {
            value = Truth.Real;
        }

        ImGui.SameLine();

        if (DrawFakeButton(
            id,
            fakeSelected))
        {
            value = Truth.Fake;
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

    private void DrawDurationButtons(
        string title,
        ref Duration duration,
        bool isNeo1)
    {
        if (DrawChoiceButton(
            $"Short##{title}_Short",
            duration == Duration.Short))
        {
            duration = Duration.Short;

            if (isNeo1)
            {
                neo2Duration = Duration.Long;
            }
            else
            {
                neo1Duration = Duration.Long;
            }
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Long##{title}_Long",
            duration == Duration.Long))
        {
            duration = Duration.Long;

            if (isNeo1)
            {
                neo2Duration = Duration.Short;
            }
            else
            {
                neo1Duration = Duration.Short;
            }
        }
    }

    private void DrawAccelButtons(
        string title,
        ref AccelOverride accelOverride,
        Duration automaticDuration)
    {
        var autoLabel =
            automaticDuration switch
            {
                Duration.Short => "Auto: Short",
                Duration.Long => "Auto: Long",
                _ => "Auto"
            };

        if (DrawChoiceButton(
            $"{autoLabel}##{title}_Auto",
            accelOverride == AccelOverride.Auto))
        {
            accelOverride =
                AccelOverride.Auto;
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Short##{title}_AccelShort",
            accelOverride == AccelOverride.Short))
        {
            accelOverride =
                AccelOverride.Short;
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Long##{title}_AccelLong",
            accelOverride == AccelOverride.Long))
        {
            accelOverride =
                AccelOverride.Long;
        }

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"None##{title}_AccelNone",
            accelOverride == AccelOverride.None))
        {
            accelOverride =
                AccelOverride.None;
        }
    }

    private void DrawChaosElementButtons(
        string title,
        ref ChaosElement element,
        bool isChaos1)
    {
        if (DrawChoiceButton(
            $"Fire##{title}_Fire",
            element == ChaosElement.Fire))
        {
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

        ImGui.SameLine();

        if (DrawChoiceButton(
            $"Water##{title}_Water",
            element == ChaosElement.Water))
        {
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
    // PLAYBACK SIDE
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
                    GetAccelCallout(
                        neo1Truth,
                        GetEffectiveAccelDuration(1)));

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
                    GetAccelCallout(
                        neo2Truth,
                        GetEffectiveAccelDuration(2)));
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
    // CALCULATOR
    // ============================================================

    private string GetSpreadCallout(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real => "LIGHTNING",
            Truth.Fake => "WATER",
            _ => "?"
        };
    }

    private string GetGazeCallout(
        Truth truth)
    {
        return truth switch
        {
            Truth.Real => "LOOK AWAY",
            Truth.Fake => "LOOK IN",
            _ => "?"
        };
    }

    private string GetInfernoCallout()
    {
        var truth =
            GetChaosTruthForElement(
                ChaosElement.Fire);

        return truth switch
        {
            Truth.Real => "SPREAD",
            Truth.Fake => "STAY",
            _ => "?"
        };
    }

    private string GetTsunamiCallout()
    {
        var truth =
            GetChaosTruthForElement(
                ChaosElement.Water);

        return truth switch
        {
            Truth.Real => "STAY",
            Truth.Fake => "SPREAD",
            _ => "?"
        };
    }

    private string GetFinalTsunamiCallout()
    {
        var tsunamiCallout =
            GetTsunamiCallout();

        return tsunamiCallout switch
        {
            "STAY" => "DONUT",
            "SPREAD" => "CHARIOT",
            _ => "?"
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

    private Duration GetEffectiveAccelDuration(
        int neoNumber)
    {
        var duration =
            neoNumber == 1
                ? neo1Duration
                : neo2Duration;

        var accelOverride =
            neoNumber == 1
                ? neo1AccelOverride
                : neo2AccelOverride;

        return accelOverride switch
        {
            AccelOverride.Short =>
                Duration.Short,

            AccelOverride.Long =>
                Duration.Long,

            AccelOverride.None =>
                Duration.Unknown,

            _ =>
                duration
        };
    }

    private string GetAccelCallout(
        Truth truth,
        Duration duration)
    {
        if (truth == Truth.Unknown ||
            duration == Duration.Unknown)
        {
            return "?";
        }

        var timing =
            duration == Duration.Short
                ? "1st"
                : "2nd";

        var mechanic =
            truth == Truth.Real
                ? "STILLNESS"
                : "MOTION";

        return $"{timing} {mechanic}";
    }

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
            Truth.Real => "REAL",
            Truth.Fake => "FAKE",
            _ => "?"
        };
    }

    // ============================================================
    // RESET
    // ============================================================

    private void ResetAll()
    {
        neo1Truth =
            Truth.Unknown;

        neo1Duration =
            Duration.Unknown;

        neo1AccelOverride =
            AccelOverride.Auto;

        chaos1Truth =
            Truth.Unknown;

        chaos1Element =
            ChaosElement.Unknown;

        neo2Truth =
            Truth.Unknown;

        neo2Duration =
            Duration.Unknown;

        neo2AccelOverride =
            AccelOverride.Auto;

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
    }
}