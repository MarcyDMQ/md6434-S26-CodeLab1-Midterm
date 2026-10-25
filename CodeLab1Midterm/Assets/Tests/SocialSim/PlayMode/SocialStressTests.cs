using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SocialStressTests
{
    static void SetStress(StudentAgent student, float value) =>
        typeof(StudentAgent).GetField("stress", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(student, value);

    static void Place(StudentAgent student, Vector3 position)
    {
        Assert.That(student.BeginDrag(), Is.True);
        Assert.That(student.TryDragTo(position), Is.True);
    }

    [UnityTest]
    public IEnumerator NeighborsCountStudentsOnceAndRespectRadiusAndEnabledState()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.ToArray();
        manager.settings.neighborRadius = 0.5f;
        Vector3 position = manager.spawnPoints[0].position;
        Place(students[0], position);
        Place(students[1], position);
        Place(students[2], position);
        Place(students[3], manager.spawnPoints[3].position);
        Place(students[4], manager.spawnPoints[4].position);
        students[1].gameObject.AddComponent<SphereCollider>().isTrigger = true;
        students[1].gameObject.AddComponent<BoxCollider>().isTrigger = true;
        Assert.That(manager.CountNeighbors(students[0]), Is.EqualTo(2));
        yield return null;
        Assert.That(students[0].NeighborCount, Is.EqualTo(2));
        Assert.That(students[0].HasSocialSupport, Is.True);
        students[1].enabled = false;
        Assert.That(manager.CountNeighbors(students[0]), Is.EqualTo(1));
        students[2].gameObject.SetActive(false);
        Assert.That(manager.CountNeighbors(students[0]), Is.Zero);
    }

    [UnityTest]
    public IEnumerator StressMaterialsAreIndependentReversibleAndCleanedUp()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        manager.settings.stressGain = 0;
        manager.settings.crowdStressPerNeighbor = 0;
        var first = manager.Students[0];
        var second = manager.Students[1];
        first.BeginDrag(); second.BeginDrag();
        typeof(StudentAgent).GetField("fatigue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(first, 0f);
        typeof(StudentAgent).GetField("fatigue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(second, 0f);
        yield return null;
        yield return null;
        var firstRenderer = first.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
        var secondRenderer = second.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
        var firstMaterial = firstRenderer.sharedMaterials[0];
        var secondMaterial = secondRenderer.sharedMaterials[0];
        Assert.That(firstMaterial, Is.Not.SameAs(secondMaterial));
        var otherColor = secondMaterial.GetColor("_Color");
        var originalColor = firstMaterial.GetColor("_Color");
        var assetColor = manager.stressMaterial.GetColor("_Color");
        var testTarget = new Material(manager.stressMaterial);
        testTarget.SetColor("_Color", Color.red);
        var targetColor = testTarget.GetColor("_Color");
        typeof(StudentStressVisual).GetField("targetMaterial", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(first.GetComponent<StudentStressVisual>(), testTarget);
        SetStress(first, 45);
        yield return null;
        yield return null;
        var expectedColor = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? Color.Lerp(originalColor.linear, targetColor.linear, 0.5f).gamma
            : Color.Lerp(originalColor, targetColor, 0.5f);
        Assert.That(firstMaterial.GetColor("_Color"), Is.EqualTo(expectedColor).Using(ColorComparer.Instance));
        Assert.That(secondMaterial.GetColor("_Color"), Is.EqualTo(otherColor).Using(ColorComparer.Instance));
        SetStress(first, 0);
        yield return null;
        yield return null;
        Assert.That(firstMaterial.GetColor("_Color"), Is.EqualTo(originalColor).Using(ColorComparer.Instance));
        Assert.That(manager.stressMaterial.GetColor("_Color"), Is.EqualTo(assetColor));
        Object.Destroy(first.gameObject);
        yield return null;
        yield return null;
        Assert.That(firstMaterial == null, Is.True);
        Assert.That(secondMaterial != null, Is.True);
        Object.Destroy(testTarget);
    }

    [UnityTest]
    public IEnumerator FatigueBlendsToBlueAndRestoresWithStressPriority()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        manager.settings.stressGain = 0;
        manager.settings.crowdStressPerNeighbor = 0;
        manager.settings.explosionGracePeriod = 999;
        Assert.That(manager.fatigueMaterial, Is.Not.Null);
        var student = manager.Students[0];
        student.BeginDrag();
        var fatigueField = typeof(StudentAgent).GetField("fatigue", BindingFlags.Instance | BindingFlags.NonPublic);
        SetStress(student, 0); fatigueField.SetValue(student, 0f);
        yield return null;
        yield return null;
        var material = student.GetComponentsInChildren<SkinnedMeshRenderer>()[0].sharedMaterials[0];
        var original = material.GetColor("_Color");
        var blue = manager.fatigueMaterial.GetColor("_Color");
        manager.settings.fatigueTintThreshold = 60f;
        fatigueField.SetValue(student, 30f);
        yield return null;
        yield return null;
        Assert.That(material.GetColor("_Color"), Is.EqualTo(original).Using(ColorComparer.Instance));
        fatigueField.SetValue(student, 60f);
        yield return null;
        yield return null;
        Assert.That(material.GetColor("_Color"), Is.EqualTo(original).Using(ColorComparer.Instance));
        fatigueField.SetValue(student, 80f);
        yield return null;
        yield return null;
        var half = QualitySettings.activeColorSpace == ColorSpace.Linear ? Color.Lerp(original.linear, blue.linear, 0.5f).gamma : Color.Lerp(original, blue, 0.5f);
        Assert.That(material.GetColor("_Color"), Is.EqualTo(half).Using(ColorComparer.Instance));
        fatigueField.SetValue(student, 100f);
        yield return null;
        yield return null;
        Assert.That(material.GetColor("_Color"), Is.EqualTo(blue).Using(ColorComparer.Instance));
        SetStress(student, manager.settings.stressExplosionThreshold);
        yield return null;
        yield return null;
        Assert.That(material.GetColor("_Color"), Is.EqualTo(manager.stressMaterial.GetColor("_Color")).Using(ColorComparer.Instance));
        SetStress(student, 0); fatigueField.SetValue(student, 0f);
        yield return null;
        yield return null;
        Assert.That(material.GetColor("_Color"), Is.EqualTo(original).Using(ColorComparer.Instance));
        Assert.That(manager.fatigueMaterial.GetColor("_Color"), Is.EqualTo(blue));
    }

    sealed class ColorComparer : System.Collections.Generic.IEqualityComparer<Color>
    {
        public static readonly ColorComparer Instance = new ColorComparer();
        public bool Equals(Color a, Color b) => Mathf.Abs(a.r-b.r) < 0.001f && Mathf.Abs(a.g-b.g) < 0.001f &&
            Mathf.Abs(a.b-b.b) < 0.001f && Mathf.Abs(a.a-b.a) < 0.001f;
        public int GetHashCode(Color value) => value.GetHashCode();
    }

    [UnityTest]
    public IEnumerator DraggedStudentExplodesOnceAtThresholdAndIsNotReplaced()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        manager.settings.explosionLifetime = 0.2f;
        manager.settings.explosionGracePeriod = 0;
        manager.settings.crowdStressPerNeighbor = 0;
        int initialCount = manager.Students.Count;
        var student = manager.Students[0];
        Assert.That(student.BeginDrag(), Is.True);
        Vector3 position = student.transform.position;
        SetStress(student, 89);
        manager.settings.stressGain = 0;
        yield return null;
        Assert.That(student.IsAlive, Is.True);
        SetStress(student, 90);
        yield return null;
        yield return null;
        Assert.That(student == null, Is.True);
        Assert.That(manager.Students.Count, Is.EqualTo(initialCount - 1));
        var explosions = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
            .Where(p => p.name == manager.explodePrefab.name + "(Clone)").ToArray();
        Assert.That(explosions.Length, Is.EqualTo(1));
        Assert.That(Vector3.Distance(explosions[0].transform.position, position), Is.LessThan(0.01f));
        var effect = explosions[0];
        yield return new WaitForSeconds(0.3f);
        Assert.That(effect == null, Is.True);
        Assert.That(manager.Students.Count, Is.EqualTo(initialCount - 1));
    }

    [UnityTest]
    public IEnumerator StartupProtectionAllowsFullStressThenExpiresWithoutResettingStress()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        Assert.That(manager.settings.explosionGracePeriod, Is.EqualTo(15f));
        manager.settings.crowdStressPerNeighbor = 0;
        int initialCount = manager.Students.Count;
        var full = manager.Students[0];
        var growing = manager.Students[1];
        var recovering = manager.Students[2];
        full.BeginDrag(); growing.BeginDrag(); recovering.BeginDrag();
        SetStress(full, 100);
        SetStress(growing, 10);
        SetStress(recovering, 100);
        typeof(StudentAgent).GetField("academic", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(growing, 0f);
        typeof(StudentAgent).GetField("academic", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recovering, 95f);
        float previousScale = Time.timeScale;
        Time.timeScale = 10f;
        try
        {
            bool loweredBeforeEnd = false;
            while (manager.IsExplosionProtected)
            {
                Assert.That(full && full.IsAlive, Is.True);
                Assert.That(full.Stress, Is.EqualTo(100));
                Assert.That(recovering && recovering.IsAlive, Is.True);
                if (!loweredBeforeEnd && Time.time - manager.SimulationStartedAt >= 10f)
                { SetStress(recovering, 89); loweredBeforeEnd = true; }
                yield return null;
            }
            Assert.That(Time.time - manager.SimulationStartedAt, Is.GreaterThanOrEqualTo(15f));
            Assert.That(growing.Stress, Is.GreaterThan(10f));
            yield return null;
            yield return null;
            Assert.That(full == null, Is.True);
            Assert.That(recovering && recovering.IsAlive, Is.True);
            Assert.That(manager.Students.Count, Is.EqualTo(initialCount - 1));
        }
        finally { Time.timeScale = previousScale; }
    }
}
