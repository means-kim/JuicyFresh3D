using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(-10)]
public class LockOnCamera_s : MonoBehaviour
{
    [SerializeField] private PlayerLockOn_s _player;
    [SerializeField] private CinemachineCamera _lockCamera;
    [SerializeField] private Transform _followPoint;
    [SerializeField] private Transform _aimPoint;

    private void Update()
    {
        RefreshCamera();
    }

    public void RefreshCamera()
    {
        if (_lockCamera == null) return;

        bool active = _player != null && _player.isActiveAndEnabled && _player.IsLockedOn;

        _lockCamera.Priority = active ? 20 : -10;

        if (!active) return;

        _followPoint.position = _player.transform.position + Vector3.up * 1.2f;
        _aimPoint.position = Vector3.Lerp(_player.transform.position, _player.Target.position, 0.5f)
                             + Vector3.up * 1.2f;

        Vector3 direction = _player.Target.position - _player.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            _followPoint.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void OnDisable()
    {
        if (_lockCamera != null)
        {
            _lockCamera.Priority = -10;
        }
    }
}