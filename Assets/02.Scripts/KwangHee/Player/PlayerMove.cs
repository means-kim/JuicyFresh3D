using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 3f;

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new(h, 0f, v);
        direction.Normalize();

        transform.position += direction * _moveSpeed * Time.deltaTime;
    }
}