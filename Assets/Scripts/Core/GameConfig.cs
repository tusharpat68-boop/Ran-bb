using UnityEngine;

namespace CyberRunner.Core
{
    /// <summary>
    /// Central data-driven configuration for CYBER RUNNER movement, three-lane switching,
    /// jump & slide physics, track generation, camera feel, touch thresholds, and mobile FPS defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CyberRunner/Game Config", order = 1)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Runner Forward Movement")]
        [Tooltip("Initial running speed in meters per second.")]
        [Min(1f)]
        public float baseRunSpeed = 14f;

        [Tooltip("Maximum forward speed cap in meters per second.")]
        [Min(1f)]
        public float maxRunSpeed = 30f;

        [Tooltip("Acceleration rate when starting a run (m/s^2).")]
        [Min(1f)]
        public float startupAcceleration = 18f;

        [Tooltip("Gradual speed gain per 100 meters traveled (m/s).")]
        [Min(0f)]
        public float speedIncreasePer100Meters = 0.85f;

        [Header("Three-Lane Movement (Phase 2)")]
        [Tooltip("Center-to-center distance between adjacent lanes in meters (lanes at -X, 0, +X).")]
        [Range(2.2f, 3.6f)]
        public float laneWidth = 2.9f;

        [Tooltip("Exponential sharpness controlling lateral lane switch speed.")]
        [Range(8f, 28f)]
        public float laneSwitchSharpness = 16f;

        [Tooltip("Visual roll bank angle in degrees during lateral lane switches.")]
        [Range(0f, 28f)]
        public float laneSwitchBankAngle = 15f;

        [Tooltip("Buffer window to queue an additional lane swipe during an active transition (seconds).")]
        [Range(0f, 0.3f)]
        public float laneInputBufferTime = 0.16f;

        [Header("Jump & Gravity Physics")]
        [Tooltip("Peak jump height in meters.")]
        [Min(0.5f)]
        public float jumpHeight = 2.65f;

        [Tooltip("Time in seconds to reach jump apex (controls snappiness).")]
        [Range(0.18f, 0.6f)]
        public float timeToJumpApex = 0.34f;

        [Tooltip("Multiplier applied to gravity while falling for crisp arcade feel.")]
        [Range(1.0f, 3.0f)]
        public float fallGravityMultiplier = 1.65f;

        [Tooltip("Grace window after leaving ground where a jump still triggers (seconds).")]
        [Range(0f, 0.25f)]
        public float coyoteTime = 0.10f;

        [Tooltip("Buffer window before landing where a swipe-up jump is queued (seconds).")]
        [Range(0f, 0.25f)]
        public float jumpBufferTime = 0.14f;

        [Tooltip("Small downward velocity applied while grounded to keep CharacterController snapped to the track.")]
        public float groundedStickVelocity = -4f;

        [Header("Slide & Fast-Fall Physics (Phase 2)")]
        [Tooltip("Duration of a cyber-slide in seconds.")]
        [Range(0.4f, 1.2f)]
        public float slideDuration = 0.72f;

        [Tooltip("Downward velocity applied when swiping down mid-air (slam-to-slide).")]
        public float fastFallVelocity = -34f;

        [Tooltip("CharacterController standing collider height in meters.")]
        public float standingColliderHeight = 1.82f;

        [Tooltip("CharacterController sliding collider height in meters (clears overhead laser gates).")]
        public float slidingColliderHeight = 0.85f;

        [Header("Collision & Stumble Forgiveness (Phase 2)")]
        [Tooltip("Seconds of stumble warning window; stumbling again during this window causes a crash.")]
        [Range(0.5f, 3.5f)]
        public float stumbleRecoveryDuration = 1.8f;

        [Tooltip("Brief invulnerability window immediately after a side-graze stumble (seconds).")]
        [Range(0.15f, 0.8f)]
        public float stumbleGraceTime = 0.45f;

        [Header("Endless Track Generation (Phase 2)")]
        [Tooltip("Length of each pooled highway segment in meters.")]
        [Min(20f)]
        public float trackSegmentLength = 40f;

        [Tooltip("Number of pooled highway segments kept active simultaneously.")]
        [Range(4, 8)]
        public int activeTrackSegments = 6;

        [Tooltip("Number of clean obstacle-free intro segments at the start of every run.")]
        [Range(1, 3)]
        public int safeIntroSegments = 2;

        [Header("Camera Configuration")]
        [Tooltip("Default local offset behind and above the runner.")]
        public Vector3 cameraOffset = new Vector3(0f, 4.4f, -7.2f);

        [Tooltip("Look-at height and forward lead relative to the runner.")]
        public Vector3 cameraLookOffset = new Vector3(0f, 1.55f, 8.0f);

        [Tooltip("Base camera field of view in degrees (Portrait Android).")]
        [Range(45f, 85f)]
        public float baseCameraFov = 62f;

        [Tooltip("Maximum additional FOV degrees at peak speed.")]
        [Range(0f, 20f)]
        public float maxSpeedFovBoost = 8f;

        [Header("Touch Controls")]
        [Tooltip("Minimum physical swipe distance in inches (DPI-normalized).")]
        [Range(0.12f, 0.6f)]
        public float swipeThresholdInches = 0.24f;

        [Tooltip("Maximum tap duration in seconds before touch is no longer treated as a tap.")]
        [Range(0.1f, 0.5f)]
        public float maxTapDuration = 0.25f;

        [Header("Mobile Performance Defaults")]
        [Tooltip("Default target FPS (30 for 2GB-3GB low-end devices, 60 for supported devices).")]
        public int defaultTargetFps = 60;

        /// <summary>
        /// Computes upward jump velocity from desired jump height and time to apex: v0 = 2h / t.
        /// </summary>
        public float JumpVelocity => (2f * jumpHeight) / Mathf.Max(0.05f, timeToJumpApex);

        /// <summary>
        /// Computes base downward gravity magnitude: g = 2h / t^2 (returned negative).
        /// </summary>
        public float Gravity => -(2f * jumpHeight) / Mathf.Max(0.0025f, timeToJumpApex * timeToJumpApex);

        /// <summary>
        /// Creates a safe in-memory fallback instance if no GameConfig asset is assigned.
        /// </summary>
        public static GameConfig CreateRuntimeDefault()
        {
            return CreateInstance<GameConfig>();
        }
    }
}
