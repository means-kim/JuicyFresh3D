using UnityEngine;

public class PlayerRotate_s : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _rotationSpeed = 720f;

    private PlayerMovement_s _movement;
    private Health_s _health;
    private Camera _camera;
    private KatanaChargeSkill_s _charge;
    private PlayerLockOn_s _lockOn;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement_s>();
        _charge = GetComponent<KatanaChargeSkill_s>();
        _health = GetComponent<Health_s>();
        _lockOn = GetComponent<PlayerLockOn_s>();
    }

    private void Update()
    {
        if (_lockOn != null && _lockOn.IsLockedOn)
        {
            FaceWorldPoint(_lockOn.Target.position, Time.deltaTime);
            return;
        }
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera == null || !Application.isFocused) return;
        if (!_camera.pixelRect.Contains(Input.mousePosition)) return;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        // 캐릭터나 장애물에 가려져도 방향이 튀지 않도록 수평면을 사용한다.
        var plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float distance)) FaceWorldPoint(ray.GetPoint(distance), Time.deltaTime);
    }

    public void FaceWorldPoint(Vector3 point, float deltaTime)
    {
        if (!isActiveAndEnabled || deltaTime <= 0f
            || (_charge != null && _charge.IsDirectionLocked)
            || (_movement != null && (_movement.IsActionBlocked
                || _movement.CurrentState == MovementState.ChargingAttack)) || (_health != null && _health.IsDead))
        {
            return;
        }
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        // 마우스 이동량이 아니라 초당 회전 속도에 deltaTime을 적용한다.
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), _rotationSpeed * deltaTime);
    }
}
