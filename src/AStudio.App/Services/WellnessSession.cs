// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace AStudio.App.Services;

/// <summary>Desktop peer to esti wellnessExercises + @esti/contracts breathing patterns.</summary>
public enum WellnessSection
{
    Breathe,
    Stretch,
    Eyes,
}

public sealed record BreathingPattern(
    string Key,
    string Name,
    string ShortLabel,
    string Goal,
    double Inhale,
    double Hold,
    double Exhale,
    double HoldOut,
    string DurationLabel,
    int SessionSeconds)
{
    public double CycleSeconds => Inhale + Hold + Exhale + HoldOut;
}

public sealed record WellnessStep(string Key, string Name, int DurationSec, string Cue);

public static class WellnessCatalog
{
    public static readonly IReadOnlyList<BreathingPattern> Patterns =
    [
        new("relax", "Relaxation & stress reduction", "Relax", "Settle the body and lower stress", 4, 2, 6, 0, "5–10 min", 5 * 60),
        new("focus", "Better focus", "Focus", "Steady the mind before deep work", 4, 4, 4, 0, "2–5 min", 3 * 60),
        new("anxiety", "Anxiety relief", "Calm", "Longer exhale to calm a racing mind", 4, 0, 6, 0, "3–10 min", 4 * 60),
        new("daily", "General daily breathing", "Daily", "Resonant, coherent breathing — anytime", 5.5, 0, 5.5, 0, "5–20 min", 5 * 60),
    ];

    public static readonly IReadOnlyList<WellnessStep> StretchRoutine =
    [
        new("neck", "Neck roll", 20, "Slow circles — five each way"),
        new("shoulders", "Shoulder rolls", 20, "Roll shoulders back, then forward"),
        new("wrists", "Wrist release", 15, "Extend one arm, flex the wrist gently"),
        new("stand", "Stand & reach", 25, "Stand, reach arms overhead, breathe"),
    ];

    public static readonly IReadOnlyList<WellnessStep> EyeRoutine =
    [
        new("far", "20-20-20", 20, "Look at something far away — relax focus"),
        new("blink", "Slow blinks", 15, "Close gently for two seconds, then open"),
        new("figure8", "Figure eight", 20, "Trace a lazy ∞ with your eyes only"),
        new("palm", "Palming", 20, "Cup warm palms over closed eyes"),
    ];

    public static BreathingPattern Pattern(string key) =>
        Patterns.FirstOrDefault(p => p.Key == key) ?? Patterns[0];

    public static IReadOnlyList<WellnessStep> Routine(WellnessSection section) =>
        section == WellnessSection.Eyes ? EyeRoutine : StretchRoutine;

    public static int RoutineTotalSec(IReadOnlyList<WellnessStep> steps) =>
        steps.Sum(s => s.DurationSec);
}

/// <summary>Tickable session state for breath / stretch / eyes guides.</summary>
public sealed class WellnessSession
{
    public WellnessSection Section { get; private set; } = WellnessSection.Breathe;
    public BreathingPattern Pattern { get; private set; } = WellnessCatalog.Patterns[0];
    public bool Running { get; private set; }
    public string PhaseLabel { get; private set; } = "Ready";
    public int PhaseLeftSec { get; private set; }
    public int SessionLeftSec { get; private set; }
    public double OrbScale { get; private set; } = 0.55;
    public WellnessStep? Step { get; private set; }
    public int StepIndex { get; private set; }
    public int StepCount =>
        Section == WellnessSection.Breathe ? 0 : WellnessCatalog.Routine(Section).Count;

    /// <summary>Stretch body / eye lid transform (degrees · px · scale).</summary>
    public double GlyphRotate { get; private set; }
    public double GlyphTranslateY { get; private set; }
    public double GlyphScaleX { get; private set; } = 1;
    public double GlyphScaleY { get; private set; } = 1;
    public double GlyphOpacity { get; private set; } = 1;
    public double IrisX { get; private set; }
    public double IrisY { get; private set; }
    public double IrisScale { get; private set; } = 1;

    DateTimeOffset _started;
    bool _completed;

    public event Action? Completed;

    public string StepProgressLabel =>
        Section == WellnessSection.Breathe || Step is null
            ? ""
            : $"Step {StepIndex + 1}/{StepCount}";

    public void SetSection(WellnessSection section)
    {
        Section = section;
        Stop();
        ResetIdle();
    }

    public void SetPattern(string key)
    {
        Pattern = WellnessCatalog.Pattern(key);
        Stop();
        ResetIdle();
    }

    public void Toggle()
    {
        if (Running) Stop();
        else Start();
    }

    public void Start()
    {
        _completed = false;
        Running = true;
        _started = DateTimeOffset.UtcNow;
        ResetIdle();
    }

    public void Stop()
    {
        Running = false;
        ResetIdle();
    }

    void ResetIdle()
    {
        PhaseLabel = "Ready";
        PhaseLeftSec = 0;
        OrbScale = 0.55;
        StepIndex = 0;
        ResetGlyphTransforms();
        if (Section == WellnessSection.Breathe)
        {
            SessionLeftSec = Pattern.SessionSeconds;
            Step = null;
        }
        else
        {
            var routine = WellnessCatalog.Routine(Section);
            Step = routine[0];
            SessionLeftSec = WellnessCatalog.RoutineTotalSec(routine);
            PhaseLeftSec = Step.DurationSec;
            PhaseLabel = Step.Name;
        }
    }

