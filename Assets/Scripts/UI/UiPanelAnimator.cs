using UnityEngine;

namespace CyberRunner.UI
{
    /// <summary>
    /// Lightweight, zero-GC UI entrance fade & scale-pop animation using unscaledDeltaTime
    /// so overlays animate smoothly even while gameplay is paused or on Main Menu.
    /// </summary>
    public sealed class UiPanelAnimator : MonoBehaviour
    {
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private float startScale = 0.94f;
        [SerializeField] private bool continuousPulse;
        [SerializeField] private float pulseAmplitude = 0.025f;
        [SerializeField] private float pulseSpeed = 3.2f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private float elapsed;
        private bool animatingIn;

        public void Configure(bool enablePulse = false)
        {
            continuousPulse = enablePulse;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && !continuousPulse)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            elapsed = 0f;
            animatingIn = !continuousPulse;
            if (animatingIn)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                if (rectTransform != null) rectTransform.localScale = Vector3.one * startScale;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            if (continuousPulse)
            {
                if (rectTransform != null)
                {
                    float s = 1f + (Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude);
                    rectTransform.localScale = new Vector3(s, s, 1f);
                }
                return;
            }

            if (!animatingIn) return;

            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float ease = 1f - ((1f - u) * (1f - u)); // smooth ease-out quad

            if (canvasGroup != null) canvasGroup.alpha = ease;
            if (rectTransform != null)
            {
                float s = Mathf.Lerp(startScale, 1f, ease);
                rectTransform.localScale = new Vector3(s, s, 1f);
            }

            if (u >= 1f)
            {
                animatingIn = false;
            }
        }
    }
}
