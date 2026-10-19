using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VibeZone.Infrastructure.Persistence;

/// <summary>Stores enums as lower-case text (readable, CHECK-constrained, migration-friendly; data-model.md).</summary>
public sealed class LowerCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    v => v.ToString().ToLowerInvariant(),
    v => Enum.Parse<TEnum>(v, true))
    where TEnum : struct, Enum;

public static class EnumCheck
{
    /// <summary>SQL value list for a CHECK constraint matching <see cref="LowerCaseEnumConverter{TEnum}"/>, e.g. <c>'draft', 'ready'</c>.</summary>
    public static string Values<TEnum>() where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n.ToLowerInvariant()}'"));
}
