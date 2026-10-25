using System.Collections.Generic;
using UnityEngine;

namespace SocialSim
{
    // Produces a short-lived movement direction, without formation slots or a meeting point.
    public static class GroupSteering
    {
        public static Vector3 Direction(int id, Vector3 position, Vector3 routeDirection,
            IReadOnlyList<Vector3> companions, GroupSettings config)
        {
            routeDirection.y = 0;
            if (routeDirection.sqrMagnitude < 0.001f) return Vector3.zero;
            var forward = routeDirection.normalized;
            Vector3 center = position, separation = Vector3.zero;
            foreach (var other in companions)
            {
                center += other;
                var away = position - other; away.y = 0;
                float distance = away.magnitude;
                if (distance > 0.001f && distance < config.separationDistance)
                    separation += away / distance * (1f - distance / config.separationDistance);
            }
            center /= companions.Count + 1;
            var toCenter = center - position; toCenter.y = 0;
            float pull = Mathf.Clamp01((toCenter.magnitude - config.cohesionDistance * 0.5f) / Mathf.Max(0.1f, config.cohesionDistance));
            var side = Vector3.Cross(Vector3.up, forward);
            // A stable personal preference avoids frame-to-frame random zigzagging.
            float bias = Mathf.Sin(id * 2.399963f);
            // Favor passing on the side over backing up into a queue.
            separation -= forward * Vector3.Dot(separation, forward) * 0.8f;
            var direction = forward + toCenter.normalized * pull * config.cohesionWeight +
                separation * config.separationWeight + side * bias * config.sideBiasWeight;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : forward;
        }
    }
}
