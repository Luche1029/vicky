using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System;
using UnityEngine.UI;
using SimpleOfflineSTT;

public class VickyEars : MonoBehaviour
{
    [SerializeField]
    STTManager _sttManager;
    [SerializeField]
    STTMicrophone _sttMicrophone;

    private AudioClip recording;
    private string micName;
    private const int HEADER_SIZE = 44;

    public Action<string> OnTextReceived;
    public Action<byte[]> OnAudioRawReceived;

    public void SetMicName(string name)
    {
        micName = name;
    } 

    public void StartRecording()
    {
        recording = Microphone.Start(micName, false, 10, 44100);
    }

    public void StopAndSend()
    {
        int lastPos = Microphone.GetPosition(micName); 
        Microphone.End(micName);

        if (lastPos <= 0)
        {
            Debug.LogWarning("Registrazione troppo breve o posizione non valida.");
            return;
        }

        float[] tempSamples = new float[recording.samples * recording.channels];
        recording.GetData(tempSamples, 0);

        float[] finalSamples = new float[lastPos * recording.channels];
        Array.Copy(tempSamples, finalSamples, finalSamples.Length);

        byte[] wavData = SaveWav.GetWav(finalSamples, recording.channels, recording.frequency);
        OnAudioRawReceived?.Invoke(wavData);
    }

    public void StartVickyListening()
    {
        _sttMicrophone.StartStreaming(
            STTMicrophone.StreamingMode.PauseDetection,
            false, // No timestamps per semplicità
            OnVickyResult);
    }

    void OnVickyResult(string committedText, string draftText, int chunkIndex)
    {
        if (string.IsNullOrEmpty(draftText) && !string.IsNullOrEmpty(committedText))
        {            
            _sttMicrophone.StopRecording(); 
            
            OnTextReceived?.Invoke(committedText);
        }
    }
}

[Serializable] public class WhisperResponse { public string text; }