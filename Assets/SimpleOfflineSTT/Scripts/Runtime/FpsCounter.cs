using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class FpsCounter : MonoBehaviour
    {
        [SerializeField]
        TMP_Text _fpsText;

        // Store recent frame times (seconds)
        readonly Queue<float> _frameTimes = new Queue<float>();

        const float SampleDuration = 3.0f; // seconds of history to keep

        float _timeSum = 0f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Add new frame
            _frameTimes.Enqueue(dt);
            _timeSum += dt;

            // Remove old frames until within duration window
            while (_timeSum > SampleDuration && _frameTimes.Count > 0)
            {
                _timeSum -= _frameTimes.Dequeue();
            }

            if (_frameTimes.Count == 0)
            {
                return;
            }

            // FPS
            float avgFrameTime = _timeSum / _frameTimes.Count;
            float fps = 1f / avgFrameTime;

            // Min / max frame time
            float min = float.MaxValue;
            float max = 0f;

            foreach (float t in _frameTimes)
            {
                if (t < min) { min = t; }
                if (t > max) { max = t; }
            }

            // Convert to ms
            float minMs = min * 1000f;
            float maxMs = max * 1000f;

            _fpsText.text =
                $"{Mathf.RoundToInt(fps)} fps\n" +
                $"min: {minMs:0}ms\n" +
                $"max: {maxMs:0}ms";
        }
    }
}
