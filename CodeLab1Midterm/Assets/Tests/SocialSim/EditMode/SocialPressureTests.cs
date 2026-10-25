using NUnit.Framework;
using SocialSim;

public class SocialPressureTests
{
    [TestCase(5)]
    [TestCase(20)]
    public void PlaygroundIgnoresCrowdingOnlyWhileActing(int neighbors)
    {
        var settings = new SimulationSettings { academicDecay = 0 };
        float academic = 80, fatigue = 20, stress = 50;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Playground, true, settings, neighbors);
        Assert.That(stress, Is.EqualTo(42));
        stress = 50;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Playground, false, settings, neighbors);
        Assert.That(stress, Is.EqualTo(50 + neighbors - settings.supportMaxNeighbors));
    }

    [TestCase(0, false, 2f, -6f)]
    [TestCase(1, false, 2f, -6f)]
    [TestCase(2, true, 1f, -11f)]
    [TestCase(4, true, 1f, -11f)]
    [TestCase(5, false, 3f, -6f)]
    [TestCase(6, false, 4f, -6f)]
    public void NeighborCountsChangeGrowthAndPlaygroundRecovery(int neighbors, bool supported, float growth, float playgroundChange)
    {
        var settings = new SimulationSettings { academicDecay = 0 };
        Assert.That(UtilityScorer.HasSocialSupport(neighbors, settings), Is.EqualTo(supported));
        float academic = 40, fatigue = 20, stress = 40;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Study, false, settings, neighbors);
        Assert.That(stress, Is.EqualTo(40 + growth));
        stress = 40;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Playground, true, settings, neighbors);
        Assert.That(stress, Is.EqualTo(40 + playgroundChange));
    }

    [Test]
    public void CrowdingGeneratesStressEvenWithGoodGradesAndClamps()
    {
        var settings = new SimulationSettings { academicDecay = 0 };
        float academic = 80, fatigue = 20, stress = 99;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 2, StudentAction.Home, true, settings, 6);
        Assert.That(stress, Is.EqualTo(100));
        stress = 1;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 2, StudentAction.Playground, true, settings, 2);
        Assert.That(stress, Is.Zero);
    }

    [Test]
    public void SupportBoundsAndRatesCanBeConfigured()
    {
        var settings = new SimulationSettings {
            academicDecay = 0, supportMinNeighbors = 1, supportMaxNeighbors = 2,
            supportedStressGainMultiplier = 0.25f, supportedRecoveryMultiplier = 2f,
            crowdStressPerNeighbor = 3f
        };
        float academic = 40, fatigue = 20, stress = 40;
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Study, false, settings, 1);
        Assert.That(stress, Is.EqualTo(40.5f));
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Playground, true, settings, 2);
        Assert.That(stress, Is.EqualTo(25f));
        UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, 1, StudentAction.Home, false, settings, 4);
        Assert.That(stress, Is.EqualTo(33f));
    }
}
