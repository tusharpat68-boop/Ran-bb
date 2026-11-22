using System;
using UnityEngine;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Obstacles;

namespace CyberRunner.Player
{
    /// <summary>
    /// Phase 2 Cyber Runner locomotion & collision controller.
    /// Drives automatic forward running with smooth distance speed scaling, responsive
    /// three-lane switching with input buffering, coyote-time/buffered jumping,
    /// cyber-sliding + mid-air fast-fall slam-to-slide, and zero-GC PhysX OverlapCapsule
    /// obstacle & Energy Coin detection.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameConfig config;
        [SerializeField] private MobileInputController inputController;
        [SerializeField] private CyberRunnerVisualRig visualRig;

        [Header("Startup")]
        [SerializeField] private bool autoStartRunning = false;

        public event Action<PlayerState> OnStateChanged;
        public event Action<int> OnLaneChanged;
        public event Action OnJumped;
        public event Action OnLanded;
        public event Action OnSlideStarted;
        public event Action OnSlideEnded;
        public event Action<string> OnStumbled;
        public event Action<string> OnCrashed;
        public event Action<EnergyCoin> OnCoinTouched;

        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
        public int CurrentLane { get; private set; } // -1 = Left, 0 = Center, +1 = Right
        public float CurrentForwardSpeed { get; private set; }
        public float VerticalVelocity { get; private set; }
        public float DistanceTraveled { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public bool IsAlive => CurrentState != PlayerState.Dead;
        public float NormalizedSpeed => config != null && config.maxRunSpeed > config.baseRunSpeed
            ? Mathf.Clamp01((CurrentForwardSpeed - config.baseRunSpeed) / (config.maxRunSpeed - config.baseRunSpeed))
            : 0f;

        private CharacterController characterController;
        private readonly Collider[] overlapBuffer = new Collider[16];

        private float startZ;
        private int previousLane;
        private int bufferedLaneDelta;
        private float laneBufferTimer;
        private float lateralVelocityX;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private float landingStateTimer;

        private float slideTimer;
        private bool pendingSlideOnLand;

        private float stumbleRecoveryTimer;
        private float stumbleGraceTimer;

        private bool wasGrounded;
        private bool isRunningActive;

        public void Configure(GameConfig gameConfig, MobileInputController input, CyberRunnerVisualRig rig)
        {
            config = gameConfig;
            inputController = input;
            visualRig = rig;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }

            if (inputController == null)
            {
                inputController = GetComponent<MobileInputController>();
            }

            if (visualRig == null)
            {
                visualRig = GetComponent<CyberRunnerVisualRig>();
            }

            ApplyColliderHeight(config.standingColliderHeight);
            characterController.radius = 0.34f;
            characterController.stepOffset = 0.25f;
            characterController.slopeLimit = 45f;
            characterController.skinWidth = 0.04f;
            startZ = transform.position.z;
        }

        private void OnEnable()
        {
            if (inputController != null)
            {
                inputController.OnSwipeUp += HandleJumpRequested;
                inputController.OnSwipeDown += HandleSlideRequested;
                inputController.OnSwipeLeft += HandleMoveLeftRequested;
                inputController.OnSwipeRight += HandleMoveRightRequested;
            }
        }

        private void OnDisable()
        {
            if (inputController != null)
            {
                inputController.OnSwipeUp -= HandleJumpRequested;
                inputController.OnSwipeDown -= HandleSlideRequested;
                inputController.OnSwipeLeft -= HandleMoveLeftRequested;
                inputController.OnSwipeRight -= HandleMoveRightRequested;
            }
        }

        private void Start()
        {
            if (autoStartRunning)
            {
                StartRun();
            }
        }

        /// <summary>
        /// Resets position, lane, timers, collider height, and visual rig for a crisp zero-lag restart.
        /// </summary>
        public void ResetForNewRun(Vector3 spawnPosition)
        {
            isRunningActive = false;
            IsSliding = false;
            pendingSlideOnLand = false;
            slideTimer = 0f;
            stumbleRecoveryTimer = 0f;
            stumbleGraceTimer = 0f;
            bufferedLaneDelta = 0;
            laneBufferTimer = 0f;
            lateralVelocityX = 0f;

            CurrentLane = 0;
            previousLane = 0;
            DistanceTraveled = 0f;
            VerticalVelocity = config.groundedStickVelocity;
            coyoteTimer = config.coyoteTime;
            jumpBufferTimer = 0f;
            landingStateTimer = 0f;
            wasGrounded = true;
            IsGrounded = true;

            ApplyColliderHeight(config.standingColliderHeight);

            // Safely teleport CharacterController by disabling briefly.
            characterController.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            characterController.enabled = true;

            startZ = spawnPosition.z;

            if (visualRig != null)
            {
                visualRig.ResetVisualPose();
            }

            OnLaneChanged?.Invoke(CurrentLane);
            StartRun();
        }

