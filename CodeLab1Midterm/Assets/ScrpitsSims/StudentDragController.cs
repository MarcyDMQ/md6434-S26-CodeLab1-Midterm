using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SocialSim
{
    // One scene-level controller handles selection and placement for all students.
    public class StudentDragController : MonoBehaviour
    {
        public Camera interactionCamera;
        [Min(0)] public float pickPaddingPixels = 16f;
        StudentAgent draggedStudent;
        Vector3 grabOffset;

        public bool TryPickStudent(Camera camera, Vector2 screenPosition, out StudentAgent picked)
        {
            picked = null;
            if (!camera) return false;
            var ray = camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var student = hit.collider.GetComponentInParent<StudentAgent>();
                if (student && student.IsAlive && student.isActiveAndEnabled) { picked = student; return true; }
                if (!hit.collider.isTrigger && !student) break;
            }
            var manager = GetComponent<SocialManager>();
            if (!manager) return false;
            float bestDistance = float.PositiveInfinity, bestCenter = float.PositiveInfinity, bestDepth = float.PositiveInfinity;
            foreach (var student in manager.Students)
            {
                if (!student || !student.IsAlive || !student.isActiveAndEnabled) continue;
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in student.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (!hasBounds) continue;
                var center = camera.WorldToScreenPoint(bounds.center);
                if (center.z <= camera.nearClipPlane) continue;
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var screen = camera.WorldToScreenPoint(point);
                    if (screen.z <= camera.nearClipPlane) continue;
                    min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                }
                if (float.IsInfinity(min.x)) continue;
                var closest = new Vector2(Mathf.Clamp(screenPosition.x, min.x, max.x), Mathf.Clamp(screenPosition.y, min.y, max.y));
                float distance = (screenPosition - closest).sqrMagnitude;
                if (distance > pickPaddingPixels * pickPaddingPixels) continue;
                float centerDistance = (screenPosition - (Vector2)center).sqrMagnitude;
                if (distance > bestDistance || (Mathf.Approximately(distance, bestDistance) &&
                    (centerDistance > bestCenter || (Mathf.Approximately(centerDistance, bestCenter) && center.z >= bestDepth)))) continue;
                if (!VisiblePoint(camera, bounds.center) && !VisiblePoint(camera, bounds.center + Vector3.up * bounds.extents.y * 0.7f)) continue;
                picked = student; bestDistance = distance; bestCenter = centerDistance; bestDepth = center.z;
            }
            return picked;
        }

        static bool VisiblePoint(Camera camera, Vector3 point)
        {
            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(point));
            float distance = Vector3.Distance(ray.origin, point);
            foreach (var hit in Physics.RaycastAll(ray, Mathf.Max(0, distance - 0.05f), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.GetComponentInParent<StudentAgent>()) return false;
            return true;
        }

        static bool TryGetSurfacePoint(Ray ray, out Vector3 point)
        {
            var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<StudentAgent>()) continue;
                point = hit.point; return true;
            }
            point = default; return false;
        }

        void Update()
        {
            if (draggedStudent && (!draggedStudent.IsAlive || !draggedStudent.isActiveAndEnabled)) FinishDrag();
            var mouse = Mouse.current;
            var camera = interactionCamera ? interactionCamera : Camera.main;
            if (mouse == null || !camera || !Application.isFocused) { FinishDrag(); return; }
            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!draggedStudent && mouse.leftButton.wasPressedThisFrame &&
                !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
            {
                if (TryPickStudent(camera, mouse.position.ReadValue(), out var student) && student.BeginDrag())
                {
                    draggedStudent = student;
                    grabOffset = TryGetSurfacePoint(ray, out var ground) ? student.transform.position - ground : Vector3.zero;
                    grabOffset.y = 0;
                }
            }
            if (!draggedStudent) return;
            if (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)
            {
                if (TryGetSurfacePoint(ray, out var point)) draggedStudent.TryDragTo(point + grabOffset);
            }
            if (!mouse.leftButton.isPressed) FinishDrag(mouse.leftButton.wasReleasedThisFrame);
        }

        void FinishDrag(bool tryJoinNearbyGroup = false)
        {
            if (draggedStudent) draggedStudent.EndDrag(tryJoinNearbyGroup);
            draggedStudent = null;
        }
        void OnDisable() => FinishDrag();
        void OnApplicationFocus(bool focused) { if (!focused) FinishDrag(); }
    }
}
