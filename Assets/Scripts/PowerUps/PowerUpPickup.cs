using UnityEngine;

namespace CyberRunner.PowerUps
{
    /// <summary>
    /// Pooled in-world collectible power-up beacon with anti-duplication claim guard
    /// and crisp rotation/bob animation driven centrally by PowerUpPoolManager.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PowerUpPickup : MonoBehaviour
    {
        public PowerUpType Type { get; private set; }
        public bool IsCollected { get; private set; }

        private Transform coreVisual;
        private Transform ringVisual;
        private MeshRenderer coreRenderer;
        private Vector3 spawnPosition;
        private float animPhase;

        public void SetVisualReferences(Transform core, Transform ring, MeshRenderer coreMeshRenderer)
        {
            coreVisual = core;
            ringVisual = ring;
            coreRenderer = coreMeshRenderer;
        }

        public void ActivateAt(PowerUpType type, Vector3 worldPosition, Material typeMaterial)
        {
            Type = type;
            IsCollected = false;
            spawnPosition = worldPosition;
            transform.position = worldPosition;
            animPhase = worldPosition.z * 0.25f;

            if (coreRenderer != null && typeMaterial != null)
            {
                coreRenderer.sharedMaterial = typeMaterial;
            }
        }

        public bool TryCollect()
        {
            if (IsCollected)
            {
                return false;
            }

            IsCollected = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            animPhase += deltaTime * 3.2f;
            float bobY = Mathf.Sin(animPhase) * 0.18f;
            transform.position = new Vector3(spawnPosition.x, spawnPosition.y + bobY, spawnPosition.z);

            if (coreVisual != null)
            {
                coreVisual.Rotate(45f * deltaTime, 140f * deltaTime, 25f * deltaTime, Space.Self);
            }

            if (ringVisual != null)
            {
                ringVisual.Rotate(-90f * deltaTime, 120f * deltaTime, 0f, Space.Self);
            }
        }
    }
}
