using UnityEngine;

namespace CyberRunner.Obstacles
{
    /// <summary>
    /// Runtime obstacle component carrying collision clearance rules and lightweight motion
    /// (moving cyber trains, oscillating security drones, rotating industrial machinery).
    /// Driven centrally by ObstaclePoolManager to avoid per-object Update() overhead on Android.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ObstacleController : MonoBehaviour
    {
        public ObstacleType Type { get; private set; }
        public string DisplayName { get; private set; }
        public int LaneIndex { get; private set; }
        public bool CanJumpOver { get; private set; }
        public bool CanSlideUnder { get; private set; }
        public float ClearanceTopY { get; private set; }
        public float ClearanceBottomY { get; private set; }
        public float HalfLengthZ { get; private set; }
        public bool CollisionResolved { get; private set; }

        private float approachSpeed;
        private float activationDistance;
        private bool motionActivated;

        private float hoverAmplitude;
        private float hoverFrequency;
        private float hoverPhase;
        private float baseY;

        private Transform spinnerPivot;
        private float spinSpeedDegrees;

        public void InitializeSpec(
            ObstacleType type,
            string displayName,
            bool canJumpOver,
            bool canSlideUnder,
            float clearanceBottomY,
            float clearanceTopY,
            float halfLengthZ,
            float approachSpeedZ,
            float motionActivationDist,
            float droneHoverAmp,
            float droneHoverFreq)
        {
            Type = type;
            DisplayName = displayName;
            CanJumpOver = canJumpOver;
            CanSlideUnder = canSlideUnder;
            ClearanceBottomY = clearanceBottomY;
            ClearanceTopY = clearanceTopY;
            HalfLengthZ = halfLengthZ;
            approachSpeed = approachSpeedZ;
            activationDistance = motionActivationDist;
            hoverAmplitude = droneHoverAmp;
            hoverFrequency = droneHoverFreq;
        }

        public void SetSpinnerPivot(Transform pivot, float degreesPerSecond)
        {
            spinnerPivot = pivot;
            spinSpeedDegrees = degreesPerSecond;
        }

        public void SetApproachSpeed(float speedZ)
        {
            approachSpeed = Mathf.Max(0f, speedZ);
        }

        public void ActivateAt(int laneIndex, Vector3 worldPosition)
        {
            LaneIndex = laneIndex;
            transform.position = worldPosition;
            baseY = worldPosition.y;
            CollisionResolved = false;
            motionActivated = false;
            hoverPhase = (worldPosition.z * 0.35f) + (laneIndex * 1.2f);
        }

        public void MarkCollisionResolved()
        {
            CollisionResolved = true;
        }

        /// <summary>
        /// Advances moving/rotating obstacles without per-object MonoBehaviour.Update overhead.
        /// </summary>
        public void Tick(float deltaTime, float playerZ)
        {
            Vector3 pos = transform.position;

            if (approachSpeed > 0f)
            {
                if (!motionActivated && (pos.z - playerZ) <= activationDistance)
                {
                    motionActivated = true;
                }

                if (motionActivated)
                {
                    pos.z -= approachSpeed * deltaTime;
                }
            }

            if (hoverAmplitude > 0f)
            {
                hoverPhase += deltaTime * hoverFrequency;
                pos.y = baseY + (Mathf.Sin(hoverPhase) * hoverAmplitude);
            }

            transform.position = pos;

            if (spinnerPivot != null && Mathf.Abs(spinSpeedDegrees) > 0.1f)
            {
                spinnerPivot.Rotate(0f, spinSpeedDegrees * deltaTime, 0f, Space.Self);
            }
        }
    }
}
