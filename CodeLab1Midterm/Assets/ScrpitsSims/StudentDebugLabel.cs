using UnityEngine;
using UnityEngine.UI;

namespace SocialSim
{
    public class StudentDebugLabel : MonoBehaviour
    {
        StudentAgent student; Text label; Transform panel; float nextRefresh;
        public void Initialize(StudentAgent agent)
        {
            student = agent;
            var canvasObject = new GameObject("Debug Label", typeof(RectTransform), typeof(Canvas));
            panel = canvasObject.transform; panel.SetParent(transform, false);
            panel.localPosition = Vector3.up * 1.4f; panel.localScale = Vector3.one * 0.008f;
            ((RectTransform)panel).sizeDelta = new Vector2(400, 240);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var textObject = new GameObject("Status", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
        }
        void LateUpdate()
        {
            if (!student || !panel) return;
            var camera = Camera.main;
            if (camera) panel.rotation = camera.transform.rotation;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            var scores = student.Scores;
            string socialState = student.HasSocialSupport ? "Supported" :
                student.IsCrowded ? "Crowded" : "Normal";
            var group = student.Group;
            label.color = group != null ? group.DebugColor : Color.white;
            string groupState = group != null ? $"Group {group.Id} ({group.Members.Count}) Lead {group.Leader?.Id} / {group.Phase}" :
                student.IsHandlingEmergency ? $"Emergency: {student.EmergencyAction}" : "Solo";
            label.text = $"Student {student.Id} | {student.CurrentAction} / {student.Phase}\n" +
                groupState + "\n" +
                $"Academic {student.Academic:F0}   Fatigue {student.Fatigue:F0}\n" +
                $"Stress {student.Stress:F0}\n" +
                $"Neighbors {student.NeighborCount} | {socialState}\n" +
                $"Study {scores.Study:F0}  Home {scores.Home:F0}  Play {scores.Playground:F0}";
        }
    }
}
