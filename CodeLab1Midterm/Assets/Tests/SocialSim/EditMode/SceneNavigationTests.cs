using System.Linq;
using NUnit.Framework;
using SocialSim;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class SceneNavigationTests
{
    [Test]
    public void AreasAndCapsuleSurfacesAreReachable()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SocialSim.unity", OpenSceneMode.Additive);
        GameObject probe = null;
        try
        {
            SocialManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out SocialManager found)) manager = found;
            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.visualPrefab, Is.Not.Null);
            Assert.That(manager.studentAnimator, Is.Not.Null);
            Assert.That(manager.areas.Length, Is.EqualTo(3));
            Assert.That(manager.spawnPoints.Length, Is.EqualTo(5));
            Assert.That(manager.spawnPoints.Distinct().Count(), Is.EqualTo(5));
            manager.navigationSurface.BuildNavMesh();
            var playground = System.Array.Find(manager.areas, area => area.action == StudentAction.Playground);
            Assert.That(playground.requireSurfaceContact, Is.True);
            Assert.That(NavMesh.SamplePosition(manager.spawnPoints[0].position, out var origin, 0.4f, NavMesh.AllAreas), Is.True);
            foreach (var spawn in manager.spawnPoints)
                Assert.That(NavMesh.SamplePosition(spawn.position, out _, 0.4f, NavMesh.AllAreas), Is.True, spawn.name);
            probe = new GameObject("Navigation Test Probe");
            probe.transform.position = origin.position;
            var mover = probe.AddComponent<NavMeshAgent>();
            foreach (var area in manager.areas)
            {
                Assert.That(area.target, Is.Not.Null, area.name);
                Assert.That(area.TryGetDestination(mover, out var destination), Is.True, area.name);
                Assert.That(area.IsReached(destination), Is.True, area.name);
            }
            Physics.SyncTransforms();
            int capsuleCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
            {
                if (!collider.name.StartsWith("Capsule")) continue;
                capsuleCount++;
                var bounds = collider.bounds;
                var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z), Vector3.down);
                Assert.That(collider.Raycast(ray, out var surface, 3f), Is.True, collider.name);
                Assert.That(NavMesh.SamplePosition(surface.point, out var top, 0.15f, NavMesh.AllAreas), Is.True, collider.name);
                Assert.That(Mathf.Abs(top.position.y - surface.point.y), Is.LessThan(0.09f), collider.name);
                Assert.That(playground.IsReached(top.position), Is.True, collider.name);
                // Being within the old 0.65-unit expanded bounds must not count as standing on the platform.
                var beside = new Vector3(bounds.max.x + 0.3f, surface.point.y, bounds.center.z);
                if (!playground.activitySurfaces.Any(s => beside.x >= s.bounds.min.x && beside.x <= s.bounds.max.x &&
                    beside.z >= s.bounds.min.z && beside.z <= s.bounds.max.z))
                    Assert.That(playground.IsReached(beside), Is.False, collider.name);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(origin.position, top.position, NavMesh.AllAreas, path), Is.True, collider.name);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), collider.name);
            }
            Assert.That(capsuleCount, Is.EqualTo(3));
            var outerSurface = playground.activitySurfaces.OrderByDescending(s => s.bounds.max.x).First();
            var outerBounds = outerSurface.bounds;
            var outside = new Vector3(outerBounds.max.x + 0.3f, outerBounds.max.y, outerBounds.center.z);
            Assert.That(outerBounds.SqrDistance(outside), Is.LessThan(playground.arrivalDistance * playground.arrivalDistance));
            Assert.That(playground.IsReached(outside), Is.False, "Outside the actual platform but inside the previous expanded bounds");
        }
        finally
        {
            if (probe) Object.DestroyImmediate(probe);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
}
