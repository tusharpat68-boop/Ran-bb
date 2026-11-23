using System;
using UnityEngine;
using UnityEngine.EventSystems;
using CyberRunner.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using InputTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace CyberRunner.Player
{
    /// <summary>
    /// Responsive Android touch & swipe controller built on Unity Input System (EnhancedTouch)
    /// with DPI-normalized swipe thresholds, UI raycast filtering, zero per-frame allocations,
    /// and keyboard fallback for Editor verification.
    /// </summary>
    public sealed class MobileInputController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        public event Action OnSwipeUp;
        public event Action OnSwipeDown;
        public event Action OnSwipeLeft;
        public event Action OnSwipeRight;
        public event Action OnTap;
        public event Action OnBackPressed;

        public string LastInputLabel { get; private set; } = "READY";

        private int activeFingerId = -1;
        private Vector2 touchStartPos;
        private float touchStartTime;
        private bool swipeConsumed;
        private float swipeThresholdPx;
        private float maxTapDuration;

        // Mouse drag emulation for Editor testing when no touchscreen is active.
        private bool mouseDragging;
        private Vector2 mouseStartPos;
        private float mouseStartTime;
        private bool mouseSwipeConsumed;

        public void SetConfig(GameConfig gameConfig)
        {
            config = gameConfig;
            RecalculateThresholds();
        }

        private void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig");
            }

            RecalculateThresholds();
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += RecalculateThresholds;
            }
            RecalculateThresholds();
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
#endif
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= RecalculateThresholds;
            }
            activeFingerId = -1;
            swipeConsumed = false;
            mouseDragging = false;
#if ENABLE_INPUT_SYSTEM
            if (EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }
#endif
        }

        private void RecalculateThresholds()
        {
            float swipeInches = config != null ? config.swipeThresholdInches : 0.24f;
            float sensMult = SettingsManager.Instance != null ? SettingsManager.Instance.SwipeThresholdMultiplier : 1.0f;
            maxTapDuration = config != null ? config.maxTapDuration : 0.25f;

            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            // Clamp to a practical pixel band so low-DPI emulators and ultra-high-DPI OLED screens both feel crisp.
            swipeThresholdPx = Mathf.Clamp(swipeInches * sensMult * dpi, 28f, 180f);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            ProcessEnhancedTouch();
            ProcessKeyboardInputSystem();
#if UNITY_EDITOR
            ProcessMouseFallbackInputSystem();
#endif
#else
            ProcessLegacyTouch();
            ProcessLegacyKeyboard();
#endif
        }

        /// <summary>
        /// Allows UI buttons (such as Phase 1 jump test or Phase 4 ability trigger) to emit an explicit jump signal.
        /// </summary>
        public void TriggerJumpFromUI()
        {
            EmitSwipeUp();
        }

#if ENABLE_INPUT_SYSTEM
        private void ProcessEnhancedTouch()
        {
            var touches = InputTouch.activeTouches;
            int count = touches.Count;
            if (count == 0)
            {
                activeFingerId = -1;
                swipeConsumed = false;
                return;
            }

            for (int i = 0; i < count; i++)
            {
                InputTouch touch = touches[i];
                int fingerId = touch.finger.index;
                InputTouchPhase phase = touch.phase;

                if (phase == InputTouchPhase.Began)
                {
                    if (activeFingerId == -1 && !IsPointerOverUI(touch.touchId))
                    {
                        activeFingerId = fingerId;
                        touchStartPos = touch.screenPosition;
                        touchStartTime = Time.unscaledTime;
                        swipeConsumed = false;
                    }
                    continue;
                }

                if (fingerId != activeFingerId)
                {
                    continue;
                }

                if (phase == InputTouchPhase.Moved || phase == InputTouchPhase.Stationary)
                {
                    if (!swipeConsumed)
                    {
                        TryEvaluateSwipe(touch.screenPosition - touchStartPos);
                    }
                }
                else if (phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled)
                {
                    if (!swipeConsumed && phase == InputTouchPhase.Ended)
                    {
                        Vector2 delta = touch.screenPosition - touchStartPos;
                        if (!TryEvaluateSwipe(delta))
                        {
                            float duration = Time.unscaledTime - touchStartTime;
                            if (duration <= maxTapDuration && delta.sqrMagnitude < swipeThresholdPx * swipeThresholdPx)
                            {
                                LastInputLabel = "TAP";
                                OnTap?.Invoke();
                            }
                        }
                    }

                    activeFingerId = -1;
                    swipeConsumed = false;
                }
            }
        }

        private void ProcessKeyboardInputSystem()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            {
                EmitSwipeUp();
            }
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            {
                EmitSwipeDown();
            }
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            {
                EmitSwipeLeft();
            }
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            {
                EmitSwipeRight();
            }

            // Unity Input System maps Android's hardware Back button to Escape.
            if (kb.escapeKey.wasPressedThisFrame)
            {
                LastInputLabel = "BACK";
                OnBackPressed?.Invoke();
            }
        }

