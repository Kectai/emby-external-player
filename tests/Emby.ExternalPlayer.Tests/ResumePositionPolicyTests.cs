using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class ResumePositionPolicyTests
{
    [TestMethod]
    public void FromEmbyUserData_PreservesAnyPositiveResumePosition()
    {
        Assert.AreEqual(1, ResumePositionPolicy.FromEmbyUserData(1));
        Assert.AreEqual(
            TimeSpan.FromSeconds(9).Ticks,
            ResumePositionPolicy.FromEmbyUserData(TimeSpan.FromSeconds(9).Ticks));
    }

    [TestMethod]
    public void FromEmbyUserData_DoesNotApplyAPluginSpecificNearEndThreshold()
    {
        var episodeRuntime = TimeSpan.FromMinutes(24).Ticks;
        var embyResumePosition = TimeSpan.FromMinutes(23).Ticks;

        Assert.IsTrue(embyResumePosition < episodeRuntime);
        Assert.AreEqual(
            embyResumePosition,
            ResumePositionPolicy.FromEmbyUserData(embyResumePosition));
    }

    [TestMethod]
    public void FromEmbyUserData_RejectsOnlyMissingOrInvalidPositions()
    {
        Assert.AreEqual(0, ResumePositionPolicy.FromEmbyUserData(0));
        Assert.AreEqual(0, ResumePositionPolicy.FromEmbyUserData(-1));
    }
}
