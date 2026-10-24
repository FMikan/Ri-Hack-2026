using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.Infrastructure;

// Tier.Bronze <-> 'bronze', ConceptState.InProgress <-> 'in_progress'. Same spelling as the HTTP JSON enum converter.
public class SnakeCaseEnumConverter<T>() : ValueConverter<T, string>(v => ToDb(v), s => FromDb(s)) where T : struct, Enum
{
    public static string ToDb(T value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    public static T FromDb(string value) => Enum.Parse<T>(value.Replace("_", ""), ignoreCase: true);

    // 'bronze', 'silver', ... for CHECK constraints.
    public static string SqlList() => string.Join(", ", Enum.GetValues<T>().Select(v => $"'{ToDb(v)}'"));
}
