using UnityEngine;

public class MicCheck : MonoBehaviour
{
    void Start()
    {
        foreach (var device in Microphone.devices)
        {
            Debug.Log("Microfono rilevato: " + device);
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("NESSUN MICROFONO TROVATO! Controlla le impostazioni di sistema.");
        }
    }
}