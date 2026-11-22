using UnityEngine;
using CyberRunner.Core;
using CyberRunner.Player;

namespace CyberRunner.CameraSystem
{
    /// <summary>
    /// Smooth third-person endless-runner camera built for low-end Android stability.
    /// Features framerate-independent exponential smoothing, three-lane portrait framing,
    /// gentle jump & slide height dynamics, speed FOV scaling, SphereCast collision avoidance,
    /// and impact shake on stumbles/crashes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CyberCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GameConfig config;

        [Header("Tracking Tuning")]
        [SerializeField] private float lateralFollowSharpness = 9.5f;
        [Tooltip("Tracks a fraction of player X so all 3 lanes stay framed comfortably in Portrait view.")]
        [Range(0.35f, 1.0f)]
        [SerializeField] private float lateralTrackRatio = 0.64f;
        [SerializeField] private float verticalFollowSharpness = 6.5f;
        [SerializeField] private float lookAheadSharpness = 12f;
        [Range(0.75f, 1.45f)]
        [SerializeField] private float distanceMultiplier = 1.0f;

        [Header("Jump & Slide Dynamics")]
        [Range(0.15f, 0.9f)]
        [SerializeField] private float jumpVerticalInfluence = 0.42f;
        [SerializeField] private float slideCameraDip = 0.45f;

        [Header("Collision Awareness")]
        [SerializeField] private bool enableCollisionCheck = true;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionSphereRadius = 0.28f;
        [SerializeField] private float collisionPadding = 0.25f;
        [SerializeField] private float minCameraDistance = 2.6f;

        private Camera cachedCamera;
        private Vector3 currentLookTarget;
        private float smoothedX;
        private float smoothedY;
        private float baselineGroundY;
        private float shakeTimer;
        private float shakeDuration;
        private float shakeIntensity;
        private bool initialized;

        public float DistanceMultiplier
        {
            get => distanceMultiplier;
            set => distanceMultiplier = Mathf.Clamp(value, 0.75f, 1.45f);
        }

        public void SetTarget(Transform targetTransform, PlayerController controller, GameConfig gameConfig)
        {
            target = targetTransform;
            playerController = controller;
            config = gameConfig;
            initialized = false;
        }

        public void TriggerShake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(0.05f, duration);
            shakeTimer = shakeDuration;
        }

        private void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }
        }

        private void Start()
        {
            if (target == null)
            {
                PlayerController found = FindFirstObjectByType<PlayerController>();
                if (found != null)
                {
                    target = found.transform;
                    playerController = found;
                }
            }

            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            shakeTimer = 0f;
            shakeIntensity = 0f;

            Vector3 baseOffset = GetScaledOffset();
            baselineGroundY = target.position.y;
            smoothedX = (target.position.x * lateralTrackRatio) + baseOffset.x;
            smoothedY = baselineGroundY + baseOffset.y;

            Vector3 desiredPos = new Vector3(smoothedX, smoothedY, target.position.z + baseOffset.z);
            transform.position = desiredPos;

            currentLookTarget = new Vector3(
                (target.position.x * 0.75f) + config.cameraLookOffset.x,
                baselineGroundY + config.cameraLookOffset.y,
                target.position.z + config.cameraLookOffset.z);

            transform.LookAt(currentLookTarget);
            cachedCamera.fieldOfView = config.baseCameraFov;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!initialized)
            {
                SnapToTarget();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 offset = GetScaledOffset();
            Vector3 targetPos = target.position;

            bool grounded = playerController == null || playerController.IsGrounded;
            if (grounded)
            {
                baselineGroundY = Mathf.Lerp(baselineGroundY, targetPos.y, 1f - Mathf.Exp(-10f * dt));
            }

            float jumpDeltaY = Mathf.Max(0f, targetPos.y - baselineGroundY);
            float slideOffset = (playerController != null && playerController.IsSliding) ? -slideCameraDip : 0f;
            float desiredY = baselineGroundY + offset.y + (jumpDeltaY * jumpVerticalInfluence) + slideOffset;
            float desiredX = (targetPos.x * lateralTrackRatio) + offset.x;

            float xLerp = 1f - Mathf.Exp(-lateralFollowSharpness * dt);
            float yLerp = 1f - Mathf.Exp(-verticalFollowSharpness * dt);
            smoothedX = Mathf.Lerp(smoothedX, desiredX, xLerp);
            smoothedY = Mathf.Lerp(smoothedY, desiredY, yLerp);

            Vector3 desiredCameraPos = new Vector3(smoothedX, smoothedY, targetPos.z + offset.z);

            if (enableCollisionCheck)
            {
                desiredCameraPos = ResolveCameraCollision(targetPos, desiredCameraPos);
            }

            if (shakeTimer > 0f)
            {
                shakeTimer = Mathf.Max(0f, shakeTimer - dt);
                float damper = shakeTimer / Mathf.Max(0.05f, shakeDuration);
                float mag = shakeIntensity * damper;
                desiredCameraPos.x += Mathf.Sin(Time.unscaledTime * 55f) * mag;
                desiredCameraPos.y += Mathf.Cos(Time.unscaledTime * 68f) * mag * 0.7f;
                if (shakeTimer <= 0f)
                {
                    shakeIntensity = 0f;
                }
            }

            transform.position = desiredCameraPos;

            Vector3 desiredLookTarget = new Vector3(
                (targetPos.x * 0.75f) + config.cameraLookOffset.x,
                baselineGroundY + config.cameraLookOffset.y + (jumpDeltaY * 0.25f) + (slideOffset * 0.5f),
                targetPos.z + config.cameraLookOffset.z);

            float lookLerp = 1f - Mathf.Exp(-lookAheadSharpness * dt);
            currentLookTarget = Vector3.Lerp(currentLookTarget, desiredLookTarget, lookLerp);
            transform.LookAt(currentLookTarget);

            UpdateDynamicFov(dt);
        }

        private Vector3 GetScaledOffset()
        {
            Vector3 offset = config.cameraOffset;
            return new Vector3(offset.x, offset.y * Mathf.Lerp(1f, distanceMultiplier, 0.5f), offset.z * distanceMultiplier);
        }

        private Vector3 ResolveCameraCollision(Vector3 targetPos, Vector3 desiredCameraPos)
        {
            Vector3 pivot = targetPos + new Vector3(0f, 1.4f, 0f);
            Vector3 toCamera = desiredCameraPos - pivot;
            float distance = toCamera.magnitude;

            if (distance <= 0.01f)
            {
                return desiredCameraPos;
            }

            Vector3 direction = toCamera / distance;
            if (Physics.SphereCast(pivot, collisionSphereRadius, direction, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    float safeDistance = Mathf.Max(minCameraDistance, hit.distance - collisionPadding);
                    return pivot + (direction * safeDistance);
                }
            }

            return desiredCameraPos;
        }

        private void UpdateDynamicFov(float dt)
        {
            float normalizedSpeed = playerController != null ? playerController.NormalizedSpeed : 0f;
            float targetFov = config.baseCameraFov + (config.maxSpeedFovBoost * normalizedSpeed);
            float fovLerp = 1f - Mathf.Exp(-5f * dt);
            cachedCamera.fieldOfView = Mathf.Lerp(cachedCamera.fieldOfView, targetFov, fovLerp);
        }
    }
}
