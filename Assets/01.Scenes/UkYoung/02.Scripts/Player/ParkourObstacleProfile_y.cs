using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourObstacleProfile_y : MonoBehaviour
    {
        [SerializeField, Range(1, 4)] private int _jumpTier = 1;

        public int JumpTier => _jumpTier;

        public void SetJumpTier(int jumpTier)
        {
            _jumpTier = Mathf.Clamp(jumpTier, 1, 4);
        }
    }
}
