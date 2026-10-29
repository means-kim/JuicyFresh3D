using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BloodSprayPool_s : MonoBehaviour
{
    private const int MaxEffects = 32;
    private const float MaxLifetime = 8f;
    private static BloodSprayPool_s _instance;
    private readonly List<Effect_s> _effects = new List<Effect_s>();

    private sealed class Effect_s
    {
        public GameObject Prefab;
        public GameObject Object;
        public ParticleSystem[] Particles;
        public bool Active;
        public bool Emitting;
        public float StopAt;
        public float RecycleAt;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        _instance = null;
    }

    public static void Show(GameObject prefab, Vector3 point, Quaternion rotation,
        float size, float emissionDuration, Scene scene)
    {
        if (prefab == null || !scene.IsValid() || !scene.isLoaded) return;
        if (_instance == null)
        {
            var root = new GameObject("Blood Spray Pool");
            SceneManager.MoveGameObjectToScene(root, scene);
            _instance = root.AddComponent<BloodSprayPool_s>();
        }
        _instance.Play(prefab, point, rotation, size, emissionDuration);
    }

    private void Play(GameObject prefab, Vector3 point, Quaternion rotation, float size, float emissionDuration)
    {
        Effect_s effect = null;
        foreach (var candidate in _effects)
        {
            if (!candidate.Active && candidate.Prefab == prefab)
            {
                effect = candidate;
                break;
            }
        }
        if (effect == null)
        {
            // 포화 시 이전 혈흔을 끊지 않고 새 효과만 생략한다.
            if (_effects.Count >= MaxEffects) return;
            var instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            effect = new Effect_s
            {
                Prefab = prefab,
                Object = instance,
                Particles = instance.GetComponentsInChildren<ParticleSystem>(true)
            };
            foreach (var particles in effect.Particles)
            {
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.useUnscaledTime = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            _effects.Add(effect);
        }
        effect.Object.transform.SetPositionAndRotation(point, rotation);
        effect.Object.transform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, size);
        effect.Object.SetActive(true);
        foreach (var particles in effect.Particles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(false);
        }
        effect.Active = true;
        effect.Emitting = true;
        effect.StopAt = Time.time + Mathf.Clamp(emissionDuration, 0.01f, MaxLifetime - 1f);
        effect.RecycleAt = Time.time + MaxLifetime;
    }

    private void Update()
    {
        foreach (var effect in _effects)
        {
            if (!effect.Active) continue;
            if (effect.Emitting && Time.time >= effect.StopAt)
            {
                foreach (var particles in effect.Particles)
                {
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                effect.Emitting = false;
            }
            if (effect.Emitting) continue;
            bool alive = false;
            foreach (var particles in effect.Particles)
            {
                if (particles.IsAlive(false)) alive = true;
            }
            if (!alive || Time.time >= effect.RecycleAt) Recycle(effect);
        }
    }

    private void Recycle(Effect_s effect)
    {
        foreach (var particles in effect.Particles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        effect.Object.SetActive(false);
        effect.Active = false;
        effect.Emitting = false;
    }

    private void OnDisable()
    {
        foreach (var effect in _effects) Recycle(effect);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
