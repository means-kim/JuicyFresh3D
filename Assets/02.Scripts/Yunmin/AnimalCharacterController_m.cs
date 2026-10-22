using UnityEngine;

public class AnimalCharacterController_m : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PlayerEffectController_m _playerEffectController;
    [SerializeField] private PostProcessEffectController_m _postProcessEffectController;

    private void Awake()
    {
        if(_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }
        if(_playerEffectController == null)
        {
            _playerEffectController = GetComponent<PlayerEffectController_m>();
        }
        if(_postProcessEffectController == null)
        {
            _postProcessEffectController = FindAnyObjectByType<PostProcessEffectController_m>();
        }
    }

    private void Update()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            _characterController.Move(moveDirection * _moveSpeed * Time.deltaTime);
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            _playerEffectController.PlayEffect();
            _postProcessEffectController.PlayEffect();
        }
    }
}
