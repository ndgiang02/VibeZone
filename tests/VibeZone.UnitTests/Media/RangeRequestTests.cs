using VibeZone.Modules.Media.Domain;

namespace VibeZone.UnitTests.Media;

public sealed class RangeRequestTests
{
    [Theory]
    [InlineData(null, RangeOutcome.Full, 0, 999)]
    [InlineData("bytes=0-", RangeOutcome.Partial, 0, 999)]
    [InlineData("bytes=100-199", RangeOutcome.Partial, 100, 199)]
    [InlineData("bytes=-300", RangeOutcome.Partial, 700, 999)]
    [InlineData("bytes=-5000", RangeOutcome.Partial, 0, 999)]
    [InlineData("bytes=990-5000", RangeOutcome.Partial, 990, 999)]
    [InlineData("bytes=1000-", RangeOutcome.Unsatisfiable, 0, 999)]
    [InlineData("bytes=-0", RangeOutcome.Unsatisfiable, 0, 999)]
    [InlineData("bytes=0-10,20-30", RangeOutcome.Full, 0, 999)]
    [InlineData("bytes=50-10", RangeOutcome.Full, 0, 999)]
    [InlineData("items=0-10", RangeOutcome.Full, 0, 999)]
    public void Parses_single_ranges_per_rfc_9110(string? header, RangeOutcome outcome, long from, long to)
    {
        var result = RangeRequest.Parse(header, 1000);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(from, result.Range.From);
        Assert.Equal(to, result.Range.To);
    }
}
