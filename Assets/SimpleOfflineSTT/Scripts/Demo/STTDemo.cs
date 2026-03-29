using NUnit.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleOfflineSTT
{
    public class STTDemo : MonoBehaviour
    {
        [SerializeField]
        STTManager _sttManager;

        [SerializeField]
        STTMicrophone _sttMicrophone;

        [SerializeField]
        AudioClip[] _testAudioClips;

        [SerializeField]
        TMP_Dropdown _backendDropdown;

        [SerializeField]
        TMP_Dropdown _languageDropdown;

        [SerializeField]
        Toggle _timestampToggle;

        [SerializeField]
        TMP_Dropdown _audioClipSelector;

        [SerializeField]
        Button _playButton;

        [SerializeField]
        Button _transcribeButton;

        [SerializeField]
        Button _pushToTalkButton;

        [SerializeField]
        VUMeter _pushToTalkVUMeter;

        [SerializeField]
        Button _playBackButton;

        [SerializeField]
        Button _pauseDetectionStartButton;

        [SerializeField]
        Button _pauseDetectionStopButton;

        [SerializeField]
        VUMeter _pauseDetectionVUMeter;

        [SerializeField]
        Button _realtimeStartButton;

        [SerializeField]
        Button _realtimeStopButton;

        [SerializeField]
        VUMeter _realtimeVUMeter;

        [SerializeField]
        AudioSource _audioSource;

        [SerializeField]
        TMP_Text _webGlWarningText;

        [SerializeField]
        TMP_Text _outputText;

        [SerializeField]
        TMP_Text _pauseDetectionRmsText;

        bool _isPushToTalkActive;
        bool _isStreaming;

        bool _populatedBackends;
        bool _populatedLanguages;

        void Start()
        {
            Debug.Assert(_sttManager != null,                   $"{nameof(_sttManager)} is null in {this.name}", this);
            Debug.Assert(_testAudioClips != null,               $"{nameof(_testAudioClips)} is null in {this.name}", this);
            Debug.Assert(_backendDropdown != null,              $"{nameof(_backendDropdown)} is null in {this.name}", this);
            Debug.Assert(_languageDropdown != null,             $"{nameof(_languageDropdown)} is null in {this.name}", this);
            Debug.Assert(_timestampToggle != null,              $"{nameof(_timestampToggle)} is null in {this.name}", this);
            Debug.Assert(_audioClipSelector != null,            $"{nameof(_audioClipSelector)} is null in {this.name}", this);
            Debug.Assert(_playButton != null,                   $"{nameof(_playButton)} is null in {this.name}", this);
            Debug.Assert(_transcribeButton != null,             $"{nameof(_transcribeButton)} is null in {this.name}", this);
            Debug.Assert(_pushToTalkButton != null,             $"{nameof(_pushToTalkButton)} is null in {this.name}", this);
            Debug.Assert(_pushToTalkVUMeter != null,            $"{nameof(_pushToTalkVUMeter)} is null in {this.name}", this);
            Debug.Assert(_playBackButton != null,               $"{nameof(_playBackButton)} is null in {this.name}", this);
            Debug.Assert(_pauseDetectionStartButton != null,    $"{nameof(_pauseDetectionStartButton)} is null in {this.name}", this);
            Debug.Assert(_pauseDetectionStopButton != null,     $"{nameof(_pauseDetectionStopButton)} is null in {this.name}", this);
            Debug.Assert(_pauseDetectionVUMeter != null,        $"{nameof(_pauseDetectionVUMeter)} is null in {this.name}", this);
            Debug.Assert(_pauseDetectionRmsText != null,        $"{nameof(_pauseDetectionRmsText)} is null in {this.name}", this);
            Debug.Assert(_realtimeStartButton != null,          $"{nameof(_realtimeStartButton)} is null in {this.name}", this);
            Debug.Assert(_realtimeStopButton != null,           $"{nameof(_realtimeStopButton)} is null in {this.name}", this);
            Debug.Assert(_realtimeVUMeter != null,              $"{nameof(_realtimeVUMeter)} is null in {this.name}", this);
            Debug.Assert(_audioSource != null,                  $"{nameof(_audioSource)} is null in {this.name}", this);
            Debug.Assert(_webGlWarningText != null,             $"{nameof(_webGlWarningText)} is null in {this.name}", this);
            Debug.Assert(_outputText != null,                   $"{nameof(_outputText)} is null in {this.name}", this);
            
            // Clear backend dropdown (populate when STTManager is ready)
            _backendDropdown.ClearOptions();

            // Clear language dropdown (populate when STTManager is ready)
            _languageDropdown.ClearOptions();

            // Populate dropdown with all AudioClips
            List<string> audioClipNames = new List<string>();
            foreach (var clip in _testAudioClips)
            {
                audioClipNames.Add(clip.name);
            }
            _audioClipSelector.ClearOptions();
            _audioClipSelector.AddOptions(audioClipNames);

            _playButton.onClick.AddListener(PlayClip);
            _transcribeButton.onClick.AddListener(Transcribe);

            EventTrigger pushToTalkTrigger = _pushToTalkButton.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry pushToTalkDownEntry = new EventTrigger.Entry();
            pushToTalkDownEntry.eventID = EventTriggerType.PointerDown;
            pushToTalkDownEntry.callback.AddListener(OnPushToTalkDown);
            pushToTalkTrigger.triggers.Add(pushToTalkDownEntry);
            EventTrigger.Entry pushToTalkUpEntry = new EventTrigger.Entry();
            pushToTalkUpEntry.eventID = EventTriggerType.PointerUp;
            pushToTalkUpEntry.callback.AddListener(OnPushToTalkUp);
            pushToTalkTrigger.triggers.Add(pushToTalkUpEntry);

            _pushToTalkVUMeter.Reset();

            _playBackButton.interactable = false;
            _playBackButton.onClick.AddListener(PlayBackRecordedAudio);

            _pauseDetectionStartButton.onClick.AddListener(OnPauseDetectionStreamingStart);
            _pauseDetectionStopButton.onClick.AddListener(OnStreamingStop);
            _pauseDetectionStopButton.interactable = false;
            _pauseDetectionVUMeter.Reset();

            _realtimeStartButton.onClick.AddListener(OnRollingWindowStreamingStart);
            _realtimeStopButton.onClick.AddListener(OnStreamingStop);
            _realtimeStopButton.interactable = false;
            _realtimeVUMeter.Reset();

            _isPushToTalkActive = false;
            _isStreaming = false;

#if UNITY_WEBGL
            _webGlWarningText.gameObject.SetActive(true);
#endif
        }

        void Update()
        {
            if (_sttManager.IsReady())
            {
                if (!_populatedBackends)
                {
                    PopulateBackends();
                }

                if (!_populatedLanguages)
                {
                    PopulateLanguages();
                }
            }

            float micRMS = _sttMicrophone.GetLastRMS();

            if (_isPushToTalkActive)
            {
                _pushToTalkVUMeter.SetRMSValue(micRMS);
            }
            else if (_isStreaming)
            {
                if (_sttMicrophone.GetStreamingMode() == STTMicrophone.StreamingMode.PauseDetection)
                {
                    _pauseDetectionVUMeter.SetRMSValue(micRMS);
                    _pauseDetectionRmsText.text = $"RMS: {micRMS.ToString("n4")}";
                }
                else if (_sttMicrophone.GetStreamingMode() == STTMicrophone.StreamingMode.Realtime)
                {
                    _realtimeVUMeter.SetRMSValue(micRMS);
                }
            }
        }

        void PopulateBackends()
        {
            BackendType currentBackend = _sttManager.GetCurrentBackend();
            int currentBackendIndex = -1;

            List<string> backendOptions = new List<string>();
            Array backends = Enum.GetValues(typeof(BackendType));
            for (int i = 0; i < backends.Length; i++)
            {
                backendOptions.Add(backends.GetValue(i).ToString());

                if (((BackendType)backends.GetValue(i)) == currentBackend)
                {
                    currentBackendIndex = i;
                }
            }
            _backendDropdown.AddOptions(backendOptions);
            _backendDropdown.SetValueWithoutNotify(currentBackendIndex);
            _backendDropdown.onValueChanged.AddListener(OnBackendSelected);

            _populatedBackends = true;
        }

        void PopulateLanguages()
        {
            List<string> languageCodes = _sttManager.GetAllLanguageCodes();
            string currentLanguageCode = _sttManager.GetCurrentLanguage();

            _languageDropdown.ClearOptions();
            _languageDropdown.AddOptions(languageCodes);

            for (int i = 0; i < languageCodes.Count; i++)
            {
                if (languageCodes[i] == currentLanguageCode)
                {
                    _languageDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }

            _languageDropdown.onValueChanged.AddListener(OnLanguageSelected);

            _populatedLanguages = true;
        }

        void OnBackendSelected(int selectedValue)
        {
            string backendName = _backendDropdown.captionText.text;
            BackendType selectedBackend = Enum.Parse<BackendType>(backendName);

            _ = _sttManager.SetBackendType(selectedBackend);
        }

        void OnLanguageSelected(int selectedValue)
        {
            string languageCode = _languageDropdown.captionText.text;

            _sttManager.SetLanguage(languageCode);
        }

        void PlayClip()
        {
            if (_testAudioClips.Length == 0)
            {
                return;
            }

            _audioSource.clip = _testAudioClips[_audioClipSelector.value];
            _audioSource.Play();
        }

        async void Transcribe()
        {
            if (_testAudioClips.Length == 0)
            {
                return;
            }

            _outputText.text = "<i>Transcribing...</i>";

            AudioClip clipToTranscribe = _testAudioClips[_audioClipSelector.value];
            string transcribedText = await _sttManager.SpeechToText(
                clipToTranscribe,
                false,
                _timestampToggle.isOn,
                OnChunkTranscribed,
                OnWordTranscribed);

            _outputText.text = transcribedText;
        }

        void OnChunkTranscribed(string committedText, string newText, int chunkIndex)
        {
            Debug.Log($"Chunk {chunkIndex} transcribed. Committed text: {committedText}, New text: {newText}");
        }

        void OnWordTranscribed(string committedText, string newText, int chunkIndex)
        {
            // Set committed text
            _outputText.text = committedText;

            // Space, if necessary
            if (!string.IsNullOrEmpty(committedText) && !string.IsNullOrEmpty(newText))
            {
                _outputText.text += " ";
            }

            // Set draft text (in italic)
            if (!string.IsNullOrEmpty(newText))
            {
                _outputText.text += "<i>" + newText + "</i>";
            }
        }

        void OnPushToTalkDown(BaseEventData eventData)
        {
            if (_isStreaming)
            {
                return;
            }

            _sttMicrophone.StartRecording(_timestampToggle.isOn);

            _playBackButton.interactable = false;

            _outputText.text = "<i>Recording...</i>";

            _isPushToTalkActive = true;
        }

        async void OnPushToTalkUp(BaseEventData eventData)
        {
            if (_isStreaming)
            {
                return;
            }

            _outputText.text = "<i>Transcribing...</i>";

            string transcribedText = await _sttMicrophone.StopRecording(OnWordTranscribed);

            _playBackButton.interactable = true;

            _outputText.text = transcribedText;

            _isPushToTalkActive = false;
            _pushToTalkVUMeter.Reset();
        }

        void PlayBackRecordedAudio()
        {
            _audioSource.clip = _sttMicrophone.GetLastRecordedClip();
            _audioSource.Play();
        }

        void OnPauseDetectionStreamingStart()
        {
            _outputText.text = "<i>Listening... (Speak and pause)</i>";

            // Disable PTT button
            _pushToTalkButton.interactable = false;
            _playBackButton.interactable = false;

            // Disable Start, enable Stop
            _pauseDetectionStartButton.interactable = false;
            _pauseDetectionStopButton.interactable = true;

            // Disable other buttons
            _realtimeStartButton.interactable = false;
            _realtimeStopButton.interactable = false;

            // Start Streaming
            _sttMicrophone.StartStreaming(
                STTMicrophone.StreamingMode.PauseDetection,
                _timestampToggle.isOn,
                OnStreamingResult);

            _isStreaming = true;
        }

        void OnRollingWindowStreamingStart()
        {
            _outputText.text = "<i>Listening... (Speak continuously)</i>";

            // Disable PTT button
            _pushToTalkButton.interactable = false;
            _playBackButton.interactable = false;

            // Disable Start, enable Stop
            _realtimeStartButton.interactable = false;
            _realtimeStopButton.interactable = true;

            // Disable other buttons
            _pauseDetectionStartButton.interactable = false;
            _pauseDetectionStopButton.interactable = false;

            // Start Streaming
            _sttMicrophone.StartStreaming(
                STTMicrophone.StreamingMode.Realtime,
                _timestampToggle.isOn,
                OnStreamingResult);

            _isStreaming = true;
        }

        void OnStreamingResult(string committedText, string draftText, int chunkIndex)
        {
            // committedText is the full confirmed string since streaming started.
            // draftText is the realtime transcription, that may be refined as streaming continues.

            // To get the new committed text since the last result, keep a cache and
            // subtract it from the committedText sent with every new chunkIndex.

            string fullText = committedText;
            
            if (!string.IsNullOrEmpty(draftText))
            {
                fullText += " <i>" + draftText + "</i>";
            }

            fullText = fullText.Trim();

            _outputText.text = fullText;
        }

        async void OnStreamingStop()
        {
            string finalOutput = await _sttMicrophone.StopRecording();
            _outputText.text = finalOutput;

            // Re-enable PTT
            _pushToTalkButton.interactable = true;
            _playBackButton.interactable = true;

            // Disable Stop, re-enable Start button
            _pauseDetectionStartButton.interactable = true;
            _pauseDetectionStopButton.interactable = false;

            // Re-enable other buttons
            _realtimeStartButton.interactable = true;
            _realtimeStopButton.interactable = true;

            _isStreaming = false;
            _pauseDetectionRmsText.text = "RMS: 0.0";
            _pauseDetectionVUMeter.Reset();
            _realtimeVUMeter.Reset();
        }
    }
}
