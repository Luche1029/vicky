using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class UnityJsonLogger
{
    public static void LogObject(object obj, string label = "UNITY LOG")
    {
        // Formatting.Indented crea la struttura leggibile a più righe
        string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
        Debug.Log($"<color=cyan>[{label}]</color>:\n{json}");
    }
}