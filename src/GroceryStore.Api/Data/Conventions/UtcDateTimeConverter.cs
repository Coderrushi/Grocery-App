using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GroceryStore.Api.Data.Conventions;

/// <summary>
/// MySQL DATETIME has no time zone, so values come back with <see cref="DateTimeKind.Unspecified"/>.
/// All timestamps in this app are stored in UTC; this converter marks them as UTC when read so they
/// serialise to JSON with a trailing "Z" and the browser converts them to local (IST) time correctly.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDb => toDb.Kind == DateTimeKind.Local ? toDb.ToUniversalTime() : toDb,
    fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));
