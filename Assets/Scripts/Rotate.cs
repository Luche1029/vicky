using UnityEngine;

public class Rotate : MonoBehaviour
{
    [Header("Oscillazioni Testa/Corpo")]
    public float rotationSpeed = 1.0f; // Intensità dell'oscillazione


    private Quaternion initialRotation;

    void Start()
    {

    }

    void Update()
    {

        float rotY = Time.time * rotationSpeed;
        transform.Rotate(Vector3.up * rotY * Time.deltaTime);
    }

}