#if UNITY_EDITOR
        private void ProcessMouseFallbackInputSystem()
        {
            if (InputTouch.activeTouches.Count > 0)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI(-1))
            {
                mouseDragging = true;
                mouseStartPos = mouse.position.ReadValue();
                mouseStartTime = Time.unscaledTime;
                mouseSwipeConsumed = false;
            }
            else if (mouseDragging && mouse.leftButton.isPressed)
            {
                if (!mouseSwipeConsumed)
                {
                    Vector2 delta = mouse.position.ReadValue() - mouseStartPos;
                    if (EvaluateSwipeDelta(delta))
                    {
                        mouseSwipeConsumed = true;
                    }
                }
            }
            else if (mouseDragging && mouse.leftButton.wasReleasedThisFrame)
            {
                if (!mouseSwipeConsumed)
                {
                    Vector2 delta = mouse.position.ReadValue() - mouseStartPos;
                    if (!EvaluateSwipeDelta(delta) && (Time.unscaledTime - mouseStartTime) <= maxTapDuration)
                    {
                        LastInputLabel = "TAP";
                        OnTap?.Invoke();
                    }
                }

                mouseDragging = false;
                mouseSwipeConsumed = false;
            }
        }
#endif
#else
        private void ProcessLegacyTouch()
        {
            int count = Input.touchCount;
            if (count == 0)
            {
                activeFingerId = -1;
                swipeConsumed = false;
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    if (activeFingerId == -1 && !IsPointerOverUI(touch.fingerId))
                    {
                        activeFingerId = touch.fingerId;
                        touchStartPos = touch.position;
                        touchStartTime = Time.unscaledTime;
                        swipeConsumed = false;
                    }
                    continue;
                }

                if (touch.fingerId != activeFingerId)
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (!swipeConsumed)
                    {
                        TryEvaluateSwipe(touch.position - touchStartPos);
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (!swipeConsumed && touch.phase == TouchPhase.Ended)
                    {
                        Vector2 delta = touch.position - touchStartPos;
                        if (!TryEvaluateSwipe(delta) && (Time.unscaledTime - touchStartTime) <= maxTapDuration)
                        {
                            LastInputLabel = "TAP";
                            OnTap?.Invoke();
                        }
                    }

                    activeFingerId = -1;
                    swipeConsumed = false;
                }
            }
        }

        private void ProcessLegacyKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                EmitSwipeUp();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                EmitSwipeDown();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                EmitSwipeLeft();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                EmitSwipeRight();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LastInputLabel = "BACK";
                OnBackPressed?.Invoke();
            }
        }
#endif

        private bool TryEvaluateSwipe(Vector2 delta)
        {
            if (EvaluateSwipeDelta(delta))
            {
                swipeConsumed = true;
                return true;
            }

            return false;
        }

        private bool EvaluateSwipeDelta(Vector2 delta)
        {
            if (delta.sqrMagnitude < swipeThresholdPx * swipeThresholdPx)
            {
                return false;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x > 0f)
                {
                    EmitSwipeRight();
                }
                else
                {
                    EmitSwipeLeft();
                }
            }
            else
            {
                if (delta.y > 0f)
                {
                    EmitSwipeUp();
                }
                else
                {
                    EmitSwipeDown();
                }
            }

            return true;
        }

        private void EmitSwipeUp()
        {
            LastInputLabel = "SWIPE UP (JUMP)";
            OnSwipeUp?.Invoke();
        }

        private void EmitSwipeDown()
        {
            LastInputLabel = "SWIPE DOWN";
            OnSwipeDown?.Invoke();
        }

        private void EmitSwipeLeft()
        {
            LastInputLabel = "SWIPE LEFT";
            OnSwipeLeft?.Invoke();
        }

        private void EmitSwipeRight()
        {
            LastInputLabel = "SWIPE RIGHT";
            OnSwipeRight?.Invoke();
        }

        private static bool IsPointerOverUI(int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return pointerId >= 0
                ? eventSystem.IsPointerOverGameObject(pointerId)
                : eventSystem.IsPointerOverGameObject();
        }
    }
}
