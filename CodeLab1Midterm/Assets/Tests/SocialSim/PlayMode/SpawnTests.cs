using System.Collections;
using System.Linq;
using NUnit.Framework;
using SocialSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SpawnTests
{
    [UnityTest]
    public IEnumerator AnyStudentCountUsesFiveDistinctSpawnPointsEvenlyWithoutOverlap()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var source = Object.FindAnyObjectByType<SocialManager>();
        var spawnPoints = source.spawnPoints.ToArray();
        Assert.That(spawnPoints.Length, Is.EqualTo(5));
        source.gameObject.SetActive(false);
        foreach (int count in new[] { 1, 5, 7, 15, 27, 50 })
        {
            var root = new GameObject("Spawn Distribution Test");
            root.SetActive(false);
            var manager = root.AddComponent<SocialManager>();
            manager.agentCount = count;
            manager.visualPrefab = source.visualPrefab;
            manager.studentAnimator = source.studentAnimator;
            manager.areas = source.areas;
            // Duplicate entries must not overweight the fifth point again.
            manager.spawnPoints = spawnPoints.Concat(Enumerable.Repeat(spawnPoints[4], 10)).ToArray();
            manager.navigationSurface = source.navigationSurface;
            manager.settings = source.settings;
            manager.showDebugLabels = false;
            manager.enableMouseDragging = false;
            manager.enableGroups = false;
            root.SetActive(true);
            yield return null;
            Assert.That(manager.Students.Count, Is.EqualTo(count), $"Count {count}");
            var distribution = Enumerable.Range(0, 5).Select(index => manager.Students.Count(s => s.SpawnPointIndex == index)).ToArray();
            Assert.That(distribution.Sum(), Is.EqualTo(count));
            Assert.That(distribution.Max() - distribution.Min(), Is.LessThanOrEqualTo(1));
            if (count == 15) Assert.That(distribution, Is.EqualTo(new[] { 3, 3, 3, 3, 3 }));
            if (count == 27) Assert.That(distribution, Is.EqualTo(new[] { 6, 6, 5, 5, 5 }));
            float spacing = Mathf.Max(manager.spawnSpacing, manager.settings.agentRadius * 2f + 0.1f);
            for (int i = 0; i < manager.Students.Count; i++)
            for (int j = 0; j < i; j++)
                Assert.That(Vector3.Distance(manager.Students[i].InitialPosition, manager.Students[j].InitialPosition),
                    Is.GreaterThanOrEqualTo(spacing - 0.001f), $"Count {count}, students {i}/{j}");
            Object.Destroy(root);
            yield return null;
        }
    }
}
