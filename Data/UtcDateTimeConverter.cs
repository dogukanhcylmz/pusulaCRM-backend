using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DigitestCrm.Api.Data;


public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }

    internal static DateTime ToUtc(DateTime v) =>
        v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc);
}

public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        v => v.HasValue ? UtcDateTimeConverter.ToUtc(v.Value) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
}
