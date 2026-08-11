using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class ResumePositionPolicyTests
{
    [TestMethod]
    public void Normalize_DropsPositionsBelowTenSeconds()
    {
        Assert.AreEqual(0, ResumePositionPolicy.Normalize(TimeSpan.FromSeconds(9).Ticks, null, 5));
        Assert.AreEqual(TimeSpan.FromSeconds(10).Ticks, ResumePositionPolicy.Normalize(TimeSpan.FromSeconds(10).Ticks, null, 5));
    }

    [TestMethod]
    public void Normalize_DropsPositionsNearMediaEnd()
    {
        var runtime = TimeSpan.FromMinutes(60).Ticks;

        Assert.AreEqual(0, ResumePositionPolicy.Normalize(TimeSpan.FromMinutes(56).Ticks, runtime, 5));
        Assert.AreEqual(TimeSpan.FromMinutes(54).Ticks, ResumePositionPolicy.Normalize(TimeSpan.FromMinutes(54).Ticks, runtime, 5));
    }
}
