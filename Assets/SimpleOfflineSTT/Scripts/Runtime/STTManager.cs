using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class STTManager : MonoBehaviour
    {
        [SerializeField]
        bool _logging = true;

        [SerializeField]
        BackendType _backend = BackendType.GPUCompute;

        [SerializeField]
        string _language = "en";

        [SerializeField, Tooltip("Warm up the models during initialization - makes the first use more responsive.")]
        bool _warmup = true;

        [Header("Models")]
        [SerializeField, Tooltip("Reference to the spectrogram model. Recommended to keep at fp32 type.")]
        ModelAsset _spectrogram;

        [SerializeField, Tooltip("Reference to the encoder model, recommended to keep at fp16 or better.")]
        ModelAsset _encoder;

        [SerializeField, Tooltip("Reference to the decoder model, recommended to keep at fp16 or better.")]
        ModelAsset _decoder;

        [SerializeField, Tooltip("Reference to the decoder_with_past model, recommended to keep at fp16 or better.")]
        ModelAsset _decoderWithPast;

        [SerializeField, Tooltip("Reference to the vocab.json file accompanying the models.")]
        TextAsset _vocabAsset;

        [SerializeField, Tooltip("Reference to the added_tokens.json file accompanying the models.")]
        TextAsset _addedTokensAsset;

        public const int MAX_LENGTH_SECONDS = 30;
        public const int TARGET_SAMPLE_RATE = 16000;

        [Header("Chunking (pause-aware)")]
        [SerializeField, Range(1, 29), Tooltip("Minimum chunk length before we start looking for a pause cut (seconds).")]
        int _minCutSeconds = 20;

        [SerializeField, Tooltip("RMS threshold below which we consider a frame 'quiet'.")]
        float _pauseRmsThreshold = 0.02f;

        [SerializeField, Tooltip("How long audio must stay quiet to qualify as a pause cut (seconds).")]
        float _pauseDurationSeconds = 0.25f;

        [SerializeField, Tooltip("Analysis frame size (ms). Smaller = more precise, slightly more CPU.")]
        int _analysisFrameMs = 10;

        STTInference _inference;
        string[] _vocab;
        bool _isReady = false;

        Dictionary<string, int> _languageCodeToTokenMap;
        Dictionary<int, float> _timestampTokenToSeconds;

        BackendType _currentBackend;
        string _currentLanguage;

        async void Start()
        {
            string vocabText = _vocabAsset.text;
#if !UNITY_WEBGL
            _vocab = await Task.Run(() => ParseVocab(vocabText));
#else
            await Task.Yield();
            _vocab = ParseVocab(vocabText);
#endif

            _languageCodeToTokenMap = new Dictionary<string, int>();

            string addedTokensText = _addedTokensAsset.text;
#if !UNITY_WEBGL
            await Task.Run(() => ParseAddedTokens(addedTokensText));
#else
            await Task.Yield();
            ParseAddedTokens(addedTokensText);
#endif

            _currentBackend = _backend;
            _currentLanguage = _language;

            _inference = new STTInference();
            _inference.LoadModels(_spectrogram, _encoder, _decoder, _decoderWithPast, _logging);
            _inference.CreateWorkers(_currentBackend, _logging);

            if (_warmup)
            {
                await WarmupModels();
            }

            _isReady = true;
            DebugLog("[SimpleOfflineSTT] Speech-To-Text System Ready.");
        }

        async Task WarmupModels()
        {
            DebugLog("[SimpleOfflineSTT] Warming up models...");

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            float[] warmupAudio = new float[TARGET_SAMPLE_RATE];
            await _inference.RunWhisper(warmupAudio, GetTokenForCode(_currentLanguage), _logging, false);

            DebugLog($"[SimpleOfflineSTT] Warmup completed in {sw.ElapsedMilliseconds} ms");
        }

        void OnDestroy()
        {
            _inference?.Dispose();
        }

        public BackendType GetCurrentBackend()
        {
            return _currentBackend;
        }

        public async Task SetBackendType(BackendType newBackend)
        {
            if (newBackend == _currentBackend)
            {
                return;
            }

            _isReady = false;

            _inference?.Dispose();

            _currentBackend = newBackend;

            _inference.CreateWorkers(_currentBackend, _logging);

            if (_warmup)
            {
                await WarmupModels();
            }

            _isReady = true;
        }

        public string GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        public void SetLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
        }

        public bool IsReady()
        {
            return _isReady;
        }

        public bool IsLogging()
        {
            return _logging;
        }

        public async Task<string> SpeechToText(
            AudioClip clip,
            bool trimBracketTags = false,
            bool addTimestamps = false,
            Action<string, string, int> onChunkTranscribed = null,
            Action<string, string, int> onWordTranscribed = null)
        {
            if (!_isReady)
            {
                return "[SimpleOfflineSTT] System not ready...";
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            float[] audioClipData = new float[clip.samples * clip.channels];
            clip.GetData(audioClipData, 0);

            int numChannels = clip.channels;
            int numSamples = clip.samples;
            int samplingRate = clip.frequency;

            if (_logging)
            {
                DebugLog($"[SimpleOfflineSTT] Getting AudioClip data took {sw.ElapsedMilliseconds} ms");
            }

            string transcribedText = await SpeechToText(
                audioClipData,
                numChannels, numSamples, samplingRate,
                trimBracketTags,
                addTimestamps,
                onChunkTranscribed,
                onWordTranscribed);

            DebugLog($"[SimpleOfflineSTT] Transcribed AudioClip {clip.name} in {sw.ElapsedMilliseconds} ms");

            return transcribedText;
        }

        public async Task<string> SpeechToText(
            float[] audioData,
            int numChannels, int numSamples, int samplingRate,
            bool trimBracketTags = false,
            bool addTimestamps = false,
            Action<string, string, int> onChunkTranscribed = null,
            Action<string, string, int> onWordTranscribed = null)
        {
            if (!_isReady)
            {
                return "[SimpleOfflineSTT] System not ready...";
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Ensure mono
#if !UNITY_WEBGL
            float[] monoData = await Task.Run(() => DSPUtils.ExtractMonoSamples(audioData, numChannels, numSamples));
#else
            float[] monoData = DSPUtils.ExtractMonoSamples(audioData, numChannels, numSamples);
#endif

            // Ensure 16KHz
#if !UNITY_WEBGL
            float[] resampledAudio = await Task.Run(() => DSPUtils.Resample(monoData, samplingRate, TARGET_SAMPLE_RATE, _logging));
#else
            float[] resampledAudio = DSPUtils.Resample(monoData, samplingRate, TARGET_SAMPLE_RATE, _logging);
#endif

            float audioLength = (float)resampledAudio.Length / (float)TARGET_SAMPLE_RATE;
            DebugLog($"[SimpleOfflineSTT] Formed {audioLength.ToString("0.00")}s of audio data in {sw.ElapsedMilliseconds} ms");

            // Compute chunk ranges off the main thread (non-WebGL)
            var splitOnPausesSw = System.Diagnostics.Stopwatch.StartNew();
            List<(int startSample, int lengthSamples)> ranges;
#if !UNITY_WEBGL
            ranges = await Task.Run(() =>
            {
                return DSPUtils.SplitOnPauses(
                    audio: resampledAudio,
                    sampleRate: TARGET_SAMPLE_RATE,
                    minCutSeconds: _minCutSeconds,
                    maxCutSeconds: MAX_LENGTH_SECONDS,
                    pauseRmsThreshold: _pauseRmsThreshold,
                    pauseDurationSeconds: _pauseDurationSeconds,
                    analysisFrameMs: _analysisFrameMs);
            });
#else
            ranges = DSPUtils.SplitOnPauses(
                audio: resampledAudio,
                sampleRate: TARGET_SAMPLE_RATE,
                minCutSeconds: _minCutSeconds,
                maxCutSeconds: MAX_LENGTH_SECONDS,
                pauseRmsThreshold: _pauseRmsThreshold,
                pauseDurationSeconds: _pauseDurationSeconds,
                analysisFrameMs: _analysisFrameMs);
#endif
            if (ranges.Count > 1)
            {
                DebugLog($"Split audio into {ranges.Count} chunks in {splitOnPausesSw.ElapsedMilliseconds} ms");
            }

            int languageToken = GetTokenForCode(_currentLanguage);

            List<int> allTokens = new List<int>();
            string fullTranscript = "";

            int chunkIndex = 0;

            foreach ((int startSample, int lengthSamples) in ranges)
            {
                float[] chunk = new float[lengthSamples];
                Array.Copy(resampledAudio, startSample, chunk, 0, lengthSamples);

                float chunkOffsetSeconds = (float)startSample / (float)TARGET_SAMPLE_RATE;

                List<int> streamedTokens = new List<int>();

                List<int> tokens = await _inference.RunWhisper(
                    chunk,
                    languageToken,
                    _logging,
                    addTimestamps,
                    (outputToken) =>
                    {
                        OnTokenTranscribedCallback(
                            fullTranscript,
                            outputToken,
                            streamedTokens,
                            addTimestamps,
                            chunkOffsetSeconds,
                            chunkIndex,
                            onWordTranscribed);
                    });

                string chunkText = TokensToString(
                    tokens,
                    includeTimestamps: addTimestamps,
                    timestampOffsetSeconds: chunkOffsetSeconds);

                if (trimBracketTags)
                {
                    chunkText = RemoveBracketTags(chunkText);
                }

                onChunkTranscribed?.Invoke(fullTranscript, chunkText, chunkIndex);
                chunkIndex++;

                allTokens.AddRange(tokens);
                fullTranscript += chunkText;
                fullTranscript += " ";
            }

            DebugLog($"[SimpleOfflineSTT] Tokens output: {string.Join(", ", allTokens)}");
            DebugLog($"[SimpleOfflineSTT] Text output: {fullTranscript}");

            return fullTranscript.ToString().Trim();
        }

        void OnTokenTranscribedCallback(
            string fullTranscript,
            int tokenId,
            List<int> streamedTokens,
            bool addTimestamps,
            float timestampOffset,
            int chunkIndex,
            Action<string, string, int> onWordTranscribed)
        {
            streamedTokens.Add(tokenId);

            string streamedString = TokensToString(streamedTokens, addTimestamps, timestampOffset);

            onWordTranscribed?.Invoke(fullTranscript, streamedString, chunkIndex);
        }

        public void RequestStop()
        {
            _inference?.RequestStop();
        }

        string[] ParseVocab(string vocabText)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var dict = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabText);

            int maxId = 0;
            foreach (int id in dict.Values)
            {
                if (id > maxId)
                {
                    maxId = id;
                }
            }

            string[] vocab = new string[maxId + 1];

            foreach (var kvp in dict)
            {
                string token = kvp.Key;

                token = token.Replace("Ġ", " ");

                vocab[kvp.Value] = token;
            }

            DebugLog($"[SimpleOfflineSTT] Parsed vocab in {sw.ElapsedMilliseconds} ms");

            return vocab;
        }

        public void ParseAddedTokens(string addedTokensText)
        {
            var rawData = JsonConvert.DeserializeObject<Dictionary<string, int>>(addedTokensText);

            _timestampTokenToSeconds = new Dictionary<int, float>();

            Regex langPattern = new Regex(@"^<\|(\D+)\|>$");
            Regex tsPattern = new Regex(@"^<\|(\d+(?:\.\d+)?)\|>$");

            foreach (var kvp in rawData)
            {
                Match tsMatch = tsPattern.Match(kvp.Key);
                if (tsMatch.Success)
                {
                    if (float.TryParse(tsMatch.Groups[1].Value, out float seconds))
                    {
                        if (!_timestampTokenToSeconds.ContainsKey(kvp.Value))
                        {
                            _timestampTokenToSeconds.Add(kvp.Value, seconds);
                        }
                    }
                    continue;
                }

                Match langMatch = langPattern.Match(kvp.Key);
                if (langMatch.Success)
                {
                    string code = langMatch.Groups[1].Value;

                    if (code.Length <= 3)
                    {
                        if (!_languageCodeToTokenMap.ContainsKey(code))
                        {
                            _languageCodeToTokenMap.Add(code, kvp.Value);
                        }
                    }
                }
            }

            DebugLog($"[SimpleOfflineSTT] Loaded {_languageCodeToTokenMap.Count} languages.");
            DebugLog($"[SimpleOfflineSTT] Loaded {_timestampTokenToSeconds.Count} timestamp tokens.");
        }

        public List<string> GetAllLanguageCodes()
        {
            return _languageCodeToTokenMap.Keys.ToList();
        }

        int GetTokenForCode(string langCode)
        {
            if (_languageCodeToTokenMap.TryGetValue(langCode.ToLowerInvariant(), out int token))
            {
                return token;
            }

            Debug.LogWarning($"[SimpleOfflineSTT] Language code '{langCode}' not found.");
            return -1;
        }

        string TokensToString(List<int> tokens, bool includeTimestamps, float timestampOffsetSeconds)
        {
            StringBuilder sb = new StringBuilder();

            bool wasTimestampToken = false;
            float lastTimestampSeconds = 0.0f;

            foreach (int id in tokens)
            {
                if (includeTimestamps && _timestampTokenToSeconds != null && _timestampTokenToSeconds.TryGetValue(id, out float seconds))
                {
                    wasTimestampToken = true;
                    lastTimestampSeconds = seconds + timestampOffsetSeconds;
                    continue;
                }

                if (id < 50257)
                {
                    if (id >= 0 && id < _vocab.Length)
                    {
                        // Print the timestamp before a new non-timestamp token
                        if (wasTimestampToken)
                        {
                            sb.Append("<|");
                            sb.Append(lastTimestampSeconds.ToString("0.00"));
                            sb.Append("|>");
                            wasTimestampToken = false;
                        }

                        // And print the token
                        string tok = _vocab[id];
                        if (!string.IsNullOrEmpty(tok))
                        {
                            sb.Append(tok);
                        }
                    }
                }
            }

            string rawBpeString = sb.ToString();
            string decodedString = WhisperDecoder.DecodeText(rawBpeString);

            return decodedString.Trim();
        }

        string RemoveBracketTags(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            StringBuilder sb = new StringBuilder(input.Length);
            bool insideBrackets = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '[')
                {
                    insideBrackets = true;
                    continue;
                }

                if (c == ']')
                {
                    insideBrackets = false;
                    continue;
                }

                if (!insideBrackets)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        void DebugLog(string log)
        {
            if (_logging)
            {
                Debug.Log(log, this);
            }
        }
    }
}