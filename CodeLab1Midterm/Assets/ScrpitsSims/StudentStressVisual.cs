using System.Collections.Generic;
using UnityEngine;

namespace SocialSim
{
    public class StudentStressVisual : MonoBehaviour
    {
        sealed class RendererMaterials
        {
            public Renderer renderer;
            public Material[] originals;
            public Material[] instances;
        }

        readonly List<RendererMaterials> bindings = new List<RendererMaterials>();
        StudentAgent student;
        Material targetMaterial;
        Material fatigueMaterial;
        SimulationSettings settings;

        public void Initialize(StudentAgent owner, Transform visualRoot, Material target, SimulationSettings configuration, Material fatigueTarget = null)
        {
            student = owner;
            targetMaterial = target;
            fatigueMaterial = fatigueTarget;
            settings = configuration;
            // Restrict this to the visual prefab, so world-space labels stay unchanged.
            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var instances = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                    if (originals[i]) instances[i] = new Material(originals[i]);
                renderer.sharedMaterials = instances;
                bindings.Add(new RendererMaterials { renderer = renderer, originals = originals, instances = instances });
            }
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (!student || settings == null) return;
            float amount = Mathf.Clamp01(student.Stress / Mathf.Max(1f, settings.stressExplosionThreshold));
            float threshold = Mathf.Clamp(settings.fatigueTintThreshold, 0f, 99f);
            float fatigue = Mathf.InverseLerp(threshold, 100f, student.Fatigue);
            foreach (var binding in bindings)
                for (int i = 0; i < binding.instances.Length; i++)
                {
                    var material = binding.instances[i];
                    if (!material) continue;
                    // Start from the original every frame so resting reverses the tint without accumulated drift.
                    if (fatigueMaterial) material.Lerp(binding.originals[i], fatigueMaterial, fatigue);
                    else material.CopyPropertiesFromMaterial(binding.originals[i]);
                    // Stress overlays the fatigue tint; maximum stress remains clearly visible.
                    if (targetMaterial && amount > 0) material.Lerp(material, targetMaterial, amount);
                }
        }

        void OnDestroy()
        {
            foreach (var binding in bindings)
            {
                if (binding.renderer) binding.renderer.sharedMaterials = binding.originals;
                foreach (var instance in binding.instances) if (instance) Destroy(instance);
            }
            bindings.Clear();
        }
    }
}
