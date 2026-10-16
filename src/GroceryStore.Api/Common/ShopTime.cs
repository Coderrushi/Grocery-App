namespace GroceryStore.Api.Common;

/// <summary>
/// The shop's calendar. Timestamps are stored in UTC, but "today" and "a day's sales" mean Indian days:
/// an order at 01:00 IST on the 5th is stored as 19:30 UTC on the 4th and must count on the 5th.
/// India has a single time zone and no daylight saving, so IST is always UTC+05:30. That keeps day
/// boundaries a fixed offset, which MySQL can apply inside a GROUP BY (ADR 0001, quirk 7).
/// </summary>
public static class ShopTime
{
    /// <summary>Minutes ahead of UTC. A constant so LINQ queries translate it to SQL as a literal.</summary>
    public const int OffsetMinutes = 330;

    public static readonly TimeSpan Offset = TimeSpan.FromMinutes(OffsetMinutes);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime + Offset);

    /// <summary>The UTC instant at which <paramref name="day"/> begins in IST.</summary>
    public static DateTime StartOfDayUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);

    public static DateTime ToShopTime(DateTime utc) => utc + Offset;
}
