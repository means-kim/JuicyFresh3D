using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFX_UNI_Rotate : MonoBehaviour
{
    public float RotationSpeed = 120.0f;

    void Update()
    {
        transform.Rotate(0, RotationSpeed * Time.deltaTime, 0);
    }
}
