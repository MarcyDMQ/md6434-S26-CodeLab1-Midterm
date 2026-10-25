using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace SocialSim
{
    public class StudentGroupCoordinator : MonoBehaviour
    {
        readonly List<StudentGroup> groups = new List<StudentGroup>();
        SocialManager manager;
        float nextScan;
        int nextId = 1;
        public IReadOnlyList<StudentGroup> Groups => groups;
        GroupSettings Config => manager.groupSettings;
        public void Initialize(SocialManager owner) { manager = owner; nextScan = Time.time; }

        void LateUpdate()
        {
            if (!manager) return;
            if (!manager.enableGroups) { DissolveAll(); return; }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + Mathf.Max(0.05f, Config.scanInterval);
                foreach (var group in groups.ToArray()) if (group.Phase == GroupPhase.Deciding) Recruit(group);
                FormGroups();
            }
            foreach (var group in groups.ToArray())
            {
                foreach (var member in group.members.ToArray())
                    if (!member || !member.IsAlive || !member.isActiveAndEnabled) RemoveMember(member, group);
                if (groups.Contains(group)) Advance(group);
            }
        }

        List<StudentAgent> Candidates(Vector3 center, StudentAction action) => manager.Students
            .Where(s => s && s.CanJoinGroup && s.PreferredAction == action &&
                Vector3.Distance(s.transform.position, center) <= Config.formationDistance)
            .OrderBy(s => (s.transform.position - center).sqrMagnitude).ThenBy(s => s.Id).ToList();

        void FormGroups()
        {
            foreach (var student in manager.Students.OrderBy(s => s.Id).ToArray())
            {
                if (!student || !student.CanJoinGroup) continue;
                var candidates = Candidates(student.transform.position, student.PreferredAction);
                if (candidates.Count < 2) continue;
                var group = new StudentGroup(nextId++, student.PreferredAction);
                groups.Add(group);
                AddMember(group, student);
                foreach (var candidate in candidates)
                {
                    if (group.members.Count >= Mathf.Max(2, Config.maxMembers)) break;
                    if (candidate.CanJoinGroup) AddMember(group, candidate);
                }
            }
        }

        void Recruit(StudentGroup group)
        {
            if (group.members.Count < 2 || group.members.Count >= Mathf.Max(2, Config.maxMembers)) return;
            var preferred = UtilityScorer.PreferredAction(group.AverageScores, group.hasAction ? group.Action : (StudentAction?)null);
            foreach (var candidate in Candidates(group.Center, preferred))
            {
                if (group.members.Count >= Mathf.Max(2, Config.maxMembers)) break;
                AddMember(group, candidate);
            }
        }

        void AddMember(StudentGroup group, StudentAgent member)
        {
            group.members.Add(member);
            member.JoinGroup(group);
            if (!group.Leader) group.Leader = member;
        }

        public bool TryJoinDroppedStudent(StudentAgent student)
        {
            if (!manager || !manager.enableGroups || !isActiveAndEnabled || !student || !student.IsAlive ||
                !student.isActiveAndEnabled || student.IsInGroup || !student.Navigation.isOnNavMesh || student.IsHandlingEmergency ||
                student.Stress >= Config.emergencyStress || student.Fatigue >= Config.emergencyFatigue) return false;
            var nearby = groups.Where(g => g.members.Count >= 2 && g.members.Count < Mathf.Max(2, Config.maxMembers))
                .Select(g => new { Group = g, Distance = g.members.Min(m => Vector3.Distance(student.transform.position, m.transform.position)) })
                .Where(item => item.Distance <= Config.dropJoinDistance).OrderBy(item => item.Distance).ThenBy(item => item.Group.Id);
            foreach (var item in nearby)
            {
                var group = item.Group;
                if (!group.members.Any(m => CanReach(student, m.transform.position))) continue;
                AddMember(group, student);
                group.joinArrivalDeadlines[student] = Time.time + Config.gatheringTimeout;
                if (group.Phase != GroupPhase.Deciding && group.Area)
                {
                    student.PrepareGroupTravel(group.Area);
                    UpdateTravel(group);
                    if (group.Phase == GroupPhase.Acting && student.HasArrivedWithGroup) student.BeginGroupAction(group.Action);
                }
                return true;
            }
            return false;
        }

        public void RemoveMember(StudentAgent member)
        {
            if (member && member.Group != null) RemoveMember(member, member.Group);
        }
        void RemoveMember(StudentAgent member, StudentGroup group)
        {
            group.members.Remove(member);
            if (member)
            {
                group.joinArrivalDeadlines.Remove(member);
                if (member.Group == group) member.ClearGroup();
            }
            if (group.members.Count < 2) { Dissolve(group); return; }
            if (!group.Leader || group.Leader == member)
            {
                group.Leader = group.members.OrderByDescending(m => m.HasArrivedWithGroup).ThenBy(m => m.Id).First();
                group.nextCohesionAt = 0;
            }
        }
        void Dissolve(StudentGroup group)
        {
            groups.Remove(group);
            foreach (var member in group.members.ToArray()) if (member && member.Group == group) member.ClearGroup();
            group.members.Clear(); group.joinArrivalDeadlines.Clear(); group.Leader = null;
        }
        void DissolveAll() { foreach (var group in groups.ToArray()) Dissolve(group); }

        void Advance(StudentGroup group)
        {
            if (group.Phase == GroupPhase.Deciding)
            {
                if (Time.time >= group.retryAt) { Recruit(group); Decide(group); }
                return;
            }
            if (!group.Area || !group.Area.isActiveAndEnabled)
            {
                foreach (var member in group.members) member.JoinGroup(group);
                group.Phase = GroupPhase.Deciding; group.retryAt = Time.time;
                return;
            }
            if (Time.time >= group.nextCohesionAt)
            {
                group.nextCohesionAt = Time.time + Mathf.Max(0.05f, Config.cohesionCheckInterval);
                UpdateTravel(group);
            }
            if (!groups.Contains(group)) return;
            if (group.Phase == GroupPhase.Acting)
            {
                if (Time.time < group.ActionEndsAt)
                {
                    foreach (var member in group.members)
                        if (member.HasArrivedWithGroup && member.Phase != StudentPhase.Acting) member.BeginGroupAction(group.Action);
                    return;
                }
                foreach (var member in group.members) if (member.Phase == StudentPhase.Acting) member.CompleteGroupAction();
                group.CompletedRounds++; group.Phase = GroupPhase.Deciding; group.retryAt = Time.time;
                return;
            }
            if (group.members.Any(m => m.HasArrivedWithGroup) && group.firstArrivalAt < 0)
            { group.firstArrivalAt = Time.time; group.Phase = GroupPhase.Gathering; }
            if (group.firstArrivalAt >= 0 && Time.time - group.firstArrivalAt >= Config.gatheringTimeout)
            {
                foreach (var late in group.members.Where(m => !m.HasArrivedWithGroup &&
                    (!group.joinArrivalDeadlines.TryGetValue(m, out var deadline) || Time.time >= deadline)).ToArray()) RemoveMember(late);
                if (!groups.Contains(group)) return;
            }
            if (group.members.All(m => m.HasArrivedWithGroup)) BeginAction(group);
        }

        static NavMeshQueryFilter Filter(StudentAgent student) => new NavMeshQueryFilter {
            agentTypeID = student.Navigation.agentTypeID, areaMask = student.Navigation.areaMask
        };
        static bool CanReach(StudentAgent student, Vector3 position)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(student.transform.position, position, Filter(student), path) && path.status == NavMeshPathStatus.PathComplete;
        }
        static bool ClearConnection(StudentAgent from, StudentAgent to) =>
            !NavMesh.Raycast(from.transform.position, to.transform.position, out _, Filter(from));

        bool CanArrive(StudentGroup group, StudentAgent member)
        {
            if (!group.Area.IsReached(member.transform.position) || Vector3.Distance(member.transform.position, group.Center) > Config.maxGroupSpread) return false;
            var ready = group.members.Where(m => m != member && m.HasArrivedWithGroup).ToArray();
            if (ready.Length == 0) return true;
            return ready.Any(m => Vector3.Distance(member.transform.position, m.transform.position) <= Config.companionArrivalDistance && ClearConnection(member, m)) &&
                ready.All(m => Vector3.Distance(member.transform.position, m.transform.position) <= Config.maxGroupSpread);
        }

        bool TrySeparate(StudentGroup group, StudentAgent member)
        {
            foreach (var other in group.members)
            {
                if (other == member) continue;
                float clearance = member.Navigation.radius + other.Navigation.radius;
                var difference = member.transform.position - other.transform.position;
                difference.y = 0;
                if (difference.magnitude >= clearance * 0.9f) continue;
                if (difference.sqrMagnitude < 0.001f)
                {
                    float angle = member.Id * 2.399963f;
                    difference = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                }
                var escape = member.transform.position + difference.normalized * (clearance + 0.15f);
                if (NavMesh.SamplePosition(escape, out var hit, 0.3f, Filter(member)) && CanReach(member, hit.position))
                { member.NavigateWithGroup(group.Area, hit.position, 0.05f); return true; }
            }
            return false;
        }

        bool NavigateCohesively(StudentGroup group, StudentAgent member)
        {
            var nav = member.Navigation;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(member.transform.position, group.travelDestination, Filter(member), path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            var corner = group.travelDestination;
            foreach (var point in path.corners)
                if (Vector3.Distance(point, member.transform.position) > 0.15f) { corner = point; break; }
            var forward = corner - member.transform.position; forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) return member.NavigateWithGroup(group.Area, group.travelDestination, 0.1f);
            forward.Normalize();

            float farBehind = group.members.Where(m => m != member &&
                    Vector3.Dot(m.transform.position - member.transform.position, forward) < -0.1f)
                .Select(m => Vector3.Distance(member.transform.position, m.transform.position)).DefaultIfEmpty(0).Max();
            bool paused = member.GroupPaused && nav.hasPath ? farBehind > Config.resumeSeparation :
                farBehind > Mathf.Max(Config.waitSeparation, Config.movingSeparation + 0.2f);
            if (group.members.Any(m => m != member && Vector3.Distance(member.transform.position, m.transform.position) <
                nav.radius + m.Navigation.radius + 0.2f)) paused = false;

            float cruise = member.GroupCruiseSpeed;
            float slowest = group.members.Min(m => m.GroupCruiseSpeed);
            float ahead = Mathf.Max(0, Vector3.Dot(member.transform.position - group.Center, forward));
            float match = Mathf.Clamp01(ahead / Mathf.Max(0.3f, Config.cohesionDistance * 0.7f));
            float scale = Mathf.Lerp(1f, cruise > 0.001f ? Mathf.Min(1f, slowest / cruise) : 1f, match);
            scale *= Mathf.Lerp(1f, Config.minimumSpeedMultiplier,
                Mathf.InverseLerp(Config.resumeSeparation, Mathf.Max(Config.resumeSeparation + 0.1f, Config.movingSeparation), farBehind));
            member.ApplyGroupSpeed(scale);
            if (paused) { member.SetGroupPaused(true); return true; }

            var companions = group.members.Where(m => m != member).Select(m => m.transform.position).ToArray();
            var direction = GroupSteering.Direction(member.Id, member.transform.position, forward, companions, Config);
            float lookAhead = Mathf.Min(Config.steeringLookAhead, Vector3.Distance(member.transform.position, corner));
            var desired = member.transform.position + direction * lookAhead;
            if (NavMesh.SamplePosition(desired, out var hit, 0.3f, Filter(member)) &&
                !NavMesh.Raycast(member.transform.position, hit.position, out _, Filter(member)))
                return member.NavigateWithGroup(group.Area, hit.position, 0.05f);
            // Obstacles and narrow passages fall back to normal pathfinding; spread again on open ground.
            return member.NavigateWithGroup(group.Area, group.travelDestination, 0.1f);
        }

        void UpdateTravel(StudentGroup group)
        {
            if (!group.Leader || !group.Area) return;
            foreach (var member in group.members.OrderBy(m => m == group.Leader ? 0 : 1).ThenBy(m => m.Id).ToArray())
            {
                if (!groups.Contains(group)) return;
                if (member.HasArrivedWithGroup) continue;
                if (TrySeparate(group, member)) continue;
                if (CanArrive(group, member)) { member.MarkGroupArrival(); continue; }
                if (!group.members.Any(m => m.HasArrivedWithGroup))
                { if (!NavigateCohesively(group, member)) RemoveMember(member); continue; }
                member.ApplyGroupSpeed(1f);
                var buddy = group.members.Where(m => m != member && m.HasArrivedWithGroup)
                    .OrderBy(m => (member.transform.position - m.transform.position).sqrMagnitude).FirstOrDefault() ?? group.Leader;
                bool buddyReady = buddy.HasArrivedWithGroup;
                if (buddyReady && !group.Area.IsReached(member.transform.position))
                {
                    // Cross the area boundary naturally, rather than stopping outside it behind a companion.
                    var nav = member.Navigation;
                    bool clearGoal = nav.hasPath && group.Area.IsReached(nav.destination) &&
                        group.members.All(m => m == member || Vector3.Distance(nav.destination, m.transform.position) >= nav.radius + m.Navigation.radius);
                    if (clearGoal) { member.NavigateWithGroup(group.Area, nav.destination, 0.1f); continue; }
                    if (group.Area.TryGetDestination(nav, out var approach, group.members))
                    { member.NavigateWithGroup(group.Area, approach, 0.1f); continue; }
                }
                bool connected = ClearConnection(member, buddy);
                if (!buddyReady && connected && Vector3.Distance(member.transform.position, buddy.transform.position) <= Config.followDistance + 0.1f)
                    member.SetGroupPaused(true);
                else if (!member.NavigateWithGroup(group.Area, buddy.transform.position, connected ? Config.followDistance : 0.1f)) RemoveMember(member);
            }
        }

        bool AlreadyTogether(StudentGroup group, ActivityArea area)
        {
            return group.members.All(m => area.IsReached(m.transform.position) &&
                Vector3.Distance(m.transform.position, group.Center) <= Config.maxGroupSpread &&
                group.members.All(other => Vector3.Distance(m.transform.position, other.transform.position) <= Config.maxGroupSpread) &&
                group.members.All(other => other == m || Vector3.Distance(m.transform.position, other.transform.position) >=
                    (m.Navigation.radius + other.Navigation.radius) * 0.9f) &&
                group.members.Any(other => other != m && Vector3.Distance(m.transform.position, other.transform.position) <= Config.companionArrivalDistance && ClearConnection(m, other)));
        }

        void Decide(StudentGroup group)
        {
            var scores = group.AverageScores;
            var actions = new[] { StudentAction.Home, StudentAction.Study, StudentAction.Playground }
                .OrderByDescending(a => scores.Get(a)).ThenBy(a => group.hasAction && a == group.Action ? 0 : 1).ThenBy(a => (int)a);
            foreach (var action in actions)
            foreach (var area in manager.areas)
            {
                if (!area || !area.isActiveAndEnabled || area.action != action) continue;
                if (AlreadyTogether(group, area))
                {
                    group.Area = area; group.Action = action;
                    foreach (var member in group.members) member.MarkGroupArrival();
                    BeginAction(group); return;
                }
                var leader = group.members.OrderBy(m => (m.transform.position - area.Center).sqrMagnitude).ThenBy(m => m.Id).First();
                if (!area.TryGetDestination(leader.Navigation, out var destination) || !group.members.All(m => CanReach(m, destination))) continue;
                group.Area = area; group.Action = action; group.hasAction = true;
                group.Leader = leader; group.travelDestination = destination;
                group.firstArrivalAt = -1; group.nextCohesionAt = Time.time;
                group.joinArrivalDeadlines.Clear(); group.Phase = GroupPhase.Moving;
                foreach (var member in group.members) member.PrepareGroupTravel(area);
                UpdateTravel(group);
                return;
            }
            group.retryAt = Time.time + 1f;
        }

        void BeginAction(StudentGroup group)
        {
            group.Phase = GroupPhase.Acting; group.hasAction = true;
            group.ActionStartedAt = Time.time;
            group.ActionEndsAt = Time.time + Mathf.Max(0.1f, manager.settings.stayDuration);
            foreach (var member in group.members) member.BeginGroupAction(group.Action);
        }
        void OnDisable() => DissolveAll();
        void OnDrawGizmos()
        {
            foreach (var group in groups)
            {
                Gizmos.color = group.DebugColor;
                if (!group.Leader) continue;
                foreach (var member in group.members) if (member && member != group.Leader)
                    Gizmos.DrawLine(group.Leader.transform.position, member.transform.position);
            }
        }
    }
}
