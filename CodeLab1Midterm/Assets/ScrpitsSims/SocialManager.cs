using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SocialSim
{
    [Serializable]
    public class SimulationSettings
    {
        [Min(0)] public float academicDecay = 0.5f;
        [Min(0)] public float studyGain = 6f;
        [Min(0)] public float studyFatigue = 4f;
        [Min(0)] public float restRecovery = 8f;
        [Range(0, 100)] public float stressAcademicThreshold = 50f;
        [Min(0)] public float stressGain = 2f;
        [Min(0)] public float playgroundStressRecovery = 8f;
        [Min(0)] public float neighborRadius = 3f;
        [Min(0)] public int supportMinNeighbors = 2;
        [Min(0)] public int supportMaxNeighbors = 4;
        [Range(0, 1)] public float supportedStressGainMultiplier = 0.5f;
        [Min(0)] public float supportedRecoveryMultiplier = 1.5f;
        [Min(0)] public float crowdStressPerNeighbor = 1f;
        [Range(1, 100)] public float stressExplosionThreshold = 90f;
        [Min(0)] public float explosionGracePeriod = 15f;
        [Min(0.1f)] public float explosionLifetime = 5f;
        [Min(0.1f)] public float stayDuration = 2f;
        [Min(0.1f)] public float moveSpeed = 2.5f;
        [Min(0.05f)] public float agentRadius = 0.3f;
        [Min(0.1f)] public float agentHeight = 1f;
        [Min(0.01f)] public float stoppingDistance = 0.35f;
    }

    public class SocialManager : MonoBehaviour
    {
        [Min(1)] public int agentCount = 5;
        public GameObject visualPrefab;
        public RuntimeAnimatorController studentAnimator;
        public Material stressMaterial;
        public GameObject explodePrefab;
        public ActivityArea[] areas;
        public Transform[] spawnPoints;
        [Min(0.1f)] public float spawnSpacing = 0.8f;
        public NavMeshSurface navigationSurface;
        public SimulationSettings settings = new SimulationSettings();
        public bool showDebugLabels = true;
        public bool enableMouseDragging = true;
        [Min(0)] public float mousePickPaddingPixels = 16f;
        public bool enableGroups = true;
        public GroupSettings groupSettings = new GroupSettings();
        public StudentGroupCoordinator GroupCoordinator { get; private set; }
        readonly List<StudentAgent> students = new List<StudentAgent>();
        public IReadOnlyList<StudentAgent> Students => students;
        public float SimulationStartedAt { get; private set; }
        public bool IsExplosionProtected => Time.time - SimulationStartedAt < settings.explosionGracePeriod;

        public int CountNeighbors(StudentAgent student)
        {
            if (!student || !student.IsAlive || !student.isActiveAndEnabled) return 0;
            float radiusSquared = settings.neighborRadius * settings.neighborRadius;
            int count = 0;
            foreach (var other in students)
                if (other && other != student && other.IsAlive && other.isActiveAndEnabled &&
                    (other.transform.position - student.transform.position).sqrMagnitude <= radiusSquared)
                    count++;
            return count;
        }

        public void UnregisterStudent(StudentAgent student)
        {
            if (GroupCoordinator) GroupCoordinator.RemoveMember(student);
            students.Remove(student);
        }

        void Start()
        {
            SimulationStartedAt = Time.time;
            var uniqueSpawnPoints = new List<Transform>();
            if (spawnPoints != null)
                foreach (var point in spawnPoints)
                    if (point && !uniqueSpawnPoints.Contains(point)) uniqueSpawnPoints.Add(point);
            if (!visualPrefab || !navigationSurface || areas == null || areas.Length == 0 ||
                uniqueSpawnPoints.Count == 0)
            {
                Debug.LogError("SocialSim: assign prefab, navigation surface, areas and at least one valid spawn point.", this);
                enabled = false;
                return;
            }
            // Build before spawning so moving students never enter the bake.
            navigationSurface.BuildNavMesh();
            if (enableMouseDragging)
            {
                var dragController = GetComponent<StudentDragController>();
                if (!dragController) dragController = gameObject.AddComponent<StudentDragController>();
                dragController.pickPaddingPixels = mousePickPaddingPixels;
            }
            var placedPositions = new List<Vector3>();
            for (int i = 0; i < agentCount; i++)
            {
                int spawnIndex = i % uniqueSpawnPoints.Count;
                if (!TryFindSpawnPosition(uniqueSpawnPoints[spawnIndex].position, spawnIndex, placedPositions, out var position))
                {
                    Debug.LogError($"SocialSim: no free NavMesh position near spawn point {spawnIndex + 1} for student {i + 1}.", this);
                    continue;
                }
                placedPositions.Add(position);
                var root = new GameObject($"Student {i + 1}");
                root.transform.SetParent(transform);
                root.transform.position = position;
                var mover = root.AddComponent<NavMeshAgent>();
                mover.agentTypeID = navigationSurface.agentTypeID;
                mover.speed = settings.moveSpeed;
                mover.radius = settings.agentRadius;
                mover.height = settings.agentHeight;
                mover.stoppingDistance = settings.stoppingDistance;
                mover.acceleration = 12f;
                mover.angularSpeed = 360f;
                mover.avoidancePriority = 40 + i % 20;
                var visual = Instantiate(visualPrefab, root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>())
                    if (script && (script.GetType().Name == "BulletReverse" || script.GetType().Name == "CharacterController"))
                        script.enabled = false;
                foreach (var body in visual.GetComponentsInChildren<Rigidbody>())
                { body.isKinematic = true; body.useGravity = false; }
                foreach (var otherMover in visual.GetComponentsInChildren<NavMeshAgent>()) otherMover.enabled = false;
                var animator = visual.GetComponentInChildren<Animator>();
                if (animator)
                {
                    animator.applyRootMotion = false;
                    if (studentAnimator) animator.runtimeAnimatorController = studentAnimator;
                }
                var student = root.AddComponent<StudentAgent>();
                student.Initialize(i + 1, settings, areas, animator, this, spawnIndex);
                students.Add(student);
                if (stressMaterial) root.AddComponent<StudentStressVisual>().Initialize(student, visual.transform, stressMaterial, settings);
                if (showDebugLabels) root.AddComponent<StudentDebugLabel>().Initialize(student);
            }
            GroupCoordinator = GetComponent<StudentGroupCoordinator>();
            if (!GroupCoordinator) GroupCoordinator = gameObject.AddComponent<StudentGroupCoordinator>();
            GroupCoordinator.Initialize(this);
        }

        bool TryFindSpawnPosition(Vector3 origin, int spawnIndex, List<Vector3> occupied, out Vector3 position)
        {
            position = default;
            var filter = new NavMeshQueryFilter { agentTypeID = navigationSurface.agentTypeID, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(origin, out var center, 1f, filter)) return false;
            float spacing = Mathf.Max(spawnSpacing, settings.agentRadius * 2f + 0.1f);
            var path = new NavMeshPath();
            for (int attempt = 0; attempt < 512; attempt++)
            {
                float angle = attempt * 2.399963f + spawnIndex * 1.256637f;
                float radius = spacing * Mathf.Sqrt(attempt);
                var candidate = center.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.3f, filter)) continue;
                bool overlaps = false;
                foreach (var other in occupied)
                    if ((hit.position - other).sqrMagnitude < spacing * spacing) { overlaps = true; break; }
                if (overlaps) continue;
                if (!NavMesh.CalculatePath(center.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                position = hit.position;
                return true;
            }
            return false;
        }
    }
}
