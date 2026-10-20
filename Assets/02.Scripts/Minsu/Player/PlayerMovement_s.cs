using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement_s : MonoBehaviour
{
    private const float Gravity = -20f;
    private const int MaxJumps = 2;
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int MoveRate = Animator.StringToHash("MoveRate");
    private float _yVelocity;
    private int _jumpCount;

    [Header("스탯")]
    [SerializeField] private float _moveSpeed = 7f;
    [FormerlySerializedAs("_strintSpeed")]
    [SerializeField] private float _sprintSpeed = 12f;
    [SerializeField] private float _jumpPower = 10f;

    [Header("컴포넌트")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Animator _animator;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null) _animator.applyRootMotion = false;
    }

    private void Update()
    {
        Vector3 input = Vector3.ClampMagnitude(new Vector3(
            Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")), 1f);
        Vector3 direction = transform.TransformDirection(input);

        if (_characterController.isGrounded && _yVelocity <= 0f)
        {
            _yVelocity = -1f;
            _jumpCount = 0;
        }
        else
        {
            _yVelocity += Gravity * Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && _jumpCount < MaxJumps)
        {
            _yVelocity = _jumpPower;
            _jumpCount++;
        }

        float speed = Input.GetButton("Sprint") ? _sprintSpeed : _moveSpeed;
        Vector3 velocity = direction * speed;
        velocity.y = _yVelocity;
        CollisionFlags collisions = _characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && _yVelocity > 0f)
            _yVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && _yVelocity < 0f)
        {
            _yVelocity = -1f;
            _jumpCount = 0;
        }

        if (_animator != null)
        {
            // 실제 수평 이동량을 사용해 벽에 막혔을 때 달리기를 멈춘다.
            Vector3 localVelocity = transform.InverseTransformDirection(_characterController.velocity);
            Vector2 movement = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z)
                / Mathf.Max(speed, 0.01f), 1f);
            _animator.SetFloat(MoveX, movement.x, 0.1f, Time.deltaTime);
            _animator.SetFloat(MoveZ, movement.y, 0.1f, Time.deltaTime);
            _animator.SetFloat(MoveRate, Mathf.Lerp(1f, Mathf.Max(speed / 5f, 0.01f), movement.magnitude));
        }
    }
}

