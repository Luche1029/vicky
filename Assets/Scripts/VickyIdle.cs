using UnityEngine;

public class VickyIdle : MonoBehaviour
{
    [Header("Oscillazioni Testa/Corpo")]
    public Transform targetBone; // Trascina qui l'osso del Collo o della Spine1
    public float swayIntensity = 1.0f; // Intensità dell'oscillazione
    public float swaySpeed = 0.5f;     // Velocità dell'oscillazione

    [Header("Respirazione (Spine)")]
    public Transform breathingBone; // Trascina qui l'osso Spine (che fa "gonfiare" il petto)
    public float breathingIntensity = 0.01f; // Espansione del petto
    public float breathingSpeed = 1.2f;    // Velocità del respiro (più veloce dell'oscillazione)

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialBreathingScale;

    void Start()
    {
        if (targetBone != null)
        {
            initialPosition = targetBone.localPosition;
            initialRotation = targetBone.localRotation;
        }
        if (breathingBone != null)
        {
            initialBreathingScale = breathingBone.localScale;
        }
    }

    void Update()
    {
        // 1. Oscillazione Procedurale (Corpo/Testa)
        if (targetBone != null)
        {
            float swayX = Mathf.Sin(Time.time * swaySpeed) * swayIntensity;
            float swayY = Mathf.Sin(Time.time * (swaySpeed * 1.33f)) * (swayIntensity * 0.5f); // Frequenza diversa per Y

            // Applica rotazione leggera (oscilla a destra/sinistra, su/giù)
            targetBone.localRotation = initialRotation * Quaternion.Euler(swayY, swayX, 0);
        }

        // 2. Respirazione (Petto)
        if (breathingBone != null)
        {
            // Espandiamo e contraiamo l'osso Spine leggermente
            float breath = 1.0f + (Mathf.Sin(Time.time * breathingSpeed) * breathingIntensity);
            breathingBone.localScale = new Vector3(initialBreathingScale.x, initialBreathingScale.y, initialBreathingScale.z * breath);
        }
    }
}