        public void StartRun()
        {
            isRunningActive = true;
            CurrentForwardSpeed = Mathf.Max(2f, config.baseRunSpeed * 0.65f);
            VerticalVelocity = config.groundedStickVelocity;
            coyoteTimer = config.coyoteTime;
            jumpBufferTimer = 0f;
            landingStateTimer = 0f;
            SetState(PlayerState.Running);
        }

        public void RequestJump()
        {
            if (!isRunningActive || CurrentState == PlayerState.Dead)
            {
                return;
            }

            if (IsSliding)
            {
                EndSlide();
            }

            pendingSlideOnLand = false;
            jumpBufferTimer = config.jumpBufferTime;
        }

        public void RequestSlide()
        {
            if (!isRunningActive || CurrentState == PlayerState.Dead)
            {
                return;
            }

            if (IsGrounded)
            {
                BeginSlide();
            }
            else
            {
                // Mid-air fast-fall slam into an immediate cyber-slide on touchdown!
                VerticalVelocity = Mathf.Min(VerticalVelocity, config.fastFallVelocity);
                pendingSlideOnLand = true;
            }
        }

        public void RequestLaneChange(int delta)
        {
            if (!isRunningActive || CurrentState == PlayerState.Dead || delta == 0)
            {
                return;
            }

            int clampedDelta = delta > 0 ? 1 : -1;
            int targetLane = Mathf.Clamp(CurrentLane + clampedDelta, -1, 1);

            // If still far from completing the current switch, buffer the next swipe cleanly.
            float currentTargetX = CurrentLane * config.laneWidth;
            if (Mathf.Abs(transform.position.x - currentTargetX) > config.laneWidth * 0.65f)
            {
                bufferedLaneDelta = clampedDelta;
                laneBufferTimer = config.laneInputBufferTime;
                return;
            }

            if (targetLane == CurrentLane)
            {
                return;
            }

            previousLane = CurrentLane;
            CurrentLane = targetLane;
            OnLaneChanged?.Invoke(CurrentLane);
        }

        private void HandleJumpRequested() => RequestJump();
        private void HandleSlideRequested() => RequestSlide();
        private void HandleMoveLeftRequested() => RequestLaneChange(-1);
        private void HandleMoveRightRequested() => RequestLaneChange(1);

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (!isRunningActive || CurrentState == PlayerState.Dead)
            {
                if (visualRig != null)
                {
                    visualRig.UpdateAnimation(CurrentState, 0f, 0f, dt, 0f);
                }
                return;
            }

            UpdateTimers(dt);
            UpdateForwardSpeed(dt);
            UpdateSlide(dt);
            UpdateJumpAndGravity(dt);
            MoveCharacter(dt);
            ProcessPhysicsOverlaps();
            UpdateDistanceAndState(dt);

