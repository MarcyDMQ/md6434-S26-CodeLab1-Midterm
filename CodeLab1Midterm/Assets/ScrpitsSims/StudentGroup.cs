using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialSim
{
    [Serializable]
    public class GroupSettings
    {
        [Min(2)] public int maxMembers = 5;
        [Min(0.05f)] public float scanInterval = 0.5f;
        [Min(0.1f)] public float formationDistance = 3f;
        [Min(0)] public float rejoinCooldown = 5f;
        [Min(0.1f)] public float gatheringTimeout = 3f;
        [Min(0.1f)] public float stalledTimeout = 5f;
        [Min(0.1f)] public float cohesionCheckInterval = 0.15f;
        [Min(0.1f)] public float movingSeparation = 2f;
        [Min(0.1f)] public float resumeSeparation = 1.5f;
        [Min(0.1f)] public float followDistance = 0.9f;
        [Min(0.1f)] public float steeringLookAhead = 1.5f;
        [Min(0.1f)] public float cohesionDistance = 1f;
        [Min(0.1f)] public float separationDistance = 0.8f;
        [Min(0)] public float cohesionWeight = 0.85f;
        [Min(0)] public float separationWeight = 1.3f;
        [Min(0)] public float sideBiasWeight = 0.4f;
        [Range(0.05f, 1)] public float minimumSpeedMultiplier = 0.3f;
        [Min(0.1f)] public float speedChangeRate = 4f;
        [Min(0.1f)] public float waitSeparation = 3.5f;
        [Min(0.1f)] public float companionArrivalDistance = 1.5f;
        [Min(0.1f)] public float maxGroupSpread = 2.5f;
        [Min(0.1f)] public float dropJoinDistance = 1.5f;
        [Range(0, 100)] public float emergencyFatigue = 85f;
        [Range(0, 100)] public float emergencyStress = 75f;
        [Range(0, 100)] public float emergencyRelease = 40f;
    }

    public enum GroupPhase { Deciding, Moving, Gathering, Acting }

    public sealed class StudentGroup
    {
        internal readonly List<StudentAgent> members = new List<StudentAgent>();
        internal readonly Dictionary<StudentAgent, float> joinArrivalDeadlines = new Dictionary<StudentAgent, float>();
        public int Id { get; }
        public IReadOnlyList<StudentAgent> Members => members;
        public GroupPhase Phase { get; internal set; }
        public StudentAction Action { get; internal set; }
        public ActivityArea Area { get; internal set; }
        public StudentAgent Leader { get; internal set; }
        internal Vector3 travelDestination;
        public float ActionStartedAt { get; internal set; }
        public float ActionEndsAt { get; internal set; }
        public int CompletedRounds { get; internal set; }
        internal bool hasAction;
        internal float retryAt, firstArrivalAt = -1f, nextCohesionAt;
        public Color DebugColor => Color.HSVToRGB(Mathf.Repeat(Id * 0.618034f, 1f), 0.55f, 1f);
        public Vector3 Center
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var member in members) if (member) { sum += member.transform.position; count++; }
                return count > 0 ? sum / count : Vector3.zero;
            }
        }
        public UtilityScores AverageScores
        {
            get
            {
                var result = new UtilityScores();
                foreach (var member in members)
                {
                    result.Home += member.Scores.Home;
                    result.Study += member.Scores.Study;
                    result.Playground += member.Scores.Playground;
                }
                if (members.Count > 0)
                { result.Home /= members.Count; result.Study /= members.Count; result.Playground /= members.Count; }
                return result;
            }
        }
        internal StudentGroup(int id, StudentAction action) { Id = id; Action = action; }
    }
}
