// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.Json;

namespace AStudio.App.Services;

/// <summary>Device-local wellness prefs — peer to frontend wellnessPrefs.ts.</summary>
public sealed class WellnessPrefs
{
    public bool HydrationEnabled { get; set; } = true;
    public int HydrationMin { get; set; } = 15;
    public string Pattern { get; set; } = "relax";
    public bool StretchEnabled { get; set; } = true;
    public int StretchMin { get; set; } = 45;
    public bool EyeExerciseEnabled { get; set; } = true;
    public int EyeExerciseMin { get; set; } = 30;

    static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AStudio",
            "wellness-prefs.json");

    public static WellnessPrefs Load()
    {
        try
        {
            var path = Path;
            if (!File.Exists(path)) return new WellnessPrefs();
            var p = JsonSerializer.Deserialize<WellnessPrefs>(File.ReadAllText(path));
            return p ?? new WellnessPrefs();
        }
        catch
        {
            return new WellnessPrefs();
        }
    }

    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            /* best-effort */
        }
    }

    public void Clamp()
    {
        HydrationMin = Math.Clamp(HydrationMin, 1, 240);
        StretchMin = Math.Clamp(StretchMin, 1, 240);
        EyeExerciseMin = Math.Clamp(EyeExerciseMin, 1, 240);
        if (string.IsNullOrWhiteSpace(Pattern)) Pattern = "relax";
    }
}

public sealed record WellnessReminder(string Kind, string Title, string Subtitle);

/// <summary>Interval reminders — hydration toast · stretch/eye banners (web useWellnessReminders peer).</summary>
public sealed class WellnessReminderClock
{
    readonly Func<WellnessPrefs> _prefs;
    DateTimeOffset _lastHydration = DateTimeOffset.MinValue;
    DateTimeOffset _lastStretch = DateTimeOffset.MinValue;
    DateTimeOffset _lastEyes = DateTimeOffset.MinValue;

    public WellnessReminderClock(Func<WellnessPrefs> prefs) => _prefs = prefs;

    public event Action<WellnessReminder>? Reminder;

    public void Tick()
    {
        var p = _prefs();
        var now = DateTimeOffset.UtcNow;
        if (p.HydrationEnabled && Elapsed(_lastHydration, p.HydrationMin, now))
        {
            _lastHydration = now;
            Reminder?.Invoke(new WellnessReminder("hydrate", "Time to hydrate", "Take a sip of water."));
        }
        if (p.StretchEnabled && Elapsed(_lastStretch, p.StretchMin, now))
        {
            _lastStretch = now;
            Reminder?.Invoke(new WellnessReminder(
                "stretch", "Stretch break", "Neck, shoulders, wrists — two minutes at your desk."));
        }
        if (p.EyeExerciseEnabled && Elapsed(_lastEyes, p.EyeExerciseMin, now))
        {
            _lastEyes = now;
            Reminder?.Invoke(new WellnessReminder(
                "eyes", "Eye break", "Look away from the screen — rest your eyes."));
        }
    }

    static bool Elapsed(DateTimeOffset last, int minutes, DateTimeOffset now)
    {
        if (last == DateTimeOffset.MinValue)
        {
            // First fire after one full interval from app start (avoid instant splash).
            return false;
        }
        return (now - last).TotalMinutes >= Math.Max(1, minutes);
    }

    /// <summary>Arm timers so first reminder waits a full interval from now.</summary>
    public void ArmFromNow()
    {
        var now = DateTimeOffset.UtcNow;
        _lastHydration = now;
        _lastStretch = now;
        _lastEyes = now;
    }
}
