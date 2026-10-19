using System.Globalization;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Domain;

public enum RangeOutcome
{
    /// <summary>No (usable) Range header: send the whole representation with 200.</summary>
    Full,

    /// <summary>One satisfiable range: send 206 with Content-Range.</summary>
    Partial,

    /// <summary>Syntactically valid but unsatisfiable: send 416 with <c>Content-Range: bytes */size</c>.</summary>
    Unsatisfiable,
}

public readonly record struct RangeResult(RangeOutcome Outcome, ByteRange Range);

/// <summary>
/// RFC 9110 §14 byte-range parsing for a single range. Multi-range and malformed headers are ignored
/// (the server MAY ignore Range and answer 200 with the full body).
/// </summary>
public static class RangeRequest
{
    public static RangeResult Parse(string? header, long size)
    {
        var full = new RangeResult(RangeOutcome.Full, new ByteRange(0, Math.Max(size - 1, 0)));
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        var spec = header["bytes=".Length..].Trim();
        if (spec.Contains(',', StringComparison.Ordinal))
        {
            return full;
        }

        var dash = spec.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            return full;
        }

        var first = spec[..dash].Trim();
        var last = spec[(dash + 1)..].Trim();

        if (first.Length == 0)
        {
            // Suffix range: the last N bytes.
            if (!TryParse(last, out var suffix))
            {
                return full;
            }

            if (suffix == 0 || size == 0)
            {
                return Unsatisfiable(size);
            }

            var from = Math.Max(size - suffix, 0);
            return new RangeResult(RangeOutcome.Partial, new ByteRange(from, size - 1));
        }

        if (!TryParse(first, out var start))
        {
            return full;
        }

        long end;
        if (last.Length == 0)
        {
            end = size - 1;
        }
        else if (!TryParse(last, out end) || end < start)
        {
            return full;
        }

        if (start >= size)
        {
            return Unsatisfiable(size);
        }

        return new RangeResult(RangeOutcome.Partial, new ByteRange(start, Math.Min(end, size - 1)));
    }

    private static RangeResult Unsatisfiable(long size) => new(RangeOutcome.Unsatisfiable, new ByteRange(0, Math.Max(size - 1, 0)));

    private static bool TryParse(string value, out long result) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
}
