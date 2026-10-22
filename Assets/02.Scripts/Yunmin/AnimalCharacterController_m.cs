using UnityEngine;

public class AnimalCharacterController_m : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private CharacterController _characterController;

    private void Awake()
    {
        if(_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            _characterController.Move(moveDirection * _moveSpeed * Time.deltaTime);
        }

    }
}
