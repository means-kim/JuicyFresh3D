using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class OutlineParticleLightEffect_m : MonoBehaviour
{
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    [Header("Model")]
    [Tooltip("Particles are emitted from these animated mesh surfaces and travel along their outward normals.")]
    [SerializeField] private SkinnedMeshRenderer[] _modelRenderers;

    [Header("Particle Source")]
    [Tooltip("Particle System prefab used for the light dust rendering setup.")]
    [SerializeField] private ParticleSystem _particlePrefab;
    [Tooltip("Fallback emission anchor used only when no valid model renderer is assigned.")]
    [SerializeField] private Transform _particleOrigin;

    [Header("Light Dust")]
    [Tooltip("Time over which the configured particle count is emitted continuously.")]
    [SerializeField, Min(0.01f)] private float _emissionDuration = 1f;
    [SerializeField, Min(1)] private int _particleCount = 24;
    [SerializeField] private Vector2 _particleSizeRange = new Vector2(0.025f, 0.065f);
    [SerializeField] private Vector2 _particleSpeedRange = new Vector2(0.25f, 0.65f);
    [SerializeField] private Vector2 _particleLifetimeRange = new Vector2(0.65f, 1.1f);

    [Tooltip("Fallback box size used only when no valid model renderer is assigned.")]
    [SerializeField] private Vector3 _spawnArea = new Vector3(0.75f, 0.9f, 0.5f);
    [SerializeField, Min(0f)] private float _gravityModifier = 0.1f;
    [SerializeField, Min(0f)] private float _noiseStrength = 0.08f;
    [SerializeField, Min(0f)] private float _particleBrightness = 2.5f;

    private sealed class ParticleEmitter
    {
        public SkinnedMeshRenderer Source;
        public ParticleSystem Particles;
        public ParticleSystemRenderer Renderer;
        public MaterialPropertyBlock Properties;
        public int EmittedCount;
    }

    private readonly List<ParticleEmitter> _emitters = new List<ParticleEmitter>();
    private Color _effectColor = Color.white;
    private float _elapsedTime;
    private float _opacity;
    private int _emittedParticleCount;
    private bool _isPlaying;

    internal void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (!EnsureParticleSystems()) return;

        foreach (ParticleEmitter emitter in _emitters)
        {
            emitter.Particles.gameObject.SetActive(true);
            emitter.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ConfigureParticleSystem(emitter);
            UpdateEmitterTransform(emitter);

            emitter.EmittedCount = 0;
            emitter.Particles.Play(true);
        }

        _elapsedTime = 0f;
        _emittedParticleCount = 0;
        _opacity = 1f;
        _isPlaying = true;

        EmitUntilCount(1);
        ApplyParticleProperties();
    }

    internal void StopEffect()
    {
        HideParticles();
    }

    internal void SetEffectColor(Color color)
    {
        bool colorChanged = _effectColor != color;
        _effectColor = color;

        if (colorChanged && _emitters.Count > 0)
        {
            ApplyParticleProperties();
        }
    }

    private void LateUpdate()
    {
        if (!_isPlaying || _emitters.Count == 0) return;

        foreach (ParticleEmitter emitter in _emitters)
        {
            UpdateEmitterTransform(emitter);
        }

        _elapsedTime += Time.deltaTime;

        float emissionDuration = Mathf.Max(0.01f, _emissionDuration);
        float progress = Mathf.Clamp01(_elapsedTime / emissionDuration);
        EmitUntilCount(Mathf.CeilToInt(progress * Mathf.Max(1, _particleCount)));

        if (_elapsedTime >= emissionDuration && !AnyParticlesAlive())
        {
            HideParticles();
        }
    }

    private bool EnsureParticleSystems()
    {
        if (_emitters.Count > 0) return true;

        if (_particlePrefab == null)
        {
            Debug.LogWarning("Assign a Particle System prefab before playing the light dust effect.", this);
            return false;
        }

        bool hasModelRenderer = false;
        if (_modelRenderers != null)
        {
            foreach (SkinnedMeshRenderer modelRenderer in _modelRenderers)
            {
                if (modelRenderer == null || modelRenderer.sharedMesh == null) continue;

                hasModelRenderer = true;

                if (!TryCreateEmitter(modelRenderer))
                {
                    ClearRuntimeParticles();
                    return false;
                }
            }
        }

        if (!hasModelRenderer && !TryCreateEmitter(null))
        {
            ClearRuntimeParticles();
            return false;
        }

        return _emitters.Count > 0;
    }

    private bool TryCreateEmitter(SkinnedMeshRenderer source)
    {
        ParticleSystem particles = Instantiate(_particlePrefab);

        particles.name = source != null ? $"{name} {source.name} Light Dust (Runtime)" : $"{name} Light Dust (Runtime)";

        particles.gameObject.hideFlags = HideFlags.DontSave;
        particles.transform.SetParent(null, false);

        SceneManager.MoveGameObjectToScene(particles.gameObject, gameObject.scene);
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();

        if (particleRenderer == null || particleRenderer.sharedMaterial == null)
        {
            Debug.LogWarning("The light dust prefab needs a Particle System Renderer material.", this);
            Destroy(particles.gameObject);
            return false;
        }

        _emitters.Add(new ParticleEmitter
        {
            Source = source,
            Particles = particles,
            Renderer = particleRenderer,
            Properties = new MaterialPropertyBlock()
        });

        return true;
    }

    private void ConfigureParticleSystem(ParticleEmitter emitter)
    {
        GetOrderedRange(_particleSizeRange, 0.001f, out float minSize, out float maxSize);
        GetOrderedRange(_particleSpeedRange, 0f, out float minSpeed, out float maxSpeed);
        GetOrderedRange(_particleLifetimeRange, 0.01f, out float minLifetime, out float maxLifetime);

        ParticleSystem.MainModule main = emitter.Particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = Mathf.Max(0.01f, _emissionDuration);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startColor = Color.white;
        main.gravityModifier = Mathf.Max(0f, _gravityModifier);
        main.maxParticles = Mathf.Max(1, _particleCount);
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = emitter.Particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = emitter.Particles.shape;
        shape.enabled = true;

        if (emitter.Source != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = emitter.Source;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = 0.01f;
            shape.randomDirectionAmount = 0f;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0f, _spawnArea.x), Mathf.Max(0f, _spawnArea.y), Mathf.Max(0f, _spawnArea.z));
            shape.randomDirectionAmount = 1f;
        }

        ParticleSystem.NoiseModule noise = emitter.Particles.noise;
        noise.enabled = _noiseStrength > 0f;
        noise.strength = Mathf.Max(0f, _noiseStrength);
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = emitter.Particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient lifetimeFade = new Gradient();
        lifetimeFade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.8f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = lifetimeFade;

        ParticleSystemRenderer particleRenderer = emitter.Renderer;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static void GetOrderedRange(Vector2 range, float minimum, out float min, out float max)
    {
        min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        max = Mathf.Max(min, Mathf.Max(range.x, range.y));
    }

    private void EmitUntilCount(int targetCount)
    {
        int clampedTarget = Mathf.Clamp(targetCount, 0, Mathf.Max(1, _particleCount));
        if (clampedTarget <= _emittedParticleCount) return;

        float totalWeight = 0f;
        foreach (ParticleEmitter emitter in _emitters)
        {
            totalWeight += GetEmitterWeight(emitter);
        }

        float cumulativeWeight = 0f;
        int allocatedCount = 0;

        for (int i = 0; i < _emitters.Count; i++)
        {
            ParticleEmitter emitter = _emitters[i];
            cumulativeWeight += GetEmitterWeight(emitter);

            int desiredCumulativeCount = (i == _emitters.Count - 1) ? clampedTarget : Mathf.RoundToInt(clampedTarget * cumulativeWeight / totalWeight);
            int desiredEmitterCount = desiredCumulativeCount - allocatedCount;
            int emitCount = desiredEmitterCount - emitter.EmittedCount;

            if (emitCount > 0)
            {
                emitter.Particles.Emit(emitCount);
                emitter.EmittedCount += emitCount;
            }
            allocatedCount = desiredCumulativeCount;
        }

        _emittedParticleCount = clampedTarget;
    }

    private static float GetEmitterWeight(ParticleEmitter emitter)
    {
        if (emitter.Source == null) return 1f;

        Vector3 size = emitter.Source.bounds.size;
        return Mathf.Max(0.001f, 2f * (size.x * size.y + size.x * size.z + size.y * size.z));
    }

    private void UpdateEmitterTransform(ParticleEmitter emitter)
    {
        Transform origin = _particleOrigin != null ? _particleOrigin : transform;

        emitter.Particles.transform.SetPositionAndRotation(origin.position, origin.rotation);
        emitter.Particles.transform.localScale = Vector3.one;
    }

    private bool AnyParticlesAlive()
    {
        foreach (ParticleEmitter emitter in _emitters)
        {
            if (emitter.Particles.IsAlive(true)) return true;
        }

        return false;
    }

    private void ApplyParticleProperties()
    {
        foreach (ParticleEmitter emitter in _emitters)
        {
            emitter.Properties.SetColor(EffectColorId, _effectColor);
            emitter.Properties.SetFloat(BrightnessId, Mathf.Max(0f, _particleBrightness));
            emitter.Properties.SetFloat(OpacityId, Mathf.Clamp01(_opacity));
            emitter.Renderer.SetPropertyBlock(emitter.Properties);
        }
    }

    private void HideParticles()
    {
        _isPlaying = false;
        _opacity = 0f;
        _elapsedTime = 0f;
        _emittedParticleCount = 0;

        foreach (ParticleEmitter emitter in _emitters)
        {
            emitter.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emitter.Particles.gameObject.SetActive(false);
            emitter.EmittedCount = 0;
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }

    private void ClearRuntimeParticles()
    {
        foreach (ParticleEmitter emitter in _emitters)
        {
            if (emitter.Particles != null)
                Destroy(emitter.Particles.gameObject);
        }
        _emitters.Clear();
    }

    internal void Clear()
    {
        HideParticles();
        ClearRuntimeParticles();
    }
}
