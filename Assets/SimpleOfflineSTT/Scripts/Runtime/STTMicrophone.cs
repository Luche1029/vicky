using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class STTMicrophone : MonoBehaviour
    {
        [SerializeField, Tooltip("Reference to the Speech-To-Text Manager")]
        STTManager _sttManager;

        [SerializeField, Tooltip("Reference to a WebGL-compatible Microphone class. Not needed on other platforms.")]
        WebGLMic _webGLMic;

        [Header("Pause Detection Settings")]
        [SerializeField, Tooltip("RMS threshold for silence")]
        float _silenceThreshold = 0.03f;

        [SerializeField, Tooltip("Amount of quiet time considered as a 'pause'")]
        float _quietTimeForPause = 0.5f;

        [Header("Realtime Settings")]
        [SerializeField, Tooltip("How often to attempt a draft transcription while speaking (seconds). Lower = more responsive, higher = cheaper.")]
        float _previewIntervalSeconds = 0.5f;

        [SerializeField, Tooltip("Transcribe each word as well as each chunk - even more responsive, but harder to read as it re-writes itself.")]
        bool _previewWordByWord = false;

        [SerializeField, Tooltip("Minimum audio required before draft starts emitting (seconds).")]
        float _previewMinAudioSeconds = 0.6f;

        [SerializeField, Tooltip("If true, only emit when the displayed text changes.")]
        bool _emitOnlyIfChanged = false;

        [Header("Max chunk time")]
        [SerializeField, Tooltip("Maximum amount of time before we force a commit, even without a pause.")]
        float _maxChunkTime = 29.9f;

        // Streaming callback: (committedText, draftText, index)
        Action<string, string, int> _onStreamResult;
        int _streamResultIndex = 0;

        // General State
        string _currentMicDevice;
        AudioClip _loopingClip;
        readonly List<float> _segmentAudio = new List<float>(); // audio since last commit
        int _lastSamplePos = 0;

        bool _isRecording = false;
        bool _isStreaming = false;

        float _lastRms = 0.0f;
        long _lastResponseTimeMs = 0;

        public enum StreamingMode
        {
            None = 0,
            PauseDetection = 1, // cheaper: only commits on pauses/max-chunk
            Realtime = 2        // responsive: draft while speaking + commits on pauses/max-chunk
        }

        StreamingMode _streamingMode = StreamingMode.None;

        bool _addTimestamps = false;

        // VAD State
        float _timeSinceLastSpeech = 0f;
        bool _hasSpeechInSegment = false;

        // Text state
        string _committedText = "";
        string _draftText = "";
        string _lastDisplayedText = "";

        // Realtime preview timing
        float _previewTimer = 0f;
        int _lastPreviewSampleCount = 0;

        // Queue processor (latest-wins, serialized)
        AudioTranscriptionQueue _queue;

        // Prevent drafts from overwriting/dropping a commit request in a "latest wins" queue.
        bool _commitQueuedOrInFlight = false;

        // Manual streaming timestamps (Whisper-style)
        long _committedSampleCount = 0;
        bool _pendingCommitHasTimestamp = false;
        float _pendingCommitTimestampSeconds = 0.0f;

        void Start()
        {
#if UNITY_WEBGL
            Debug.Assert(_webGLMic != null, $"{nameof(_webGLMic)} is null in {nameof(STTMicrophone)}");
            _currentMicDevice = _webGLMic.name;
            _webGLMic.gameObject.SetActive(true);
#else
            if (Microphone.devices.Length > 0)
            {
                _currentMicDevice = Microphone.devices[0];
                Debug.Log($"[STTMicrophone] Using device: {_currentMicDevice}");
            }
            else
            {
                Debug.LogError("[STTMicrophone] No microphone found!");
            }
#endif

            _queue = new AudioTranscriptionQueue(TranscribeAudioAsync, _sttManager.IsLogging);
        }

        public void SetMicDevice(string micDevice)
        {
            _currentMicDevice = micDevice;
            Debug.Log($"[STTMicrophone] Now using device: {_currentMicDevice}");
        }

        void Update()
        {
            if (!_isRecording)
            {
                return;
            }

            ReadMicIntoSegmentBuffer();

            if (!_isStreaming)
            {
                return;
            }

            ProcessPauseDetectionAndMaxChunk();

            if (_streamingMode == StreamingMode.Realtime)
            {
                ProcessRealtimeDraft();
            }
        }

        void ReadMicIntoSegmentBuffer()
        {
#if UNITY_WEBGL
            int currentPos = _webGLMic.GetPosition();
#else
            int currentPos = Microphone.GetPosition(_currentMicDevice);
#endif
            if (currentPos == _lastSamplePos)
            {
                return;
            }

            int samplesToRead;

            if (currentPos > _lastSamplePos)
            {
                samplesToRead = currentPos - _lastSamplePos;
            }
            else
            {
                samplesToRead = (_loopingClip.samples - _lastSamplePos) + currentPos;
            }

            float[] newSamples = new float[samplesToRead];

#if UNITY_WEBGL
            int read = _webGLMic.ReadNewSamples(_lastSamplePos, newSamples);
#else
            int read = GetDataFromClip(_loopingClip, _lastSamplePos, newSamples);
#endif

            if (read > 0)
            {
                _segmentAudio.AddRange(newSamples);
                _lastSamplePos = currentPos;
                _lastRms = DSPUtils.CalculateRMS(newSamples);
            }
        }

        int GetDataFromClip(AudioClip clip, int startPos, float[] output)
        {
            int bufferLen = clip.samples;
            int samplesToRead = output.Length;

            if (startPos + samplesToRead <= bufferLen)
            {
                clip.GetData(output, startPos);
            }
            else
            {
                int endReadLen = bufferLen - startPos;
                float[] endPart = new float[endReadLen];
                clip.GetData(endPart, startPos);

                int startReadLen = samplesToRead - endReadLen;
                float[] startPart = new float[startReadLen];
                clip.GetData(startPart, 0);

                Array.Copy(endPart, 0, output, 0, endReadLen);
                Array.Copy(startPart, 0, output, endReadLen, startReadLen);
            }

            return samplesToRead;
        }

        // -------------------------
        // PAUSE DETECTION + MAX CHUNK -> COMMIT
        // -------------------------

        void ProcessPauseDetectionAndMaxChunk()
        {
            int sampleRate = GetSampleRate();

            float secondsInSegment = _segmentAudio.Count / (float)sampleRate;
            if (secondsInSegment >= _maxChunkTime)
            {
                CommitCurrentSegment("max-chunk");
                return;
            }

            if (_lastRms > _silenceThreshold)
            {
                _timeSinceLastSpeech = 0f;
                _hasSpeechInSegment = true;
                return;
            }

            if (_hasSpeechInSegment)
            {
                _timeSinceLastSpeech += Time.deltaTime;

                bool silenceLongEnough = _timeSinceLastSpeech >= _quietTimeForPause;
                bool hasEnoughAudio = _segmentAudio.Count >= Mathf.FloorToInt(sampleRate * 0.4f);

                if (silenceLongEnough && hasEnoughAudio)
                {
                    CommitCurrentSegment("pause");
                    _timeSinceLastSpeech = 0f;
                    _hasSpeechInSegment = false;
                }
            }
        }

        void CommitCurrentSegment(string reason)
        {
            if (_onStreamResult == null)
            {
                return;
            }

            if (_segmentAudio.Count == 0)
            {
                return;
            }

            int sampleRate = GetSampleRate();

            // Timestamp is the start-time of this segment within the overall recording session.
            if (_addTimestamps)
            {
                _pendingCommitTimestampSeconds = _committedSampleCount / (float)sampleRate;
                _pendingCommitHasTimestamp = true;
            }
            else
            {
                _pendingCommitHasTimestamp = false;
                _pendingCommitTimestampSeconds = 0.0f;
            }

            float[] audio = _segmentAudio.ToArray();
            _segmentAudio.Clear();

            // Advance "committed time" by the audio duration we just committed.
            _committedSampleCount += audio.Length;

            _previewTimer = 0f;
            _lastPreviewSampleCount = 0;

            _draftText = "";

            if (_sttManager != null && _sttManager.IsLogging())
            {
                Debug.Log($"[STTMicrophone] Commit segment ({reason}) {audio.Length / (float)sampleRate:0.00}s");
            }

            _commitQueuedOrInFlight = true;

            _queue.EnqueueLatest(
                audio,
                trimBracketTags: true,
                isCommit: true,
                onResult: OnQueueResult);
        }

        // -------------------------
        // REALTIME DRAFT
        // -------------------------

        void ProcessRealtimeDraft()
        {
            if (_onStreamResult == null)
            {
                return;
            }

            if (_commitQueuedOrInFlight)
            {
                return;
            }

            // Only draft while actively speaking (cheaper + avoids silence churn)
            if (!_hasSpeechInSegment)
            {
                return;
            }

            int sampleRate = GetSampleRate();

            int minSamples = Mathf.FloorToInt(_previewMinAudioSeconds * sampleRate);
            if (_segmentAudio.Count < minSamples)
            {
                return;
            }

            _previewTimer += Time.deltaTime;
            if (_previewTimer < _previewIntervalSeconds)
            {
                return;
            }

            _previewTimer = 0f;

            if (_segmentAudio.Count <= _lastPreviewSampleCount)
            {
                return;
            }

            _lastPreviewSampleCount = _segmentAudio.Count;

            float[] audio = _segmentAudio.ToArray();

            _queue.EnqueueLatest(
                audio,
                trimBracketTags: true,
                isCommit: false,
                onResult: OnQueueResult);
        }

        // -------------------------
        // QUEUE RESULT -> TEXT STATE -> UI OUTPUT
        // -------------------------

        void OnQueueResult(string text, int index, bool isCommit)
        {
            _lastResponseTimeMs = _queue.LastResponseTimeMs;

            if (isCommit)
            {
                _commitQueuedOrInFlight = false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                EmitDisplay();
                return;
            }

            string trimmed = text.Trim();

            if (isCommit)
            {
                // Prefix a manual Whisper-style timestamp at the start of the committed segment.
                if (_pendingCommitHasTimestamp)
                {
                    string ts = FormatWhisperTimestamp(_pendingCommitTimestampSeconds);

                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        trimmed = ts + " " + trimmed;
                    }
                    else
                    {
                        trimmed = ts;
                    }
                }

                _pendingCommitHasTimestamp = false;
                _pendingCommitTimestampSeconds = 0.0f;

                if (!string.IsNullOrEmpty(_committedText))
                {
                    if (!_committedText.EndsWith(" ", StringComparison.Ordinal))
                    {
                        _committedText += " ";
                    }
                }

                _committedText += trimmed;
                _draftText = "";
            }
            else
            {
                _draftText = trimmed;
            }

            EmitDisplay();
        }

        void EmitDisplay()
        {
            if (_onStreamResult == null)
            {
                return;
            }

            string display;

            if (_streamingMode == StreamingMode.PauseDetection)
            {
                display = _committedText;
            }
            else
            {
                if (string.IsNullOrEmpty(_committedText))
                {
                    display = _draftText;
                }
                else if (string.IsNullOrEmpty(_draftText))
                {
                    display = _committedText;
                }
                else
                {
                    display = _committedText + " " + _draftText;
                }
            }

            display = (display ?? "").Trim();

            if (_emitOnlyIfChanged)
            {
                if (string.Equals(display, _lastDisplayedText, StringComparison.Ordinal))
                {
                    return;
                }
            }

            _lastDisplayedText = display;

            _onStreamResult.Invoke(_committedText, _draftText, _streamResultIndex);
            _streamResultIndex++;
        }

        static string FormatWhisperTimestamp(float seconds)
        {
            if (seconds < 0.0f)
            {
                seconds = 0.0f;
            }

            return "<|" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "|>";
        }

        // -------------------------
        // CORE TRANSCRIPTION (for queue)
        // -------------------------

        async Task<string> TranscribeAudioAsync(float[] audio, bool trimBracketTags)
        {
            if (_sttManager == null)
            {
                return "";
            }

            int sampleRate = GetSampleRate();

            // While streaming, we add timestamps manually on commit.
            bool addTimestamps = false;

            string result = await _sttManager.SpeechToText(
                audio,
                1, audio.Length, sampleRate,
                trimBracketTags,
                addTimestamps,
                onChunkTranscribed: null,
                OnStreamedWordCallback);

            if (result == null)
            {
                return "";
            }

            return result;
        }

        void OnStreamedWordCallback(string committedText, string newText, int chunkIndex)
        {
            if (!_previewWordByWord)
            {
                return;
            }

            // We need to prepend the already known committed text to this chunk result
            string fullCommittedText = _committedText;

            if (!string.IsNullOrEmpty(_committedText) && !string.IsNullOrEmpty(committedText))
            {
                if (!fullCommittedText.EndsWith(" "))
                {
                    fullCommittedText += " ";
                }
            }

            if (!string.IsNullOrEmpty(newText))
            {
                fullCommittedText += committedText;
            }

            _onStreamResult?.Invoke(fullCommittedText, newText, chunkIndex);
        }

        int GetSampleRate()
        {
#if UNITY_WEBGL
            return _webGLMic.SampleRate;
#else
            return STTManager.TARGET_SAMPLE_RATE;
#endif
        }

        // -------------------------
        // PUBLIC API
        // -------------------------

        public void StartRecording(bool addTimestamps = false)
        {
            ResetStateForStart();

#if UNITY_WEBGL
            _webGLMic.StartRecording();
#else
            _loopingClip = Microphone.Start(
                _currentMicDevice,
                true,
                STTManager.MAX_LENGTH_SECONDS,
                STTManager.TARGET_SAMPLE_RATE);
#endif

            _isRecording = true;

            _addTimestamps = addTimestamps;
        }

        public void StartStreaming(
            StreamingMode mode,
            bool addTimestamps,
            Action<string, string, int> onStreamResult)
        {
            StartRecording(addTimestamps);

            _isStreaming = true;
            _streamingMode = mode;

            _streamResultIndex = 0;
            _onStreamResult = onStreamResult;

            if (_queue != null)
            {
                _queue.Reset();
                _queue.SetEmitIndex(0);
            }

            EmitDisplay();
        }

        public StreamingMode GetStreamingMode()
        {
            return _streamingMode;
        }

        void ResetStateForStart()
        {
            _segmentAudio.Clear();
            _lastSamplePos = 0;

            _isRecording = false;
            _isStreaming = false;

            _addTimestamps = false;

            _streamingMode = StreamingMode.None;
            _onStreamResult = null;
            _streamResultIndex = 0;

            _timeSinceLastSpeech = 0f;
            _hasSpeechInSegment = false;

            _lastRms = 0f;

            _committedText = "";
            _draftText = "";
            _lastDisplayedText = "";

            _previewTimer = 0f;
            _lastPreviewSampleCount = 0;

            _commitQueuedOrInFlight = false;

            _committedSampleCount = 0;
            _pendingCommitHasTimestamp = false;
            _pendingCommitTimestampSeconds = 0.0f;

            if (_queue != null)
            {
                _queue.Reset();
            }

            _lastResponseTimeMs = 0;
        }

        public async Task<string> StopRecording(Action<string, string, int> streamResultCallback = null)
        {
            if (!_isRecording)
            {
                return "";
            }

#if UNITY_WEBGL
            _webGLMic.EndRecording();
            int sampleRate = _webGLMic.SampleRate;
#else
            Microphone.End(_currentMicDevice);
            int sampleRate = STTManager.TARGET_SAMPLE_RATE;
#endif

            _isRecording = false;
            _isStreaming = false;

            // Stop further streaming UI updates.
            _onStreamResult = null;
            _streamingMode = StreamingMode.None;

            if (_queue != null)
            {
                await _queue.FlushAndStopAsync();
                _lastResponseTimeMs = _queue.LastResponseTimeMs;
            }

            // Commit any remaining segment audio synchronously here (no draft).
            if (_segmentAudio.Count > 0)
            {
                float[] tailAudio = _segmentAudio.ToArray();
                _segmentAudio.Clear();

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                string tail = await _sttManager.SpeechToText(
                    tailAudio,
                    1, tailAudio.Length, sampleRate,
                    trimBracketTags: true,
                    addTimestamps: false,
                    onChunkTranscribed: null,
                    onWordTranscribed: streamResultCallback);

                if (!string.IsNullOrWhiteSpace(tail))
                {
                    _lastResponseTimeMs = stopwatch.ElapsedMilliseconds;

                    tail = tail.Trim();

                    if (_addTimestamps)
                    {
                        float tsSeconds = _committedSampleCount / (float)sampleRate;
                        string ts = FormatWhisperTimestamp(tsSeconds);
                        tail = ts + " " + tail;
                    }

                    if (!string.IsNullOrEmpty(_committedText))
                    {
                        if (!_committedText.EndsWith(" ", StringComparison.Ordinal))
                        {
                            _committedText += " ";
                        }
                    }

                    _committedText += tail;
                }

                _committedSampleCount += tailAudio.Length;
            }

            return _committedText.Trim();
        }

        public AudioClip GetLastRecordedClip()
        {
            if (_segmentAudio.Count == 0)
            {
                return null;
            }

            int sampleRate = GetSampleRate();

            AudioClip clip = AudioClip.Create("RecordedAudio", _segmentAudio.Count, 1, sampleRate, false);
            clip.SetData(_segmentAudio.ToArray(), 0);
            return clip;
        }

        public float GetLastRMS()
        {
            return _lastRms;
        }

        public long GetLastResponseTimeMs()
        {
            return _lastResponseTimeMs;
        }
    }
}