using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;

// Classe di supporto per leggere il JSON di Inworld
[Serializable]
public class InworldResponse {
    public string audioContent; 
}

public class VickyVoice : MonoBehaviour
{

    public Animator anim;

    public AudioSource vickyAudioSource;

    public System.Action OnSpeechFinished;
        
    private string base64ApiKey = "c2NBamFzeW1JTWlFVWRlQURpYmhOdW1QYU5XcXFZeW46NFpPTllXbTR2SjVtNEJKZzBLNGd6ck9kanBPYWh3dHMxWlpQRU9EUFhtNkNXRUNNbzhwWmx4bzFJRXJLTWt2ZQ==";


    public void Speak(string text, Action onEnd = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            StartCoroutine(PostTTS(text, onEnd));
        }
    }

    IEnumerator PostTTS(string text, Action onEnd)
    {
        string url = "https://api.inworld.ai/tts/v1/voice"; 
        
        
        string jsonPayload = "{" +
            "\"text\": \"" + text + "\"," +
            "\"modelId\": \"inworld-tts-1.5-max\"," +
            "\"voiceId\": \"Orietta\"," +
            "\"timestampType\": \"WORD\"," +
            "\"audioConfig\": {\"audioEncoding\": \"LINEAR16\"}" +
        "}";        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            
            www.SetRequestHeader("Content-Type", "application/json"); // [cite: 49]
            www.SetRequestHeader("Authorization", "Basic " + base64ApiKey); // [cite: 48]

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {

                // 1. Decodifica il JSON
                var rawResponse = www.downloadHandler.text;
                InworldResponse response = JsonUtility.FromJson<InworldResponse>(rawResponse);
                
                var details = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawResponse);    
 
                byte[] audioBytes = Convert.FromBase64String(response.audioContent);
                yield return StartCoroutine(PlayAudioCoroutine(audioBytes, onEnd));
            }
            else
            {
                Debug.LogError("Errore Inworld: " + www.error);
                onEnd?.Invoke();
            }
        }
    }

    IEnumerator PlayAudioCoroutine(byte[] audioBytes, Action onEnd)
    {
        string tempPath = Application.persistentDataPath + "/vicky_voice.wav";
        System.IO.File.WriteAllBytes(tempPath, audioBytes);

        using (UnityWebRequest audioLoader = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.WAV))
        {
            yield return audioLoader.SendWebRequest();
            if (audioLoader.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(audioLoader);
                AudioSource source = GetComponent<AudioSource>();
                source.clip = clip;
                source.Play();

                // Aspetta finché l'audio è in riproduzione
                while (source.isPlaying) { 
                    InvokeRepeating("ChooseRandomPose", 2f, 4f);
                    yield return null; 
                }
                onEnd?.Invoke();
            }
        }
    }

    IEnumerator LoadAudioFromFile(string path)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                vickyAudioSource.clip = DownloadHandlerAudioClip.GetContent(www);
                vickyAudioSource.Play();
            }
        }
    }

    [System.Serializable]
    public class InworldResponse {
        public string audioContent;
        public TimestampInfo timestampInfo; // Contenitore principale
    }

    [System.Serializable]
    public class TimestampInfo {
        public WordAlignment wordAlignment;
    }

    [System.Serializable]
    public class WordAlignment {
        public PhoneticDetail[] phoneticDetails; // Qui dentro ci sono i visemi!
    }

    [System.Serializable]
    public class PhoneticDetail {
        public string viseme;           // Il simbolo del viseme (es. "aa", "O")
        public float startTimeSeconds;  // Quando iniziare il movimento
    }


}