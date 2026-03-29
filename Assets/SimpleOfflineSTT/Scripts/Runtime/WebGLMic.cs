using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class WebGLMic : MonoBehaviour
    {
        List<float> _buffer = new List<float>();
        bool _isRecording = false;

        int _sampleRate = 0;
        bool _hasSampleRate = false;

#if UNITY_WEBGL
        [DllImport("__Internal")]
        static extern void InitWebGLMic();

        [DllImport("__Internal")]
        static extern void StartWebGLMic();

        [DllImport("__Internal")]
        static extern void StopWebGLMic();
#endif

        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InitWebGLMic();
#endif
        }

        public int SampleRate
        {
            get
            {
                if (!_hasSampleRate)
                {
                    Debug.LogWarning("[WebGLMic] Sample rate not received yet, defaulting to 48000");
                    return 48000;
                }
                return _sampleRate;
            }
        }

        public void OnSampleRate(string rate)
        {
            if (int.TryParse(rate, out int sr))
            {
                _sampleRate = sr;
                _hasSampleRate = true;

                Debug.Log($"[WebGLMic] Browser sample rate: {_sampleRate} Hz");
            }
            else
            {
                Debug.LogError($"[WebGLMic] Failed to parse sample rate: {rate}");
            }
        }

        public void StartRecording()
        {
            _buffer.Clear();
            _isRecording = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            StartWebGLMic();
#endif
        }

        public void EndRecording()
        {
            _isRecording = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            StopWebGLMic();
#endif
        }

        // Equivalent to Microphone.GetPosition
        public int GetPosition()
        {
            return _buffer.Count;
        }

        // Read samples written since last position
        public int ReadNewSamples(int lastSamplePos, float[] output)
        {
            int available = _buffer.Count - lastSamplePos;
            if (available <= 0)
            {
                return 0;
            }

            int count = Math.Min(available, output.Length);
            _buffer.CopyTo(lastSamplePos, output, 0, count);
            return count;
        }

        // Called from JS
        public void OnAudioChunk(string base64)
        {
            if (!_isRecording)
            {
                return;
            }

            byte[] bytes = Convert.FromBase64String(base64);

            // PCM16 = 2 bytes per sample
            int sampleCount = bytes.Length / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                short pcm = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                float sample = pcm / 32768f;
                _buffer.Add(sample);
            }

            //Debug.Log($"[WebGLMic] Got {sampleCount} new samples");
        }

        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
