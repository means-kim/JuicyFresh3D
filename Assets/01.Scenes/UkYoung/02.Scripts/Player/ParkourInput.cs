using UnityEngine;
using UnityEngine.InputSystem;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputActionMap _playerActions;
        private InputAction _jumpAction;
        private InputAction _lookAction;
        private InputAction _moveAction;
        private InputAction _sprintAction;

        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 Look => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool IsSprinting => _sprintAction?.IsPressed() ?? false;
        public bool WasJumpPressed => _jumpAction?.WasPressedThisFrame() ?? false;

        public void Initialize(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
            CacheActions();
        }

        private void Awake()
        {
            CacheActions();
        }

        private void OnEnable()
        {
            CacheActions();
            _playerActions?.Enable();
        }

        private void OnDisable()
        {
            _playerActions?.Disable();
        }

        private void CacheActions()
        {
            if (_inputActions == null)
            {
                return;
            }

            _playerActions = _inputActions.FindActionMap("Player", true);
            _moveAction = _playerActions.FindAction("Move", true);
            _lookAction = _playerActions.FindAction("Look", true);
            _jumpAction = _playerActions.FindAction("Jump", true);
            _sprintAction = _playerActions.FindAction("Sprint", true);

            if (isActiveAndEnabled)
            {
                _playerActions.Enable();
            }
        }
    }
}