    void ResetGlyphTransforms()
    {
        GlyphRotate = 0;
        GlyphTranslateY = 0;
        GlyphScaleX = 1;
        GlyphScaleY = 1;
        GlyphOpacity = 1;
        IrisX = 0;
        IrisY = 0;
        IrisScale = 1;
    }

    public void Tick()
    {
        if (!Running || _completed) return;
        var elapsed = (DateTimeOffset.UtcNow - _started).TotalSeconds;
        if (Section == WellnessSection.Breathe)
            TickBreath(elapsed);
        else
            TickRoutine(elapsed);
    }

    void TickBreath(double elapsed)
    {
        var remain = Pattern.SessionSeconds - elapsed;
        if (remain <= 0)
        {
            Finish();
            return;
        }
        SessionLeftSec = (int)Math.Ceiling(remain);
        var cyc = Pattern.CycleSeconds;
        var pos = elapsed % cyc;
        var inhale = Pattern.Inhale;
        var hold = Pattern.Hold;
        var exhale = Pattern.Exhale;
        if (pos < inhale)
        {
            PhaseLabel = "Breathe in";
            PhaseLeftSec = (int)Math.Ceiling(inhale - pos);
            OrbScale = 0.55 + 0.45 * Ease(pos / inhale);
        }
        else if (pos < inhale + hold)
        {
            PhaseLabel = "Hold";
            PhaseLeftSec = (int)Math.Ceiling(inhale + hold - pos);
            OrbScale = 1;
        }
        else if (pos < inhale + hold + exhale)
        {
            var p = (pos - inhale - hold) / Math.Max(0.01, exhale);
            PhaseLabel = "Breathe out";
            PhaseLeftSec = (int)Math.Ceiling(inhale + hold + exhale - pos);
            OrbScale = 1 - 0.45 * Ease(p);
        }
        else
        {
            PhaseLabel = "Hold";
            PhaseLeftSec = (int)Math.Ceiling(cyc - pos);
            OrbScale = 0.55;
        }
    }

    void TickRoutine(double elapsed)
    {
        var routine = WellnessCatalog.Routine(Section);
        var total = WellnessCatalog.RoutineTotalSec(routine);
        if (elapsed >= total)
        {
            Finish();
            return;
        }
        SessionLeftSec = (int)Math.Ceiling(total - elapsed);
        var acc = 0.0;
        for (var i = 0; i < routine.Count; i++)
        {
            var dur = routine[i].DurationSec;
            if (elapsed < acc + dur)
            {
                StepIndex = i;
                Step = routine[i];
                PhaseLabel = Step.Name;
                PhaseLeftSec = (int)Math.Ceiling(acc + dur - elapsed);
                OrbScale = 1;
                AnimateGlyph(Step.Key, elapsed - acc, dur);
                return;
            }
            acc += dur;
        }
        Finish();
    }

    /// <summary>Peer to glass.scss esti-stretch-* / esti-eye-* keyframes.</summary>
    void AnimateGlyph(string key, double localT, double dur)
    {
        ResetGlyphTransforms();
        // Loop phase within step (web infinite animations).
        var loop = Section == WellnessSection.Eyes
            ? key switch
            {
                "far" => 3.0,
                "blink" => 2.5,
                "figure8" => 4.0,
                _ => 3.0,
            }
            : key switch
            {
                "neck" => 4.0,
                "shoulders" => 3.0,
                "wrists" => 2.5,
                _ => 3.5,
            };
        var p = (localT % loop) / loop; // 0..1 in loop
        var wave = Math.Sin(p * Math.PI * 2);

        if (Section == WellnessSection.Stretch)
        {
            switch (key)
            {
                case "neck":
                    GlyphRotate = wave * 8;
                    break;
                case "shoulders":
                    GlyphTranslateY = -6 * Math.Sin(p * Math.PI);
                    break;
                case "wrists":
                    GlyphRotate = wave * -4;
                    GlyphScaleX = 1 + 0.04 * Math.Abs(wave);
                    break;
                case "stand":
                    GlyphScaleY = 1 + 0.08 * Math.Max(0, Math.Sin(p * Math.PI));
                    break;
            }
            return;
        }

        // Eyes
        switch (key)
        {
            case "far":
                IrisScale = 0.85 + 0.2 * (0.5 + 0.5 * Math.Sin(p * Math.PI * 2));
                break;
            case "blink":
                // Lid squeeze mid-cycle
                GlyphScaleY = p is > 0.45 and < 0.55 ? 0.08 : 1;
                break;
            case "figure8":
                {
                    var a = p * Math.PI * 2;
                    IrisX = Math.Sin(a) * 18;
                    IrisY = Math.Sin(a * 2) * 10;
                }
                break;
            case "palm":
                GlyphOpacity = 0.35 + 0.35 * (0.5 + 0.5 * Math.Sin(p * Math.PI * 2));
                IrisScale = 0.9;
                break;
        }
    }

    void Finish()
    {
        if (_completed) return;
        _completed = true;
        Running = false;
        PhaseLabel = "Complete";
        PhaseLeftSec = 0;
        SessionLeftSec = 0;
        OrbScale = 0.55;
        Completed?.Invoke();
    }

    static double Ease(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return (1 - Math.Cos(p * Math.PI)) / 2;
    }

    public static string FormatMmSs(int sec)
    {
        sec = Math.Max(0, sec);
        return $"{sec / 60:00}:{sec % 60:00}";
    }
}
