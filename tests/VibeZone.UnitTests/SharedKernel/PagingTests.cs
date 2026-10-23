using VibeZone.Infrastructure.Web;
using VibeZone.SharedKernel;

namespace VibeZone.UnitTests.SharedKernel;

public sealed class PagingTests
{
    [Theory]
    [InlineData(null, null, 1, 20, 0)]
    [InlineData(3, 10, 3, 10, 20)]
    [InlineData(0, 0, 1, 1, 0)]
    [InlineData(-5, 500, 1, 50, 0)]
    public void Page_request_is_clamped(int? page, int? pageSize, int expectedPage, int expectedSize, int expectedSkip)
    {
        var request = new PageRequest(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedSize, request.PageSize);
        Assert.Equal(expectedSkip, request.Skip);
    }

    [Theory]
    [InlineData(1, 20, 0, false)]
    [InlineData(1, 20, 20, false)]
    [InlineData(1, 20, 21, true)]
    [InlineData(2, 20, 41, true)]
    [InlineData(3, 20, 41, false)]
    public void Has_more_when_items_remain_after_this_page(int page, int pageSize, int total, bool hasMore) =>
        Assert.Equal(hasMore, ListMeta.For(page, pageSize, total).HasMore);
}
