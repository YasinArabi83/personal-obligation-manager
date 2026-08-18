namespace POM.Users;

/// <summary>
/// The calendar a user prefers for display/input in the UI. Storage is always UTC/Gregorian
/// (docs/AGENTS.md §2) — this preference only affects presentation-layer Jalali↔Gregorian
/// conversion. Persisted as text ('Jalali' | 'Gregorian') via EF enum-to-string conversion.
/// </summary>
public enum CalendarPreference
{
    Jalali = 0,
    Gregorian = 1,
}
