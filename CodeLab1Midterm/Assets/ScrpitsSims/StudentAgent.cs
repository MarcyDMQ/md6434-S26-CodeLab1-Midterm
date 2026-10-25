using UnityEngine;
using UnityEngine.AI;

namespace SocialSim
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class StudentAgent : MonoBehaviour
    {
        public int Id { get; private set; }
        public int SpawnPointIndex { get; private set; }
        public Vector3 InitialPosition { get; private set; }
        public float Academic => academic;
        public float Fatigue => fatigue;
        public float Stress => stress;
        public int NeighborCount { get; private set; }
        public bool HasSocialSupport => settings != null && UtilityScorer.HasSocialSupport(NeighborCount, settings);
        public bool IsCrowded => settings != null && NeighborCount > settings.supportMaxNeighbors;
        public bool IsAlive => initialized && !dead;
        public StudentGroup Group { get; private set; }
        public int GroupId => Group != null ? Group.Id : 0;
        public bool IsInGroup => Group != null;
        public bool IsHandlingEmergency => stressEmergency || fatigueEmergency;
        public StudentAction EmergencyAction => stressEmergency ? StudentAction.Playground : StudentAction.Home;
        public StudentAction PreferredAction => UtilityScorer.PreferredAction(Scores, hasAction ? CurrentAction : (StudentAction?)null);
        public bool CanJoinGroup => IsAlive && isActiveAndEnabled && mover && mover.isOnNavMesh && !IsInGroup &&
            !IsHandlingEmergency && Time.time >= groupCooldownUntil && Phase != StudentPhase.Dragging && Phase != StudentPhase.Acting;
        public bool HasArrivedWithGroup { get; private set; }
        internal NavMeshAgent Navigation => mover;
        internal bool GroupPaused { get; private set; }
        public float GroupSpeedMultiplier { get; private set; } = 1f;
        float groupCruiseSpeed, groupAppliedSpeed;
        internal float GroupCruiseSpeed
        {
            get
            {
                if (Mathf.Abs(mover.speed - groupAppliedSpeed) > 0.01f) groupCruiseSpeed = mover.speed;
                return groupCruiseSpeed;
            }
        }
        public StudentAction CurrentAction { get; private set; }
        public StudentPhase Phase { get; private set; }
        public UtilityScores Scores { get; private set; }
        [SerializeField, Range(0, 100)] float academic;
        [SerializeField, Range(0, 100)] float fatigue;
        [SerializeField, Range(0, 100)] float stress;
        SimulationSettings settings;
        ActivityArea[] areas;
        NavMeshAgent mover;
        Animator animator;
        SocialManager manager;
        ActivityArea currentArea;
        ActivityArea failedArea;
        float explorationSeconds, phaseTime, failedUntil, stalledTime;
        Vector3 progressPosition;
        bool initialized, hasAction, hasSpeedParameter;
        bool dead;
        float initializedAt;
        bool ShouldExplode => stress >= settings.stressExplosionThreshold &&
            (manager ? !manager.IsExplosionProtected : Time.time - initializedAt >= settings.explosionGracePeriod);
        bool stressEmergency, fatigueEmergency;
        float groupCooldownUntil;
        public void Initialize(int id, SimulationSettings configuration, ActivityArea[] activityAreas, Animator visualAnimator,
            SocialManager owner = null, int spawnPointIndex = -1)
        {
            manager = owner;
            initializedAt = Time.time;
            SpawnPointIndex = spawnPointIndex;
            InitialPosition = transform.position;
            Id = id; settings = configuration; areas = activityAreas; animator = visualAnimator;
            mover = GetComponent<NavMeshAgent>();
            groupCruiseSpeed = groupAppliedSpeed = mover.speed;
            academic = Random.Range(35f, 85f); fatigue = Random.Range(10f, 70f);
            stress = 0f;
            explorationSeconds = Random.Range(0f, 20f);
            if (animator && animator.runtimeAnimatorController)
                foreach (var parameter in animator.parameters)
                    if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) hasSpeedParameter = true;
            Phase = StudentPhase.Deciding; initialized = true;
        }
        void Update()
        {
            if (!IsAlive) return;
            if (ShouldExplode) { Explode(); return; }
            UpdateEmergency();
            NeighborCount = manager ? manager.CountNeighbors(this) : 0;
            float dt = Time.deltaTime;
            // Do not apply action effects beyond the remaining stay time on a long frame.
            float remaining = Group != null ? Group.ActionEndsAt - Time.time + dt : settings.stayDuration - phaseTime;
            float actingTime = Phase == StudentPhase.Acting ? Mathf.Min(dt, Mathf.Max(0f, remaining)) : 0f;
            UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, actingTime, CurrentAction, true, settings, NeighborCount);
            UtilityScorer.UpdateNeeds(ref academic, ref fatigue, ref stress, dt - actingTime, CurrentAction, false, settings, NeighborCount);
            if (ShouldExplode) { Explode(); return; }
            explorationSeconds += dt;
            Scores = UtilityScorer.Evaluate(academic, fatigue, stress, explorationSeconds);
            UpdateEmergency();
            if (animator && hasSpeedParameter) animator.SetFloat("Speed", mover.velocity.magnitude);
            if (Phase == StudentPhase.Dragging) return;
            if (!mover.isOnNavMesh) { LeaveGroup(); Release(); Phase = StudentPhase.Waiting; return; }
            if (IsInGroup) { UpdateGroupMovement(dt); return; }
            switch (Phase)
            {
                case StudentPhase.Deciding: Decide(); break;
                case StudentPhase.Waiting:
                    phaseTime += dt;
                    if (phaseTime >= 1f) { phaseTime = 0; Phase = StudentPhase.Deciding; }
                    break;
                case StudentPhase.Moving:
                    if (!mover.pathPending && mover.pathStatus != NavMeshPathStatus.PathComplete) { FailTarget(); break; }
                    if (!mover.pathPending && currentArea && currentArea.IsReached(transform.position))
                    { mover.isStopped = true; phaseTime = 0; Phase = StudentPhase.Acting; break; }
                    if ((transform.position - progressPosition).sqrMagnitude >= 0.01f)
                    { progressPosition = transform.position; stalledTime = 0; }
                    else stalledTime += dt;
                    if (stalledTime >= 5f) FailTarget();
                    break;
                case StudentPhase.Acting:
                    phaseTime += dt;
                    if (phaseTime >= settings.stayDuration)
                    {
                        if (CurrentAction == StudentAction.Playground) explorationSeconds = 0;
                        Scores = UtilityScorer.Evaluate(academic, fatigue, stress, explorationSeconds);
                        Phase = StudentPhase.Deciding;
                    }
                    break;
            }
        }
        public bool BeginDrag()
        {
            if (!IsAlive || !isActiveAndEnabled || !mover.isOnNavMesh) return false;
            LeaveGroup();
            Release();
            mover.isStopped = true;
            mover.ResetPath();
            phaseTime = 0;
            Phase = StudentPhase.Dragging;
            return true;
        }

        public bool TryDragTo(Vector3 surfacePosition)
        {
            if (!IsAlive || Phase != StudentPhase.Dragging || !mover.enabled) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = mover.agentTypeID, areaMask = mover.areaMask };
            if (!NavMesh.SamplePosition(surfacePosition, out var hit, 0.4f, filter)) return false;
            return mover.Warp(hit.position);
        }

        public void EndDrag(bool tryJoinNearbyGroup = true)
        {
            if (!IsAlive || Phase != StudentPhase.Dragging) return;
            failedArea = null;
            failedUntil = 0;
            stalledTime = 0;
            phaseTime = 0;
            Phase = StudentPhase.Deciding;
            if (!tryJoinNearbyGroup || !manager || !manager.GroupCoordinator || !manager.GroupCoordinator.TryJoinDroppedStudent(this))
                StartGroupCooldown();
        }

        void StartGroupCooldown()
        {
            if (manager) groupCooldownUntil = Time.time + manager.groupSettings.rejoinCooldown;
        }

        void UpdateEmergency()
        {
            if (!manager) return;
            var config = manager.groupSettings;
            bool wasEmergency = IsHandlingEmergency;
            StudentAction oldAction = EmergencyAction;
            if (stress >= config.emergencyStress) stressEmergency = true;
            else if (stress <= config.emergencyRelease) stressEmergency = false;
            if (fatigue >= config.emergencyFatigue) fatigueEmergency = true;
            else if (fatigue <= config.emergencyRelease) fatigueEmergency = false;
            bool changed = wasEmergency != IsHandlingEmergency || (IsHandlingEmergency && oldAction != EmergencyAction);
            if (IsHandlingEmergency && IsInGroup) LeaveGroup();
            if (wasEmergency && !IsHandlingEmergency) StartGroupCooldown();
            if (changed && Phase != StudentPhase.Dragging)
            {
                if (mover && mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
                Release(); phaseTime = 0; Phase = StudentPhase.Deciding;
            }
        }

        public void LeaveGroup()
        {
            if (manager && manager.GroupCoordinator) manager.GroupCoordinator.RemoveMember(this);
        }

        internal void JoinGroup(StudentGroup group)
        {
            Group = group;
            CurrentAction = group.Action; hasAction = true;
            HasArrivedWithGroup = false;
            if (mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
            Phase = StudentPhase.Gathering;
        }

        internal void ClearGroup()
        {
            RestoreGroupSpeed();
            Group = null; GroupPaused = false; HasArrivedWithGroup = false;
            StartGroupCooldown();
            if (mover && mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
            Release(); phaseTime = 0; stalledTime = 0;
            if (IsAlive && Phase != StudentPhase.Dragging) Phase = StudentPhase.Deciding;
        }

        internal void PrepareGroupTravel(ActivityArea area)
        {
            RestoreGroupSpeed();
            currentArea = area; CurrentAction = area.action; hasAction = true;
            HasArrivedWithGroup = false; GroupPaused = true;
            phaseTime = 0; stalledTime = 0; progressPosition = transform.position;
            if (mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
            Phase = StudentPhase.Moving;
        }

        internal bool NavigateWithGroup(ActivityArea area, Vector3 destination, float stoppingDistance)
        {
            if (!IsAlive || !mover.isOnNavMesh) return false;
            currentArea = area; CurrentAction = area.action; hasAction = true;
            if (Phase != StudentPhase.Moving) { stalledTime = 0; progressPosition = transform.position; }
            Phase = StudentPhase.Moving; GroupPaused = false;
            mover.stoppingDistance = stoppingDistance;
            mover.isStopped = false;
            if (!mover.hasPath || Vector3.Distance(mover.destination, destination) > 0.2f)
                return mover.SetDestination(destination);
            return true;
        }

        internal void MarkGroupArrival()
        {
            RestoreGroupSpeed();
            HasArrivedWithGroup = true; GroupPaused = false;
            mover.ResetPath(); mover.isStopped = true;
            Phase = StudentPhase.Gathering;
        }

        internal void ApplyGroupSpeed(float multiplier)
        {
            float cruise = GroupCruiseSpeed;
            float target = cruise * Mathf.Clamp(multiplier, manager.groupSettings.minimumSpeedMultiplier, 1f);
            float step = manager.groupSettings.speedChangeRate * Mathf.Max(Time.deltaTime, manager.groupSettings.cohesionCheckInterval);
            groupAppliedSpeed = Mathf.MoveTowards(mover.speed, target, step);
            mover.speed = groupAppliedSpeed;
            GroupSpeedMultiplier = cruise > 0.001f ? groupAppliedSpeed / cruise : 1f;
        }

        void RestoreGroupSpeed()
        {
            if (!mover) return;
            groupAppliedSpeed = GroupCruiseSpeed;
            mover.speed = groupAppliedSpeed;
            GroupSpeedMultiplier = 1f;
        }

        internal void SetGroupPaused(bool paused)
        {
            GroupPaused = paused;
            if (Phase == StudentPhase.Moving && mover.isOnNavMesh) mover.isStopped = paused;
            if (paused) { stalledTime = 0; progressPosition = transform.position; }
        }

        void UpdateGroupMovement(float dt)
        {
            if (Phase != StudentPhase.Moving || GroupPaused) return;
            if (!mover.pathPending && mover.pathStatus != NavMeshPathStatus.PathComplete) { FailTarget(); return; }
            if ((transform.position - progressPosition).sqrMagnitude >= 0.01f)
            { progressPosition = transform.position; stalledTime = 0; }
            else stalledTime += dt;
            if (stalledTime >= manager.groupSettings.stalledTimeout) FailTarget();
        }

        internal void BeginGroupAction(StudentAction action)
        {
            CurrentAction = action; hasAction = true; phaseTime = 0;
            mover.isStopped = true; Phase = StudentPhase.Acting;
        }

        internal void CompleteGroupAction()
        {
            if (CurrentAction == StudentAction.Playground) explorationSeconds = 0;
            Scores = UtilityScorer.Evaluate(academic, fatigue, stress, explorationSeconds);
            Phase = StudentPhase.Gathering;
        }

        void Explode()
        {
            if (dead) return;
            dead = true;
            Phase = StudentPhase.Dead;
            Release();
            if (mover && mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
            if (manager)
            {
                manager.UnregisterStudent(this);
                if (manager.explodePrefab)
                {
                    var effect = Instantiate(manager.explodePrefab, transform.position, Quaternion.identity);
                    Destroy(effect, Mathf.Max(0.1f, settings.explosionLifetime));
                }
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (manager) manager.UnregisterStudent(this);
        }

        void Decide()
        {
            ActivityArea bestArea = null;
            Vector3 bestDestination = default; float bestScore = float.NegativeInfinity;
            // Stable order is Home, Study, Playground; current action wins ties.
            for (int order = 0; order < 3; order++)
            foreach (var area in areas)
            {
                if (!area || !area.isActiveAndEnabled || (int)area.action != order || (area == failedArea && Time.time < failedUntil)) continue;
                if (IsHandlingEmergency && area.action != EmergencyAction) continue;
                float score = Scores.Get(area.action);
                bool tiePreferred = hasAction && area.action == CurrentAction && (!bestArea || bestArea.action != CurrentAction);
                if (score < bestScore || (score == bestScore && !tiePreferred)) continue;
                Vector3 destination;
                if (area.IsReached(transform.position)) destination = transform.position;
                else if (!area.TryGetDestination(mover, out destination)) continue;
                bestArea = area; bestDestination = destination; bestScore = score;
            }
            if (!bestArea) { Release(); mover.ResetPath(); phaseTime = 0; Phase = StudentPhase.Waiting; return; }
            currentArea = bestArea;
            CurrentAction = bestArea.action; hasAction = true;
            if (bestArea.IsReached(transform.position))
            { mover.isStopped = true; mover.ResetPath(); phaseTime = 0; Phase = StudentPhase.Acting; return; }
            CurrentAction = bestArea.action; hasAction = true; mover.isStopped = false;
            mover.stoppingDistance = bestArea.requireSurfaceContact ? 0.05f : Mathf.Min(settings.stoppingDistance, bestArea.arrivalDistance * 0.1f);
            if (!mover.SetDestination(bestDestination)) { FailTarget(); return; }
            progressPosition = transform.position; stalledTime = 0; Phase = StudentPhase.Moving;
        }
        void FailTarget()
        {
            var failed = currentArea;
            LeaveGroup();
            failedArea = failed; failedUntil = Time.time + 5f;
            Release(); mover.ResetPath(); mover.isStopped = true; Phase = StudentPhase.Deciding;
        }
        void Release()
        {
            currentArea = null;
        }
        void OnDisable()
        {
            LeaveGroup();
            Release();
            if (mover && mover.isOnNavMesh) { mover.ResetPath(); mover.isStopped = true; }
            if (!dead) Phase = StudentPhase.Deciding;
        }
    }
}
