using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public sealed class AudioTranscriptionQueue
    {
        readonly Func<float[], bool, Task<string>> _transcribeAsync;
        readonly Func<bool> _isLogging;

        Task _runnerTask;
        bool _isRunning;

        float[] _pendingAudio;
        bool _pendingTrim;
        bool _pendingIsCommit;
        Action<string, int, bool> _pendingCallback;

        int _emitIndex;
        int _generation;

        long _lastResponseTimeMs;

        public AudioTranscriptionQueue(
            Func<float[], bool, Task<string>> transcribeAsync,
            Func<bool> isLogging = null)
        {
            _transcribeAsync = transcribeAsync ?? throw new ArgumentNullException(nameof(transcribeAsync));
            _isLogging = isLogging ?? (() => false);
        }

        public long LastResponseTimeMs => _lastResponseTimeMs;

        public void Reset()
        {
            _generation++;
            _emitIndex = 0;

            _pendingAudio = null;
            _pendingCallback = null;
        }

        public void SetEmitIndex(int value)
        {
            _emitIndex = value;
        }

        public int GetEmitIndex()
        {
            return _emitIndex;
        }

        public bool IsBusy()
        {
            return _isRunning;
        }

        public void EnqueueLatest(
            float[] audio,
            bool trimBracketTags,
            bool isCommit,
            Action<string, int, bool> onResult)
        {
            if (audio == null || audio.Length == 0)
            {
                return;
            }

            if (onResult == null)
            {
                throw new ArgumentNullException(nameof(onResult));
            }

            _pendingAudio = audio;
            _pendingTrim = trimBracketTags;
            _pendingIsCommit = isCommit;
            _pendingCallback = onResult;

            if (_isRunning)
            {
                if (_isLogging())
                {
                    Debug.Log("[AudioTranscriptionQueue] Busy; replaced pending audio.");
                }

                return;
            }

            int gen = _generation;
            _runnerTask = RunnerLoopAsync(gen);
        }

        public async Task FlushAndStopAsync()
        {
            _generation++;

            Task t = _runnerTask;
            _pendingAudio = null;
            _pendingCallback = null;

            if (t != null)
            {
                try
                {
                    await t;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        async Task RunnerLoopAsync(int gen)
        {
            _isRunning = true;

            try
            {
                while (true)
                {
                    if (gen != _generation)
                    {
                        return;
                    }

                    float[] audio = _pendingAudio;
                    bool trim = _pendingTrim;
                    bool isCommit = _pendingIsCommit;
                    Action<string, int, bool> cb = _pendingCallback;

                    _pendingAudio = null;
                    _pendingCallback = null;

                    if (audio == null || audio.Length == 0 || cb == null)
                    {
                        return;
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string text = await _transcribeAsync(audio, trim);
                    _lastResponseTimeMs = sw.ElapsedMilliseconds;

                    if (gen != _generation)
                    {
                        return;
                    }

                    if (text == null)
                    {
                        text = "";
                    }

                    cb.Invoke(text, _emitIndex, isCommit);
                    _emitIndex++;

                    if (_pendingAudio == null)
                    {
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _isRunning = false;
            }
        }
    }
}