using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class RelayRangeTests
{
    [TestMethod]
    public void BuildHeader_UsesInclusiveEnd()
    {
        Assert.AreEqual("bytes=100-355", RelayRange.BuildHeader(100, 256));
    }

    [TestMethod]
    public void BuildHeader_RejectsInvalidValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RelayRange.BuildHeader(-1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RelayRange.BuildHeader(0, 0));
        Assert.ThrowsExactly<OverflowException>(() => RelayRange.BuildHeader(long.MaxValue, 2));
    }
}
