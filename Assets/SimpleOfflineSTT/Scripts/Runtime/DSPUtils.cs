using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public static class DSPUtils
    {
        public static float[] ExtractMonoSamples(float[] audioData, int channels, int samples)
        {
            if (channels == 1)
            {
                return audioData;
            }

            float[] mono = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += audioData[i * channels + c];
                }
                mono[i] = sum / channels;
            }

            return mono;
        }

        public static float[] Resample(float[] input, int srcRate, int dstRate, bool logging)
        {
            if (srcRate == dstRate)
            {
                return input;
            }

            float ratio = (float)dstRate / srcRate;
            int len = Mathf.RoundToInt(input.Length * ratio);
            float[] output = new float[len];

            for (int i = 0; i < len; i++)
            {
                float pos = i / ratio;
                int i0 = Mathf.FloorToInt(pos);
                int i1 = Mathf.Min(i0 + 1, input.Length - 1);
                float t = pos - i0;

                output[i] = Mathf.Lerp(input[i0], input[i1], t);
            }

            if (logging)
            {
                Debug.Log($"[DSPUtils] Resampled to {dstRate}Hz");
            }

            return output;
        }

        public static float CalculateRMS(float[] samples)
        {
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Sqrt(sum / samples.Length);
        }

        /// <summary>
        /// RMS over a contiguous frame without allocations.
        /// Thread-safe (pure C#).
        /// </summary>
        public static float ComputeRmsFrame(float[] audio, int start, int count)
        {
            if (audio == null)
            {
                return 0f;
            }

            if (audio.Length == 0)
            {
                return 0f;
            }

            if (count <= 0)
            {
                return 0f;
            }

            start = ClampInt(start, 0, audio.Length);
            int end = ClampInt(start + count, 0, audio.Length);

            int n = end - start;
            if (n <= 0)
            {
                return 0f;
            }

            double sumSq = 0.0;
            for (int i = start; i < end; i++)
            {
                float s = audio[i];
                sumSq += (double)s * (double)s;
            }

            return (float)Math.Sqrt(sumSq / n);
        }

        /// <summary>
        /// Thread-friendly: returns a materialized list of chunk ranges (startSample, lengthSamples).
        /// No Unity API usage, no logging, safe to run in Task.Run.
        /// </summary>
        public static List<(int startSample, int lengthSamples)> SplitOnPauses(
            float[] audio,
            int sampleRate,
            int minCutSeconds,
            int maxCutSeconds,
            float pauseRmsThreshold,
            float pauseDurationSeconds,
            int analysisFrameMs)
        {
            List<(int startSample, int lengthSamples)> ranges = new List<(int startSample, int lengthSamples)>();

            if (audio == null)
            {
                return ranges;
            }

            if (audio.Length == 0)
            {
                return ranges;
            }

            if (sampleRate <= 0)
            {
                return ranges;
            }

            if (minCutSeconds < 1)
            {
                minCutSeconds = 1;
            }

            if (maxCutSeconds <= minCutSeconds)
            {
                maxCutSeconds = minCutSeconds + 1;
            }

            int minCutSamples = minCutSeconds * sampleRate;
            int maxCutSamples = maxCutSeconds * sampleRate;

            if (analysisFrameMs < 1)
            {
                analysisFrameMs = 1;
            }

            double frameSeconds = analysisFrameMs / 1000.0;
            int frameSize = (int)Math.Round(sampleRate * frameSeconds);
            if (frameSize < 1)
            {
                frameSize = 1;
            }

            int requiredQuietFrames = (int)Math.Ceiling(pauseDurationSeconds / frameSeconds);
            if (requiredQuietFrames < 1)
            {
                requiredQuietFrames = 1;
            }

            int start = 0;

            while (start < audio.Length)
            {
                int remaining = audio.Length - start;

                if (remaining <= maxCutSamples)
                {
                    ranges.Add((start, remaining));
                    break;
                }

                int minEnd = start + minCutSamples;
                int maxEnd = start + maxCutSamples;

                if (minEnd > audio.Length)
                {
                    minEnd = audio.Length;
                }

                if (maxEnd > audio.Length)
                {
                    maxEnd = audio.Length;
                }

                int cut = -1;

                int i = minEnd;
                int quietFrames = 0;
                int quietRunStart = -1;

                while (i + frameSize <= maxEnd)
                {
                    float rms = ComputeRmsFrame(audio, i, frameSize);

                    if (rms < pauseRmsThreshold)
                    {
                        if (quietFrames == 0)
                        {
                            quietRunStart = i;
                        }

                        quietFrames++;

                        if (quietFrames >= requiredQuietFrames)
                        {
                            cut = ClampInt(quietRunStart, start + 1, maxEnd);
                            break;
                        }
                    }
                    else
                    {
                        quietFrames = 0;
                        quietRunStart = -1;
                    }

                    i += frameSize;
                }

                if (cut < 0)
                {
                    cut = maxEnd;
                }

                int len = cut - start;
                if (len <= 0)
                {
                    cut = maxEnd;
                    len = cut - start;
                }

                ranges.Add((start, len));
                start = cut;
            }

            return ranges;
        }

        static int ClampInt(int v, int min, int max)
        {
            if (v < min)
            {
                return min;
            }

            if (v > max)
            {
                return max;
            }

            return v;
        }
    }
}