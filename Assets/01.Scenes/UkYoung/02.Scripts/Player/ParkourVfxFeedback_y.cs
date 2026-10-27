using UnityEngine;
using UnityEngine.Rendering;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(ParkourFeedbackSignals_y))]
    public sealed class ParkourVfxFeedback_y : MonoBehaviour
    {
        [SerializeField] private ParkourFeedbackSignals_y _signals;
        [SerializeField] private ParkourMotor _motor;
        [SerializeField] private Texture2D _dustFlipbookTexture;
        [SerializeField] private Texture2D _dustParticleTexture;

        [Header("Density")]
        [SerializeField] private float _slideEmissionDistance = 0.8f;
        [SerializeField] private float _wallRunEmissionDistance = 1.1f;

        private ParticleSystem _impactSystem;
        private ParticleSystem _dustSystem;
        private Material _impactMaterial;
        private Material _dustMaterial;
        private Vector3 _previousPosition;
        private float _slideDistance;
        private float _wallRunDistance;

        private void Awake()
        {
            _signals ??= GetComponent<ParkourFeedbackSignals_y>();
            _motor ??= GetComponent<ParkourMotor>();
            _impactSystem = CreateImpactSystem();
            _dustSystem = CreateDustSystem();
            _previousPosition = transform.position;
        }

        private void OnEnable()
        {
            if (_signals == null)
            {
                return;
            }

            _signals.FootContact += HandleFootContact;
            _signals.JumpTakeoff += HandleJumpTakeoff;
            _signals.LandingContact += HandleLandingContact;
            _signals.HandContact += HandleHandContact;
        }

        private void OnDisable()
        {
            if (_signals == null)
            {
                return;
            }

            _signals.FootContact -= HandleFootContact;
            _signals.JumpTakeoff -= HandleJumpTakeoff;
            _signals.LandingContact -= HandleLandingContact;
            _signals.HandContact -= HandleHandContact;
        }

        private void OnDestroy()
        {
            if (_impactMaterial != null)
            {
                Destroy(_impactMaterial);
            }

            if (_dustMaterial != null)
            {
                Destroy(_dustMaterial);
            }
        }

        private void Update()
        {
            float distance = Vector3.Distance(transform.position, _previousPosition);
            _previousPosition = transform.position;

            if (_motor != null && _motor.IsSliding)
            {
                _slideDistance += distance;
                if (_slideDistance >= _slideEmissionDistance)
                {
                    _signals.GetGroundContact(out Vector3 groundPoint, out Vector3 groundNormal);
                    EmitDust(groundPoint, groundNormal, 3, 0.32f, 0.75f);
                    _slideDistance = 0f;
                }
            }
            else
            {
                _slideDistance = 0f;
            }

            if (_motor != null && _motor.IsWallRunning && _motor.ActiveWallCollider != null)
            {
                _wallRunDistance += distance;
                if (_wallRunDistance >= _wallRunEmissionDistance)
                {
                    Vector3 wallPoint = _motor.ActiveWallCollider.ClosestPoint(transform.position);
                    Vector3 wallNormal = (transform.position - wallPoint).normalized;
                    EmitDust(wallPoint, wallNormal, 2, 0.22f, 0.55f);
                    _wallRunDistance = 0f;
                }
            }
            else
            {
                _wallRunDistance = 0f;
            }
        }

        private ParticleSystem CreateImpactSystem()
        {
            ParticleSystem system = CreateParticleSystem("Parkour Contact VFX");
            var main = system.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.75f;
            main.maxParticles = 8;

            var textureSheet = system.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Grid;
            textureSheet.numTilesX = 4;
            textureSheet.numTilesY = 3;
            textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
            textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            textureSheet.cycleCount = 1;

            _impactMaterial = CreateParticleMaterial(_dustFlipbookTexture);
            system.GetComponent<ParticleSystemRenderer>().material = _impactMaterial;
            return system;
        }

        private ParticleSystem CreateDustSystem()
        {
            ParticleSystem system = CreateParticleSystem("Parkour Trail Dust");
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.63f, 0.67f, 0.7f, 0.46f),
                new Color(0.84f, 0.86f, 0.88f, 0.65f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 32;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.35f));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.65f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            _dustMaterial = CreateParticleMaterial(_dustParticleTexture);
            system.GetComponent<ParticleSystemRenderer>().material = _dustMaterial;
            return system;
        }

        private ParticleSystem CreateParticleSystem(string objectName)
        {
            var effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(transform, false);
            var system = effectObject.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static Material CreateParticleMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            return material;
        }

        private void HandleFootContact(Vector3 point, Vector3 normal, float intensity)
        {
            EmitDust(point, normal, 1, 0.14f, 0.3f);
        }

        private void HandleJumpTakeoff(Vector3 point, Vector3 normal, float intensity)
        {
            EmitDust(point, normal, 4, 0.24f, 0.65f);
            EmitImpact(point, normal, 0.48f);
        }

        private void HandleLandingContact(Vector3 point, Vector3 normal, float intensity)
        {
            EmitDust(point, normal, Mathf.RoundToInt(Mathf.Lerp(4f, 8f, intensity)),
                Mathf.Lerp(0.25f, 0.4f, intensity), Mathf.Lerp(0.65f, 1.15f, intensity));
            EmitImpact(point, normal, Mathf.Lerp(0.55f, 1.05f, intensity));
        }

        private void HandleHandContact(Vector3 point, Vector3 normal, float intensity)
        {
            EmitDust(point, normal, 2, 0.16f, 0.38f);
        }

        private void EmitImpact(Vector3 point, Vector3 normal, float size)
        {
            if (_impactSystem == null || _impactMaterial == null)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams
            {
                position = point + normal * 0.025f,
                rotation3D = Quaternion.LookRotation(normal, Vector3.up).eulerAngles,
                startSize = size
            };
            _impactSystem.Emit(emit, 1);
        }

        private void EmitDust(
            Vector3 point,
            Vector3 normal,
            int count,
            float minimumSize,
            float maximumSize)
        {
            if (_dustSystem == null || _dustMaterial == null)
            {
                return;
            }

            for (int index = 0; index < count; index++)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized;
                Vector3 velocity = normal * Random.Range(0.12f, 0.5f)
                                   + tangent * Random.Range(0.05f, 0.4f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + normal * 0.02f,
                    velocity = velocity,
                    startSize = Random.Range(minimumSize, maximumSize)
                };
                _dustSystem.Emit(emit, 1);
            }
        }

    }
}
