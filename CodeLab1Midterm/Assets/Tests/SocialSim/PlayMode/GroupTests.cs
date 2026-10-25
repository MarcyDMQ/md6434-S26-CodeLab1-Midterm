using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialSim;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GroupTests
{
    static void Set(StudentAgent student, string field, float value) =>
        typeof(StudentAgent).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(student, value);

    IEnumerator Prepare()
    {
        Random.InitState(6434);
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        manager.enableGroups = false;
        manager.groupSettings.rejoinCooldown = 0;
        manager.settings.academicDecay = 0;
        manager.settings.studyGain = 0;
        manager.settings.studyFatigue = 0;
        manager.settings.restRecovery = 0;
        manager.settings.stressGain = 0;
        manager.settings.crowdStressPerNeighbor = 0;
        foreach (var student in manager.Students.ToArray())
        {
            student.BeginDrag();
            Set(student, "academic", 40); Set(student, "fatigue", 10); Set(student, "stress", 0);
            Set(student, "explorationSeconds", 0);
        }
        yield return null;
    }

    static void ReleaseCluster(SocialManager manager, StudentAgent[] students, Vector3 origin)
    {
        for (int i = 0; i < students.Length; i++)
        {
            Assert.That(students[i].TryDragTo(origin + Vector3.right * (0.35f * i)), Is.True);
            students[i].EndDrag();
        }
    }

    [UnityTest]
    public IEnumerator TwoGroupsPersistAcrossRoundsAndAllAreasAreReachable()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.ToArray();
        ReleaseCluster(manager, students.Take(3).ToArray(), manager.spawnPoints[0].position);
        ReleaseCluster(manager, students.Skip(3).Take(3).ToArray(), manager.spawnPoints[4].position);
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 5;
        try
        {
            float deadline = Time.time + 2;
            while (Time.time < deadline && manager.GroupCoordinator.Groups.Count < 2) yield return null;
            Assert.That(manager.GroupCoordinator.Groups.Count, Is.EqualTo(2));
            var groups = manager.GroupCoordinator.Groups.ToArray();
            Assert.That(groups.All(g => g.Members.Count == 3), Is.True);
            foreach (var group in groups)
            foreach (var area in manager.areas)
            {
                foreach (var member in group.Members)
                {
                    Assert.That(area.TryGetDestination(member.GetComponent<NavMeshAgent>(), out var position), Is.True, area.name);
                    Assert.That(area.IsReached(position), Is.True);
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(member.transform.position, position, NavMesh.AllAreas, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                }
            }
            deadline = Time.time + 40;
            while (Time.time < deadline && groups.Any(g => g.CompletedRounds < 2)) yield return null;
            foreach (var group in groups)
            {
                Assert.That(manager.GroupCoordinator.Groups, Does.Contain(group));
                Assert.That(group.CompletedRounds, Is.GreaterThanOrEqualTo(2));
                Assert.That(group.Members.All(m => m.GroupId == group.Id && m.CurrentAction == group.Action), Is.True);
                Assert.That(group.Action, Is.EqualTo(StudentAction.Study));
                Assert.That(group.Leader, Is.Not.Null);
                Assert.That(group.Members.All(m => group.Area.IsReached(m.transform.position)), Is.True);
                Assert.That(group.AverageScores.Study, Is.EqualTo(group.Members.Average(m => m.Scores.Study)).Within(0.001));
            }
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator GroupCapacityDraggingDisableDeathAndToggleReleaseMembers()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.Take(6).ToArray();
        ReleaseCluster(manager, students, manager.spawnPoints[0].position);
        manager.groupSettings.rejoinCooldown = 5;
        manager.enableGroups = true;
        yield return new WaitForSeconds(0.6f);
        var group = manager.GroupCoordinator.Groups.Single();
        Assert.That(group.Members.Count, Is.EqualTo(5));
        var dragged = group.Members[0];
        Assert.That(dragged.BeginDrag(), Is.True);
        Assert.That(dragged.IsInGroup, Is.False);
        Assert.That(group.Members.Count, Is.EqualTo(4));
        dragged.EndDrag(false);
        Assert.That(dragged.CanJoinGroup, Is.False);
        var disabled = group.Members[0];
        disabled.enabled = false;
        Assert.That(group.Members.Count, Is.EqualTo(3));
        manager.settings.explosionGracePeriod = 0;
        Set(group.Members[0], "stress", 90);
        yield return null;
        yield return null;
        Assert.That(group.Members.Count, Is.EqualTo(2));
        manager.enableGroups = false;
        yield return null;
        yield return null;
        Assert.That(manager.GroupCoordinator.Groups.Count, Is.Zero);
        Assert.That(manager.Students.All(s => !s.IsInGroup), Is.True);
    }

    [UnityTest]
    public IEnumerator EmergencyLeavesGroupAndLatchesPressureBeforeFatigue()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(3).ToArray(), manager.spawnPoints[0].position);
        manager.groupSettings.rejoinCooldown = 5;
        manager.enableGroups = true;
        yield return new WaitForSeconds(0.6f);
        var group = manager.GroupCoordinator.Groups.Single();
        var student = group.Members[0];
        Set(student, "stress", 75); Set(student, "fatigue", 85);
        yield return null;
        yield return null;
        Assert.That(student.IsInGroup, Is.False);
        Assert.That(student.IsHandlingEmergency, Is.True);
        Assert.That(student.EmergencyAction, Is.EqualTo(StudentAction.Playground));
        Assert.That(group.Members.Count, Is.EqualTo(2));
        Set(student, "stress", 50);
        yield return null;
        Assert.That(student.EmergencyAction, Is.EqualTo(StudentAction.Playground));
        Set(student, "stress", 40);
        yield return null;
        yield return null;
        Assert.That(student.EmergencyAction, Is.EqualTo(StudentAction.Home));
        Set(student, "fatigue", 40);
        yield return null;
        yield return null;
        Assert.That(student.IsHandlingEmergency, Is.False);
        Assert.That(student.CanJoinGroup, Is.False);
    }

    [UnityTest]
    public IEnumerator GatheringHasNoActionEffectsAndTimeoutDoesNotResetOtherMembers()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.Take(4).ToArray();
        ReleaseCluster(manager, students, manager.spawnPoints[0].position);
        manager.groupSettings.stalledTimeout = 20;
        manager.groupSettings.movingSeparation = 100;
        manager.groupSettings.rejoinCooldown = 5;
        manager.settings.studyGain = 6;
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 3;
        try
        {
            float deadline = Time.time + 2;
            while (Time.time < deadline && manager.GroupCoordinator.Groups.Count == 0) yield return null;
            var group = manager.GroupCoordinator.Groups.Single();
            var late = group.Members.Last(m => m != group.Leader);
            late.GetComponent<NavMeshAgent>().speed = 0;
            // Control arrivals independently of local avoidance: only the designated late member is absent.
            Assert.That(group.Area.TryGetDestination(group.Leader.GetComponent<NavMeshAgent>(), out var reference), Is.True);
            var placed = new System.Collections.Generic.List<Vector3>();
            foreach (var ready in group.Members.Where(m => m != late))
            {
                var navigation = ready.GetComponent<NavMeshAgent>();
                bool positioned = false;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    float angle = attempt * Mathf.PI / 8;
                    var candidate = placed.Count == 0 ? reference : reference + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.8f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.3f, NavMesh.AllAreas) || !group.Area.IsReached(hit.position) ||
                        placed.Any(p => Vector3.Distance(p, hit.position) < 0.65f)) continue;
                    Assert.That(navigation.Warp(hit.position), Is.True);
                    Assert.That(navigation.SetDestination(hit.position), Is.True);
                    placed.Add(hit.position); positioned = true; break;
                }
                Assert.That(positioned, Is.True);
            }
            deadline = Time.time + 20;
            while (Time.time < deadline && group.Phase != GroupPhase.Gathering) yield return null;
            Assert.That(group.Phase, Is.EqualTo(GroupPhase.Gathering));
            var arrived = group.Members.First(m => m.HasArrivedWithGroup);
            float academic = arrived.Academic;
            yield return new WaitForSeconds(0.25f);
            Assert.That(arrived.Academic, Is.EqualTo(academic).Within(0.001));
            deadline = Time.time + 5;
            while (Time.time < deadline && group.Phase != GroupPhase.Acting) yield return null;
            Assert.That(group.Phase, Is.EqualTo(GroupPhase.Acting));
            Assert.That(late.IsInGroup, Is.False);
            Assert.That(group.Members.Count, Is.EqualTo(3));
            Assert.That(group.ActionEndsAt - group.ActionStartedAt, Is.EqualTo(manager.settings.stayDuration).Within(0.001));
            float endsAt = group.ActionEndsAt;
            group.Members[0].BeginDrag();
            Assert.That(group.Members.Count, Is.EqualTo(2));
            Assert.That(group.Phase, Is.EqualTo(GroupPhase.Acting));
            Assert.That(group.ActionEndsAt, Is.EqualTo(endsAt));
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator DropJoinsMovingGroupImmediatelyDespitePreferenceAndCooldown()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(3).ToArray(), manager.spawnPoints[0].position);
        manager.groupSettings.rejoinCooldown = 5;
        manager.enableGroups = true;
        yield return new WaitForSeconds(0.6f);
        var group = manager.GroupCoordinator.Groups.Single();
        Assert.That(group.Phase, Is.EqualTo(GroupPhase.Moving));
        var student = manager.Students[3];
        Set(student, "academic", 95); Set(student, "fatigue", 60);
        yield return null;
        Assert.That(student.PreferredAction, Is.EqualTo(StudentAction.Home));
        Assert.That(student.TryDragTo(group.Members[0].transform.position), Is.True);
        student.EndDrag();
        Assert.That(student.GroupId, Is.EqualTo(group.Id));
        Assert.That(group.Members.Count, Is.EqualTo(4));
        Assert.That(student.CurrentAction, Is.EqualTo(group.Action));
        Assert.That(student.Phase, Is.EqualTo(StudentPhase.Moving));
    }

    [UnityTest]
    public IEnumerator DropIntoActingGroupKeepsExistingClockAndPositions()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(3).ToArray(), manager.spawnPoints[0].position);
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 3;
        try
        {
            float deadline = Time.time + 20;
            while (Time.time < deadline && !manager.GroupCoordinator.Groups.Any(g => g.Phase == GroupPhase.Acting)) yield return null;
            var group = manager.GroupCoordinator.Groups.Single(g => g.Phase == GroupPhase.Acting);
            var members = group.Members.ToArray();
            var positions = members.Select(m => m.transform.position).ToArray();
            float endsAt = group.ActionEndsAt;
            var student = manager.Students[3];
            Set(student, "academic", 95); Set(student, "fatigue", 60);
            Assert.That(student.TryDragTo(members[0].transform.position), Is.True);
            student.EndDrag();
            Assert.That(student.GroupId, Is.EqualTo(group.Id));
            Assert.That(group.Phase, Is.EqualTo(GroupPhase.Acting));
            Assert.That(group.ActionEndsAt, Is.EqualTo(endsAt));
            for (int i = 0; i < members.Length; i++) Assert.That(Vector3.Distance(members[i].transform.position, positions[i]), Is.LessThan(0.01f));
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator FullGroupsAndDistantDropsDoNotForceJoin()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(5).ToArray(), manager.spawnPoints[0].position);
        manager.enableGroups = true;
        yield return new WaitForSeconds(0.6f);
        var group = manager.GroupCoordinator.Groups.Single();
        var student = manager.Students[5];
        Assert.That(student.TryDragTo(group.Members[0].transform.position), Is.True);
        student.EndDrag();
        Assert.That(student.IsInGroup, Is.False);
        Assert.That(group.Members.Count, Is.EqualTo(5));
        group.Members[0].BeginDrag();
        student.BeginDrag();
        Assert.That(student.TryDragTo(manager.spawnPoints[4].position), Is.True);
        student.EndDrag();
        Assert.That(student.IsInGroup, Is.False);
        Assert.That(group.Members.Count, Is.EqualTo(4));
    }

    [UnityTest]
    public IEnumerator CompactGroupWaitsForSlowerCompanionAndFinishesActivity()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(3).ToArray(), manager.spawnPoints[0].position);
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 3;
        try
        {
            float deadline = Time.time + 2;
            while (Time.time < deadline && manager.GroupCoordinator.Groups.Count == 0) yield return null;
            var group = manager.GroupCoordinator.Groups.Single();
            group.Members.Last(m => m != group.Leader).GetComponent<NavMeshAgent>().speed = 0.8f;
            var originalMembers = group.Members.ToArray();
            var trace = new System.Collections.Generic.List<string>();
            float nextTrace = 0;
            bool waited = false;
            deadline = Time.time + 30;
            while (Time.time < deadline && group.CompletedRounds == 0)
            {
                if (Time.time >= nextTrace)
                {
                    nextTrace = Time.time + 0.5f;
                    trace.Add($"{Time.time:F1} {group.Phase}: " + string.Join("; ", originalMembers.Select(m =>
                        $"{m.Id}: G{m.GroupId} {m.Phase} {m.transform.position} rem={m.GetComponent<NavMeshAgent>().remainingDistance:F2} stop={m.GetComponent<NavMeshAgent>().isStopped}")));
                }
                waited |= group.Members.Any(m => m.Phase == StudentPhase.Moving &&
                    (m.GetComponent<NavMeshAgent>().isStopped || m.GroupSpeedMultiplier < 0.95f));
                yield return null;
            }
            Assert.That(waited, Is.True);
            Assert.That(manager.GroupCoordinator.Groups, Does.Contain(group));
            Assert.That(group.Members.Count, Is.EqualTo(3), string.Join("\n", trace));
            Assert.That(group.CompletedRounds, Is.GreaterThanOrEqualTo(1));
            float span = group.Members.Max(a => group.Members.Max(b => Vector3.Distance(a.transform.position, b.transform.position)));
            Assert.That(span, Is.LessThanOrEqualTo(manager.groupSettings.maxGroupSpread));
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator DraggingLeaderElectsAnotherMemberWithoutDissolvingGroup()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        ReleaseCluster(manager, manager.Students.Take(4).ToArray(), manager.spawnPoints[0].position);
        manager.enableGroups = true;
        yield return new WaitForSeconds(0.6f);
        var group = manager.GroupCoordinator.Groups.Single();
        var leader = group.Leader;
        Assert.That(leader.BeginDrag(), Is.True);
        Assert.That(leader.IsInGroup, Is.False);
        Assert.That(group.Leader, Is.Not.Null);
        Assert.That(group.Leader, Is.Not.SameAs(leader));
        Assert.That(group.Members.Count, Is.EqualTo(3));
        yield return new WaitForSeconds(0.3f);
        Assert.That(manager.GroupCoordinator.Groups, Does.Contain(group));
        Assert.That(group.Leader.CurrentAction, Is.EqualTo(group.Action));
    }

    [UnityTest]
    public IEnumerator PlaygroundGroupStandsOnActualPlatformsBeforeRelievingStress()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.Take(3).ToArray();
        foreach (var student in students)
        { Set(student, "academic", 95); Set(student, "fatigue", 10); Set(student, "stress", 50); }
        ReleaseCluster(manager, students, manager.spawnPoints[4].position);
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 3;
        try
        {
            float deadline = Time.time + 25;
            while (Time.time < deadline && !students.All(s => s.CurrentAction == StudentAction.Playground && s.Phase == StudentPhase.Acting)) yield return null;
            var playground = manager.areas.Single(a => a.action == StudentAction.Playground);
            foreach (var student in students)
            {
                Assert.That(student.CurrentAction, Is.EqualTo(StudentAction.Playground));
                Assert.That(student.Phase, Is.EqualTo(StudentPhase.Acting));
                Assert.That(playground.IsReached(student.transform.position), Is.True);
                bool aboveMesh = playground.activitySurfaces.Any(surface => {
                    var bounds = surface.bounds;
                    var ray = new Ray(new Vector3(student.transform.position.x, bounds.max.y + 1f, student.transform.position.z), Vector3.down);
                    return surface.Raycast(ray, out var hit, bounds.size.y + 2f) &&
                        Mathf.Abs(student.transform.position.y - hit.point.y) <= playground.surfaceHeightTolerance;
                });
                Assert.That(aboveMesh, Is.True, student.name);
            }
            var pressures = students.Select(s => s.Stress).ToArray();
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < students.Length; i++) Assert.That(students[i].Stress, Is.LessThan(pressures[i]));
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator OpenGroundGroupSpreadsSidewaysInsteadOfRemainingInAQueue()
    {
        yield return Prepare();
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var students = manager.Students.Take(3).ToArray();
        var origin = manager.spawnPoints[2].position;
        var direction = manager.areas.Single(a => a.action == StudentAction.Study).Center - origin;
        direction.y = 0; direction.Normalize();
        var side = Vector3.Cross(Vector3.up, direction);
        for (int i = 0; i < students.Length; i++)
        {
            Assert.That(students[i].TryDragTo(origin + direction * (i - 1) * 0.7f), Is.True);
            students[i].EndDrag();
        }
        float initialWidth = students.Max(s => Vector3.Dot(s.transform.position, side)) - students.Min(s => Vector3.Dot(s.transform.position, side));
        var initialCenter = students.Select(s => s.transform.position).Aggregate(Vector3.zero, (a, b) => a + b) / students.Length;
        manager.enableGroups = true;
        float previous = Time.timeScale;
        Time.timeScale = 3;
        try
        {
            float end = Time.time + 2f;
            float widest = initialWidth;
            while (Time.time < end)
            {
                widest = Mathf.Max(widest, students.Max(s => Vector3.Dot(s.transform.position, side)) - students.Min(s => Vector3.Dot(s.transform.position, side)));
                yield return null;
            }
            Assert.That(students.All(s => s.IsInGroup && s.GroupId == students[0].GroupId), Is.True);
            Assert.That(widest, Is.GreaterThan(Mathf.Max(0.75f, initialWidth + 0.35f)));
            var center = students[0].Group.Center;
            Assert.That(Vector3.Dot(center - initialCenter, direction), Is.GreaterThan(1f));
        }
        finally { Time.timeScale = previous; }
    }
}
