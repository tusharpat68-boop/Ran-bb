using UnityEngine;

namespace CyberRunner.Collectibles
{
    /// <summary>
    /// Pooled collectible Energy Coin with duplicate-collection guard and crisp pickup pop animation.
    /// Driven centrally by CoinPoolManager to eliminate per-coin Update() overhead on low-end Android.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class EnergyCoin : MonoBehaviour
    {
        public bool IsCollected { get; private set; }
        public int CoinValue { get; private set; } = 1;

        private Transform visualTransform;
        private Vector3 spawnPosition;
        private float spinAngle;
        private float bobPhase;
        private float collectAnimTimer;
        private const float CollectAnimDuration = 0.16f;

        public void SetVisualTransform(Transform visual)
        {
            visualTransform = visual;
        }

        public void ActivateAt(Vector3 worldPosition, int value = 1)
        {
            CoinValue = Mathf.Max(1, value);
            IsCollected = false;
            collectAnimTimer = 0f;
            spawnPosition = worldPosition;
            transform.position = worldPosition;

            spinAngle = (worldPosition.z * 18f) % 360f;
            bobPhase = worldPosition.z * 0.45f;

            if (visualTransform != null)
            {
                visualTransform.localScale = Vector3.one;
                visualTransform.localRotation = Quaternion.Euler(45f, spinAngle, 45f);
            }
        }

        /// <summary>
        /// Attempts to claim this coin once. Returns false if already collected (prevents duplication).
        /// </summary>
        public bool TryCollect()
        {
            if (IsCollected)
            {
                return false;
            }

            IsCollected = true;
            collectAnimTimer = 0f;
            return true;
        }

        /// <summary>
        /// Advances coin rotation/bob or collection pop animation.
        /// Returns true when the collection animation completes and the coin should return to the pool.
        /// </summary>
        public bool Tick(float deltaTime, Vector3 playerPosition)
        {
            if (IsCollected)
            {
                collectAnimTimer += deltaTime;
                float t = Mathf.Clamp01(collectAnimTimer / CollectAnimDuration);

                Vector3 targetPos = playerPosition + new Vector3(0f, 1.35f, 0.3f);
                transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-24f * deltaTime));

                if (visualTransform != null)
                {
                    float scale = Mathf.Lerp(1.25f, 0.05f, t);
                    visualTransform.localScale = new Vector3(scale, scale, scale);
                    spinAngle += 720f * deltaTime;
                    visualTransform.localRotation = Quaternion.Euler(45f, spinAngle, 45f);
                }

                return collectAnimTimer >= CollectAnimDuration;
            }

            spinAngle += 165f * deltaTime;
            bobPhase += 3.6f * deltaTime;

            float bobY = Mathf.Sin(bobPhase) * 0.11f;
            transform.position = new Vector3(spawnPosition.x, spawnPosition.y + bobY, spawnPosition.z);

            if (visualTransform != null)
            {
                visualTransform.localRotation = Quaternion.Euler(45f, spinAngle, 45f);
            }

            return false;
        }
    }
}
