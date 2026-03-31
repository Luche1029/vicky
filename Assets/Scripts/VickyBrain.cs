using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine.UI;
using System.Linq;
using System;
using UnityEngine.Android;
public class VickyBrain : MonoBehaviour
{    
    private static string N8N_WEBHOOK_URL = "http://100.84.227.69:5678/webhook/vicky-chat";
   
    public VickyVoice vickyVoice;
    public VickyEars vickyEars;

    private string micName;

    private Animator anim;

    void Awake() 
    {
        vickyEars.OnTextReceived += (text) => StartCoroutine(PostToN8n(text));
        
        vickyVoice.OnSpeechFinished += () => {
            anim.SetBool("IsSpeaking", false);
            vickyEars.StartVickyListening();
        };
    }

    IEnumerator Start()
    {

        anim = GetComponent<Animator>();
        #if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            // Aspetta che l'utente clicchi "Consenti"
            yield return new WaitUntil(() => Permission.HasUserAuthorizedPermission(Permission.Microphone));
        }
        #endif

        micName = null; 

        if (Microphone.devices.Count() < 1)
        {
            Debug.LogWarning("nessun microfono trovato");
            anim.SetBool("IsSpeaking", true);
            vickyVoice.Speak("Mi dispiace, non ho rilevato alcun microfono.", () => 
            {
                anim.SetBool("IsSpeaking", false);
            });     
            yield break;
        }              

        micName = Microphone.devices[0];   

        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(micName, out minFreq, out maxFreq);
        Debug.Log($"Microfono: {micName} | Freq: {minFreq}-{maxFreq}");

        var intro = "Ciao, sono Vicky. Chiedi pure.";
        anim.SetBool("IsSpeaking", true);
        vickyVoice.Speak(
            intro,
            () =>
            {
                anim.SetBool("IsSpeaking", false);
                vickyEars.StartVickyListening();                    
            });
        

        InvokeRepeating("ChooseRandomPose", 2f, 4f);
  
    }

    IEnumerator PostToN8n(string query)
    {
        string jsonPayload = "{\"query\":\"" + query + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(N8N_WEBHOOK_URL, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            // Timeout di sicurezza (5 secondi) per non bloccare tutto se n8n è giù
            www.timeout = 5; 

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string response = ParseN8nResponse(www.downloadHandler.text);
                Debug.Log("Vicky ha risposto: " + response);
                vickyVoice.Speak(
                        response,
                        () =>
                        {
                            anim.SetBool("IsSpeaking", false);
                            vickyEars.StartVickyListening();                    
                        });

            }
            else
            {
                Debug.LogError($"Errore: {www.result} - {www.error}");
            }
        }
    }

    // Piccolo helper per leggere la risposta di n8n
    string ParseN8nResponse(string json) {
        return json.Contains("reply") ? json.Split(new string[] { "\"reply\":\"" }, StringSplitOptions.None)[1].Split('"')[0] : json;
    }





    void ChooseRandomPose()
    {
        // Genera un numero tra 0 e 5 (0 è Stand/Idle, 1-4 sono le pose)
        int randomId = UnityEngine.Random.Range(1, 4); 
        anim.SetInteger("Choice", randomId);
        
        // Opzionale: Resetta il parametro dopo un attimo per permettere la ripetizione
        Invoke("ResetParam", 0.5f);
    }

    void ResetParam()
    {
        anim.SetInteger("Choice", 0);
    }

    [System.Serializable]
    public class OpenAIResponse {
        public AIChoice[] choices;
    }

    [System.Serializable]
    public class AIChoice {
        public AIMessage message;
    }

    [System.Serializable]
    public class AIMessage {
        public string content;
    }
}