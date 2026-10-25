using NUnit.Framework;
using SocialSim;
using UnityEngine;

public class GroupSteeringTests
{
    [Test]
    public void PersonalBiasSpreadsCompanionsToBothSidesWhileMovingForward()
    {
        var settings = new GroupSettings { cohesionWeight = 0, separationWeight = 0 };
        var first = GroupSteering.Direction(1, Vector3.zero, Vector3.forward, new Vector3[0], settings);
        var second = GroupSteering.Direction(2, Vector3.zero, Vector3.forward, new Vector3[0], settings);
        Assert.That(first.z, Is.GreaterThan(0));
        Assert.That(second.z, Is.GreaterThan(0));
        Assert.That(first.x * second.x, Is.LessThan(0));
    }

    [Test]
    public void NearbyCompanionCausesLateralSeparationAndDistantCompanionAttracts()
    {
        var settings = new GroupSettings { sideBiasWeight = 0, cohesionWeight = 0 };
        var away = GroupSteering.Direction(1, Vector3.zero, Vector3.forward, new[] { Vector3.right * 0.3f }, settings);
        Assert.That(away.x, Is.LessThan(0));
        settings.separationWeight = 0; settings.cohesionWeight = 1;
        var toward = GroupSteering.Direction(1, Vector3.zero, Vector3.forward, new[] { Vector3.right * 4 }, settings);
        Assert.That(toward.x, Is.GreaterThan(0));
        Assert.That(toward.z, Is.GreaterThan(0));
    }
}
