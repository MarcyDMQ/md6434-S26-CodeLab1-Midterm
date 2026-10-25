using NUnit.Framework;
using SocialSim;

public class UtilityScorerTests
{
    [Test]
    public void StressGrowsOnlyBelowAcademicThreshold()
    {
        var settings = new SimulationSettings { academicDecay = 0 };
        float a = 50, f = 20, stress = 10;
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Home, false, settings);
        Assert.That(stress, Is.EqualTo(10));
        a = 49;
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Study, false, settings);
        Assert.That(stress, Is.EqualTo(14));
    }
    [Test]
    public void PlaygroundRelievesStressOnlyAfterArrivalAndClamps()
    {
        var settings = new SimulationSettings { academicDecay = 0 };
        float a = 40, f = 20, stress = 30;
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Playground, false, settings);
        Assert.That(stress, Is.EqualTo(34));
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Playground, true, settings);
        Assert.That(stress, Is.EqualTo(22));
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 10, StudentAction.Playground, true, settings);
        Assert.That(stress, Is.Zero);
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 100, StudentAction.Home, false, settings);
        Assert.That(stress, Is.EqualTo(100));
    }
    [Test]
    public void StressIncreasesPlaygroundUtilityWhileHighFatigueStillPrefersHome()
    {
        var calm = UtilityScorer.Evaluate(40, 10, 0, 0);
        var stressed = UtilityScorer.Evaluate(40, 10, 80, 0);
        Assert.That(stressed.Playground, Is.GreaterThan(calm.Playground));
        Assert.That(stressed.Playground, Is.GreaterThan(stressed.Study));
        var tired = UtilityScorer.Evaluate(40, 90, 100, 1000);
        Assert.That(tired.Home, Is.GreaterThan(tired.Playground));
    }
    [Test]
    public void LowAcademicAndFatiguePreferStudy()
    {
        var scores = UtilityScorer.Evaluate(10, 5, 0, 0);
        Assert.That(scores.Study, Is.GreaterThan(scores.Home));
        Assert.That(scores.Study, Is.GreaterThan(scores.Playground));
    }
    [Test]
    public void HighFatiguePrefersHomeEvenAfterLongExplorationWait()
    {
        var scores = UtilityScorer.Evaluate(0, 90, 0, 1000);
        Assert.That(scores.Home, Is.GreaterThan(scores.Study));
        Assert.That(scores.Home, Is.GreaterThan(scores.Playground));
    }
    [Test]
    public void ExplorationGrowsCapsAndResets()
    {
        Assert.That(UtilityScorer.Evaluate(90, 0, 0, 40).Playground, Is.EqualTo(45));
        Assert.That(UtilityScorer.Evaluate(90, 0, 0, 400).Playground, Is.EqualTo(45));
        Assert.That(UtilityScorer.Evaluate(90, 0, 0, 0).Playground, Is.EqualTo(5));
    }
    [Test]
    public void TravelOnlyDecaysAcademicAndStudyAddsBothNeeds()
    {
        float a = 50, f = 20, stress = 0;
        var settings = new SimulationSettings();
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Study, false, settings);
        Assert.That(a, Is.EqualTo(49)); Assert.That(f, Is.EqualTo(20));
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Study, true, settings);
        Assert.That(a, Is.EqualTo(60)); Assert.That(f, Is.EqualTo(28));
    }
    [Test]
    public void NeedsClampAndHomeRestoresFatigue()
    {
        float a = 99, f = 99, stress = 0;
        var settings = new SimulationSettings();
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 2, StudentAction.Study, true, settings);
        Assert.That(a, Is.EqualTo(100)); Assert.That(f, Is.EqualTo(100));
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 20, StudentAction.Home, true, settings);
        Assert.That(a, Is.EqualTo(90)); Assert.That(f, Is.Zero);
        UtilityScorer.UpdateNeeds(ref a, ref f, ref stress, 1000, StudentAction.Playground, true, settings);
        Assert.That(a, Is.Zero); Assert.That(f, Is.Zero);
    }
}
