using VibeZone.SharedKernel;

namespace VibeZone.UnitTests.SharedKernel;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_exposes_error_and_hides_value()
    {
        Result<int> result = DomainError.NotFound("track.not_found", "Track not found.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void EntityId_is_version_7_with_current_timestamp()
    {
        var id = EntityId.New();

        Assert.Equal(7, id.Version);
        // The first 48 bits of a UUIDv7 are the Unix timestamp in milliseconds.
        var unixMs = Convert.ToInt64(id.ToString("N")[..12], 16);
        var delta = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        Assert.InRange(delta, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
}
