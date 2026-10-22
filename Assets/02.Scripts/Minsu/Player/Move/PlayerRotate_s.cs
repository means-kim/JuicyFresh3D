using UnityEngine;

public class PlayerRotate_s : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    private float _accumulationX = 0f; // 좌우  회전 각도

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;

        transform.eulerAngles = new Vector3(0f, _accumulationX, 0f);
    }
}