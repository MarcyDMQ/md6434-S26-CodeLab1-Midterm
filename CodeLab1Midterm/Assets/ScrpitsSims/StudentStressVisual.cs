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
        SimulationSettings settings;

        public void Initialize(StudentAgent owner, Transform visualRoot, Material target, SimulationSettings configuration)
        {
            student = owner;
            targetMaterial = target;
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
            if (!student || !targetMaterial || settings == null) return;
            float amount = Mathf.Clamp01(student.Stress / Mathf.Max(1f, settings.stressExplosionThreshold));
            foreach (var binding in bindings)
                for (int i = 0; i < binding.instances.Length; i++)
                    if (binding.instances[i]) binding.instances[i].Lerp(binding.originals[i], targetMaterial, amount);
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
