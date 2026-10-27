using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerCameraController_k : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform _target;
    [SerializeField] private Camera _viewCamera;
    [SerializeField][Min(0f)] private float _targetHeight = 1f;

    [Header("마우스 좌우 회전")]
    [SerializeField][Min(0f)] private float _mouseSensitivity = 3f;
    [SerializeField] private bool _captureCursorOnEnable = true;

    private float _yawAngle;
    private bool _hasFocus;
    private bool _ownsCursor;
    private int _captureFrame = -1;

    public bool CanReadGameplayInput => isActiveAndEnabled && _hasFocus
        && Cursor.lockState == CursorLockMode.Locked && Time.frameCount > _captureFrame;

    private void Awake()
    {
        if (_target == null)
        {
            Debug.LogError("PlayerCameraController_k에 플레이어 Transform을 연결하세요.", this);
            enabled = false;
            return;
        }

        if (_viewCamera == null)
        {
            _viewCamera = Camera.main;
        }

        // 실제 카메라의 시작 방향을 사용해 첫 프레임의 시점 변경을 막습니다.
        Vector3 forward = _viewCamera != null ? _viewCamera.transform.forward : transform.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        _yawAngle = forward.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
            : transform.eulerAngles.y;

        _hasFocus = Application.isFocused;
        transform.SetPositionAndRotation(_target.position + Vector3.up * _targetHeight,
            Quaternion.Euler(0f, _yawAngle, 0f));
    }

    private void OnEnable()
    {
        if (_captureCursorOnEnable && _hasFocus)
        {
            CaptureCursor();
        }
    }

    private void Update()
    {
        if (!_hasFocus || _target == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReleaseCursor();
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CaptureCursor();
            }

            return;
        }

        if (!CanReadGameplayInput)
        {
            return;
        }

        // Mouse X는 프레임 동안의 이동량이므로 deltaTime을 다시 곱하지 않습니다.
        float mouseDelta = Input.GetAxisRaw("Mouse X");
        _yawAngle = Mathf.Repeat(_yawAngle + mouseDelta * _mouseSensitivity, 360f);
        transform.rotation = Quaternion.Euler(0f, _yawAngle, 0f);
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        // PlayerMove.Update가 끝난 위치를 CinemachineBrain보다 먼저 반영합니다.
        transform.position = _target.position + Vector3.up * _targetHeight;
    }

    private void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _ownsCursor = true;
        // 커서를 다시 잡는 클릭은 같은 프레임의 공격/대쉬 입력으로 전달하지 않습니다.
        _captureFrame = Time.frameCount;
    }

    private void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _ownsCursor = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;

        if (!hasFocus && _ownsCursor)
        {
            ReleaseCursor();
        }
    }

    private void OnDisable()
    {
        if (_ownsCursor)
        {
            ReleaseCursor();
        }
    }
}
