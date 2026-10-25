using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace SocialSim
{
    public class ActivityArea : MonoBehaviour
    {
        public StudentAction action;
        public Transform target;
        [Min(0.1f)] public float arrivalDistance = 2f;
        [FormerlySerializedAs("groupSurfaceHints")] public Collider[] activitySurfaces;
        public bool requireSurfaceContact;
        [Min(0.01f)] public float surfaceHeightTolerance = 0.12f;
        [Min(0.01f)] public float surfaceGoalInset = 0.15f;
        public Vector3 Center => target ? target.position : transform.position;

        public bool IsReached(Vector3 position)
        {
            if (requireSurfaceContact) return IsOnActivitySurface(position);
            if (activitySurfaces != null)
                foreach (var surface in activitySurfaces)
                    if (surface && surface.enabled && surface.gameObject.activeInHierarchy &&
                        surface.bounds.SqrDistance(position) <= arrivalDistance * arrivalDistance) return true;
            return Vector3.Distance(position, Center) <= arrivalDistance;
        }

        bool IsOnActivitySurface(Vector3 position)
        {
            if (activitySurfaces == null) return false;
            float highest = float.NegativeInfinity;
            foreach (var surface in activitySurfaces)
            {
                if (!surface || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                var bounds = surface.bounds;
                var ray = new Ray(new Vector3(position.x, bounds.max.y + 0.5f, position.z), Vector3.down);
                if (surface.Raycast(ray, out var hit, bounds.size.y + 1f)) highest = Mathf.Max(highest, hit.point.y);
            }
            return !float.IsNegativeInfinity(highest) && Mathf.Abs(position.y - highest) <= surfaceHeightTolerance;
        }

        bool IsSafeSurfaceGoal(Vector3 position)
        {
            return IsOnActivitySurface(position) && IsOnActivitySurface(position + Vector3.right * surfaceGoalInset) &&
                IsOnActivitySurface(position - Vector3.right * surfaceGoalInset) &&
                IsOnActivitySurface(position + Vector3.forward * surfaceGoalInset) &&
                IsOnActivitySurface(position - Vector3.forward * surfaceGoalInset);
        }

        // These are temporary navigation goals, never assigned standing positions.
        public bool TryGetDestination(NavMeshAgent mover, out Vector3 destination, IReadOnlyList<StudentAgent> companions = null)
        {
            destination = default;
            var path = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = mover.agentTypeID, areaMask = mover.areaMask };
            int attempts = companions == null ? 6 : 12;
            int surfaceOffset = activitySurfaces != null && activitySurfaces.Length > 0 ? Random.Range(0, activitySurfaces.Length) : 0;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 seed = Center;
                bool sampledSurface = false;
                if (activitySurfaces != null && activitySurfaces.Length > 0)
                {
                    var surface = activitySurfaces[(surfaceOffset + attempt) % activitySurfaces.Length];
                    if (surface && surface.enabled && surface.gameObject.activeInHierarchy)
                    {
                        var bounds = surface.bounds;
                        var horizontal = attempt == attempts - 1 ? Vector2.zero : new Vector2(
                            Random.Range(-bounds.extents.x, bounds.extents.x), Random.Range(-bounds.extents.z, bounds.extents.z)) * 0.8f;
                        var ray = new Ray(new Vector3(bounds.center.x + horizontal.x, bounds.max.y + 1f, bounds.center.z + horizontal.y), Vector3.down);
                        if (surface.Raycast(ray, out var surfaceHit, bounds.size.y + 2f))
                        { seed = surfaceHit.point; sampledSurface = true; }
                    }
                }
                if (requireSurfaceContact && !sampledSurface) continue;
                var offset = requireSurfaceContact || attempt == attempts - 1 ? Vector2.zero : Random.insideUnitCircle * arrivalDistance * 0.8f;
                seed += new Vector3(offset.x, 0, offset.y);
                float sampleRadius = requireSurfaceContact ? 0.25f : attempt == attempts - 1 ? arrivalDistance : 0.5f;
                if (!NavMesh.SamplePosition(seed, out var hit, sampleRadius, filter) || !IsReached(hit.position)) continue;
                if (requireSurfaceContact && !IsSafeSurfaceGoal(hit.position)) continue;
                bool occupied = false;
                if (companions != null)
                    foreach (var other in companions)
                        if (other && other.Navigation != mover && Vector3.Distance(hit.position, other.transform.position) < mover.radius + other.Navigation.radius + 0.1f)
                        { occupied = true; break; }
                if (occupied) continue;
                if (!NavMesh.CalculatePath(mover.transform.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                destination = hit.position;
                return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = action == StudentAction.Study ? Color.blue : action == StudentAction.Home ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(Center, arrivalDistance);
            if (activitySurfaces != null)
                foreach (var surface in activitySurfaces) if (surface)
                    Gizmos.DrawWireCube(surface.bounds.center, surface.bounds.size + (requireSurfaceContact ? Vector3.zero : Vector3.one * arrivalDistance * 2f));
        }
    }
}
