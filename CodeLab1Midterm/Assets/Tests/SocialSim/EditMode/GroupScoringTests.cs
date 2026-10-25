using NUnit.Framework;
using SocialSim;

public class GroupScoringTests
{
    [Test]
    public void PreferredActionKeepsCurrentOnTiesAndOtherwiseUsesHighestScore()
    {
        var scores = new UtilityScores { Home = 20, Study = 20, Playground = 20 };
        Assert.That(UtilityScorer.PreferredAction(scores), Is.EqualTo(StudentAction.Home));
        Assert.That(UtilityScorer.PreferredAction(scores, StudentAction.Study), Is.EqualTo(StudentAction.Study));
        scores.Playground = 30;
        Assert.That(UtilityScorer.PreferredAction(scores, StudentAction.Study), Is.EqualTo(StudentAction.Playground));
    }
}