            if (visualRig != null)
            {
                float targetBank = Mathf.Clamp((lateralVelocityX / 12f) * config.laneSwitchBankAngle,
                    -config.laneSwitchBankAngle, config.laneSwitchBankAngle);
                visualRig.UpdateAnimation(CurrentState, CurrentForwardSpeed, VerticalVelocity, dt, targetBank);
            }
        }

        private void UpdateTimers(float dt)
        {
            if (stumbleRecoveryTimer > 0f)
            {
                stumbleRecoveryTimer = Mathf.Max(0f, stumbleRecoveryTimer - dt);
            }

            if (stumbleGraceTimer > 0f)
            {
                stumbleGraceTimer = Mathf.Max(0f, stumbleGraceTimer - dt);
            }

            if (laneBufferTimer > 0f)
            {
                laneBufferTimer = Mathf.Max(0f, laneBufferTimer - dt);
                float currentTargetX = CurrentLane * config.laneWidth;
                if (bufferedLaneDelta != 0 && Mathf.Abs(transform.position.x - currentTargetX) < config.laneWidth * 0.4f)
                {
                    int delta = bufferedLaneDelta;
                    bufferedLaneDelta = 0;
                    laneBufferTimer = 0f;
                    RequestLaneChange(delta);
                }
                else if (laneBufferTimer <= 0f)
                {
                    bufferedLaneDelta = 0;
                }
            }
        }

        private void UpdateForwardSpeed(float dt)
        {
            float distanceBonus = (DistanceTraveled / 100f) * config.speedIncreasePer100Meters;
            float targetSpeed = Mathf.Min(config.maxRunSpeed, config.baseRunSpeed + distanceBonus);

            if (CurrentForwardSpeed < targetSpeed)
            {
                CurrentForwardSpeed = Mathf.MoveTowards(
                    CurrentForwardSpeed,
                    targetSpeed,
                    config.startupAcceleration * dt);
            }
            else
            {
                CurrentForwardSpeed = targetSpeed;
            }
        }

        private void BeginSlide()
        {
            if (IsSliding)
            {
                // Refresh slide timer cleanly if swiping down again mid-slide.
                slideTimer = config.slideDuration;
                return;
            }

            IsSliding = true;
            slideTimer = config.slideDuration;
            pendingSlideOnLand = false;
            ApplyColliderHeight(config.slidingColliderHeight);
            SetState(PlayerState.Sliding);
            OnSlideStarted?.Invoke();
        }

        private void EndSlide()
        {
            if (!IsSliding)
            {
                return;
            }

            IsSliding = false;
            slideTimer = 0f;
            ApplyColliderHeight(config.standingColliderHeight);
            OnSlideEnded?.Invoke();
        }

        private void UpdateSlide(float dt)
        {
            if (!IsSliding)
            {
                return;
            }

            slideTimer -= dt;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }

        private void UpdateJumpAndGravity(float dt)
        {
            if (characterController.isGrounded)
            {
                coyoteTimer = config.coyoteTime;
                if (VerticalVelocity < 0f)
                {
                    VerticalVelocity = config.groundedStickVelocity;
                }
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
            }

            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
            }

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                if (IsSliding)
                {
                    EndSlide();
                }

                VerticalVelocity = config.JumpVelocity;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                landingStateTimer = 0f;
                SetState(PlayerState.Jumping);
                OnJumped?.Invoke();
            }

            float gravity = config.Gravity;
            if (VerticalVelocity < 0f && !characterController.isGrounded)
            {
                gravity *= config.fallGravityMultiplier;
            }

            VerticalVelocity += gravity * dt;
            VerticalVelocity = Mathf.Max(VerticalVelocity, -50f);
        }

        private void MoveCharacter(float dt)
        {
            float targetX = CurrentLane * config.laneWidth;
            float currentX = transform.position.x;
            float nextX = Mathf.Lerp(currentX, targetX, 1f - Mathf.Exp(-config.laneSwitchSharpness * dt));
            float deltaX = nextX - currentX;
            lateralVelocityX = deltaX / dt;

            Vector3 displacement = new Vector3(deltaX, VerticalVelocity * dt, CurrentForwardSpeed * dt);
            characterController.Move(displacement);

            // Catch falling through Sky Highway road gaps promptly.
            if (transform.position.y < -2.2f)
            {
                TriggerCrash("SKY ABYSS GAP");
                return;
            }

            IsGrounded = characterController.isGrounded;
            if (IsGrounded && !wasGrounded)
            {
                if (pendingSlideOnLand)
                {
                    pendingSlideOnLand = false;
                    BeginSlide();
                }
                else
                {
                    landingStateTimer = 0.12f;
                    if (visualRig != null)
                    {
                        visualRig.TriggerLandingImpact();
                    }
                }

                OnLanded?.Invoke();
            }

            wasGrounded = IsGrounded;
        }

        /// <summary>
        /// Zero-GC PhysX capsule overlap detection against pooled obstacles and Energy Coins.
        /// Uses the live CharacterController standing/sliding height so laser gates and barriers feel 100% fair.
        /// </summary>
        private void ProcessPhysicsOverlaps()
        {
            if (CurrentState == PlayerState.Dead)
            {
                return;
            }

            float radius = characterController.radius * 0.92f;
            float height = characterController.height;
            Vector3 worldCenter = transform.position + characterController.center;
            float halfSegment = Mathf.Max(0.05f, (height * 0.5f) - radius);

            Vector3 p0 = worldCenter + new Vector3(0f, -halfSegment, 0f);
            Vector3 p1 = worldCenter + new Vector3(0f, halfSegment, 0f);

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                p0,
                p1,
                radius,
                overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (col.TryGetComponent(out EnergyCoin coin))
                {
                    if (!coin.IsCollected)
                    {
                        OnCoinTouched?.Invoke(coin);
                    }
                    continue;
                }

                if (col.TryGetComponent(out ObstacleController obstacle))
                {
                    ResolveObstacleOverlap(obstacle);
                    if (CurrentState == PlayerState.Dead)
                    {
                        return;
                    }
                }
            }
        }

        private void ResolveObstacleOverlap(ObstacleController obstacle)
        {
            if (obstacle.CollisionResolved)
            {
                return;
            }

            float playerFeetY = transform.position.y;
            float playerHeadY = playerFeetY + characterController.height;

            // Fair jump clearance over low road barriers.
            if (obstacle.CanJumpOver && playerFeetY >= obstacle.ClearanceTopY - 0.18f)
            {
                return;
            }

            // Fair slide clearance under overhead laser gates & hovering drones.
            if (obstacle.CanSlideUnder && IsSliding && playerHeadY <= obstacle.ClearanceBottomY + 0.08f)
            {
                return;
            }

            // Brief grace invulnerability right after recovering from a side graze.
            if (stumbleGraceTimer > 0f)
            {
                obstacle.MarkCollisionResolved();
                return;
            }

            // Check for side-graze forgiveness when swiping sideways into the outer edge of an obstacle.
            float lateralOffset = Mathf.Abs(transform.position.x - obstacle.transform.position.x);
            bool isTransitioningSideways = Mathf.Abs(lateralVelocityX) > 3.5f;
            if (isTransitioningSideways && lateralOffset > 0.72f && previousLane != CurrentLane)
            {
                obstacle.MarkCollisionResolved();
                if (stumbleRecoveryTimer > 0f)
                {
                    // Second stumble during recovery window causes a crash.
                    TriggerCrash(obstacle.DisplayName);
                }
                else
                {
                    TriggerSideStumble(obstacle.DisplayName);
                }
                return;
            }

            obstacle.MarkCollisionResolved();
            TriggerCrash(obstacle.DisplayName);
        }

        private void TriggerSideStumble(string obstacleName)
        {
            CurrentLane = previousLane;
            OnLaneChanged?.Invoke(CurrentLane);

            stumbleRecoveryTimer = config.stumbleRecoveryDuration;
            stumbleGraceTimer = config.stumbleGraceTime;
            CurrentForwardSpeed = Mathf.Max(config.baseRunSpeed * 0.85f, CurrentForwardSpeed * 0.88f);

            if (visualRig != null)
            {
                visualRig.TriggerStumbleFeedback();
            }

            SetState(PlayerState.Stumbled);
            OnStumbled?.Invoke(obstacleName);
        }

        private void TriggerCrash(string causeName)
        {
            if (CurrentState == PlayerState.Dead)
            {
                return;
            }

            isRunningActive = false;
            if (IsSliding)
            {
                EndSlide();
            }

            CurrentForwardSpeed = 0f;
            SetState(PlayerState.Dead);
            OnCrashed?.Invoke(causeName);
        }

        private void UpdateDistanceAndState(float dt)
        {
            if (CurrentState == PlayerState.Dead)
            {
                return;
            }

            DistanceTraveled = Mathf.Max(0f, transform.position.z - startZ);

            if (IsSliding)
            {
                SetState(PlayerState.Sliding);
                return;
            }

            if (stumbleGraceTimer > 0f)
            {
                SetState(PlayerState.Stumbled);
                return;
            }

            if (IsGrounded)
            {
                if (landingStateTimer > 0f)
                {
                    landingStateTimer = Mathf.Max(0f, landingStateTimer - dt);
                    SetState(PlayerState.Landing);
                }
                else
                {
                    SetState(PlayerState.Running);
                }
            }
            else
            {
                SetState(VerticalVelocity > 0.25f ? PlayerState.Jumping : PlayerState.Falling);
            }
        }

        private void ApplyColliderHeight(float height)
        {
            characterController.height = height;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private void SetState(PlayerState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            CurrentState = nextState;
            OnStateChanged?.Invoke(nextState);
        }
    }
}
