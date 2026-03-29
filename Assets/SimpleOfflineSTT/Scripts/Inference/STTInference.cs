using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class STTInference : IDisposable
    {
        // Whisper Tiny token constants
        const int START_TOKEN = 50258;      // <|startoftranscript|>
        const int TRANSCRIBE_TOKEN = 50359; // <|transcribe|>
        const int NO_TIMESTAMPS = 50363;    // <|notimestamps|>
        const int END_TOKEN = 50257;        // <|endoftext|>

        const int VOCAB_SIZE = 51865;
        const int BUF_SIZE = 480000;
        const int MAX_TOKENS = 448;
        const int NUM_LAYERS = 4;

        // Time budget for inference per frame (in milliseconds)
        // 8ms is a safe target for 60fps (16.6ms total frame time)
        private const float MAX_AI_MS_PER_FRAME = 8.0f;

        private readonly SemaphoreSlim _transcribeLock = new SemaphoreSlim(1, 1);

        Model _spectrogramModel;
        Model _encoderModel;
        Model _decoderModel;
        Model _decoderWithPastModel;

        Worker _spectrogram;
        Worker _encoder;
        Worker _decoder;
        Worker _decoderWithPast;

        Worker _argmaxWorker;
        Tensor<float> _penaltyTensor;
        Tensor<float> _noPenaltyTensor;

        bool _stopRequested;
        double _frameStartTime;

        float[] _persistentAudioBuffer = new float[BUF_SIZE];

        BackendType _lastBackend;

        class WhisperCache
        {
            public Tensor[] DecoderKV = new Tensor[NUM_LAYERS * 2];
            public Tensor[] EncoderKV = new Tensor[NUM_LAYERS * 2];

            public void Dispose()
            {
                for (int i = 0; i < DecoderKV.Length; i++)
                {
                    if (DecoderKV[i] != null)
                    {
                        DecoderKV[i].Dispose();
                        DecoderKV[i] = null;
                    }

                    if (EncoderKV[i] != null)
                    {
                        EncoderKV[i].Dispose();
                        EncoderKV[i] = null;
                    }
                }
            }
        }

        public void LoadModels(
            ModelAsset spectrogram,
            ModelAsset encoder,
            ModelAsset decoder,
            ModelAsset decoderWithPast,
            bool logging)
        {
            _spectrogramModel = ModelLoader.Load(spectrogram);
            _encoderModel = ModelLoader.Load(encoder);
            _decoderModel = ModelLoader.Load(decoder);
            _decoderWithPastModel = ModelLoader.Load(decoderWithPast);
        }

        public void CreateWorkers(BackendType backend, bool logging)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _spectrogram?.Dispose();
            _encoder?.Dispose();
            _decoder?.Dispose();
            _decoderWithPast?.Dispose();
            _argmaxWorker?.Dispose();

            _penaltyTensor?.Dispose();
            _noPenaltyTensor?.Dispose();

            _spectrogram = new Worker(_spectrogramModel, backend);
            _encoder = new Worker(_encoderModel, backend);
            _decoder = new Worker(_decoderModel, backend);
            _decoderWithPast = new Worker(_decoderWithPastModel, backend);

            _argmaxWorker = CreateArgmaxWorker(backend);

            float[] penaltyArray = new float[VOCAB_SIZE];
            penaltyArray[NO_TIMESTAMPS] = -10000f;
            _penaltyTensor = new Tensor<float>(new TensorShape(1, 1, VOCAB_SIZE), penaltyArray);

            float[] zeroArray = new float[VOCAB_SIZE];
            _noPenaltyTensor = new Tensor<float>(new TensorShape(1, 1, VOCAB_SIZE), zeroArray);

            if (logging)
            {
                Debug.Log($"[STTInference] Created workers in {sw.ElapsedMilliseconds} ms");
            }

            _lastBackend = backend;
        }

        Worker CreateArgmaxWorker(BackendType backend)
        {
            var graph = new FunctionalGraph();
            var inputLogits = graph.AddInput<float>(new DynamicTensorShape(1, -1, VOCAB_SIZE));
            var penaltyInput = graph.AddInput<float>(new TensorShape(1, 1, VOCAB_SIZE));

            var biasedLogits = Functional.Add(inputLogits, penaltyInput);
            var argMax = Functional.ArgMax(biasedLogits, 2, false);

            var model = graph.Compile(argMax);
            return new Worker(model, backend);
        }

        /// <summary>
        /// Slices the worker schedule across multiple frames based on time budget.
        /// </summary>
        private async Task ScheduleLayerByLayerAsync(Worker worker)
        {
            var it = worker.ScheduleIterable();
            while (it.MoveNext())
            {
                double elapsed = (Time.realtimeSinceStartupAsDouble - _frameStartTime) * 1000.0;

                if (elapsed > MAX_AI_MS_PER_FRAME)
                {
                    await Task.Yield();
                    _frameStartTime = Time.realtimeSinceStartupAsDouble;
                }
            }
        }

        public async Task<List<int>> RunWhisper(
            float[] inputAudio,
            int languageToken,
            bool logging,
            bool timestamp,
            Action<int> TokenCallback = null)
        {
            var tokens = new List<int> { START_TOKEN, languageToken, TRANSCRIBE_TOKEN };
            if (!timestamp) tokens.Add(NO_TIMESTAMPS);

            var cache = new WhisperCache();
            await _transcribeLock.WaitAsync();

            try
            {
                // If we hit an exception, recreate the workers now
                if (_spectrogram == null || _encoder == null || _decoder == null || _decoderWithPast == null || _argmaxWorker == null)
                {
                    CreateWorkers(_lastBackend, logging);
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                _frameStartTime = Time.realtimeSinceStartupAsDouble;
                _stopRequested = false;

                Array.Clear(_persistentAudioBuffer, 0, BUF_SIZE);
                Array.Copy(inputAudio, _persistentAudioBuffer, Math.Min(inputAudio.Length, BUF_SIZE));

                using var audioTensor = new Tensor<float>(new TensorShape(1, BUF_SIZE), _persistentAudioBuffer);

                // Sliced Spectrogram
                _spectrogram.SetInput("audio", audioTensor);
                await ScheduleLayerByLayerAsync(_spectrogram);
                var mel = _spectrogram.PeekOutput() as Tensor<float>;

                // Sliced Encoder
                _encoder.SetInput("input_features", mel);
                await ScheduleLayerByLayerAsync(_encoder);
                var encoderStates = _encoder.PeekOutput("last_hidden_state") as Tensor<float>;

                // Initial Decode
                using (var tokensTensor = new Tensor<int>(new TensorShape(1, tokens.Count), tokens.ToArray()))
                {
                    _decoder.SetInput("input_ids", tokensTensor);
                    _decoder.SetInput("encoder_hidden_states", encoderStates);
                    await ScheduleLayerByLayerAsync(_decoder);

                    UpdateCacheFromWorker(_decoder, cache, true);

                    Tensor logitsCopy = null;
                    _decoder.CopyOutput("logits", ref logitsCopy);

                    using (logitsCopy)
                    {
                        int nextToken = await ArgMaxLastAsync(logitsCopy, timestamp);

                        tokens.Add(nextToken);
                        TokenCallback?.Invoke(nextToken);
                    }
                }

                if (logging)
                {
                    Debug.Log($"[STTInference] Performed initial decode in {sw.ElapsedMilliseconds} ms");
                }

                // Autoregressive Loop
                for (int step = 0; step < MAX_TOKENS; step++)
                {
                    if (_stopRequested)
                    {
                        tokens.Add(END_TOKEN);
                    }

                    int lastToken = tokens[tokens.Count - 1];
                    if (lastToken == END_TOKEN)
                    {
                        break;
                    }

                    using var lastTokenTensor = new Tensor<int>(new TensorShape(1, 1), new[] { lastToken });

                    _decoderWithPast.SetInput("input_ids", lastTokenTensor);
                    BindCacheToWorker(_decoderWithPast, cache);

                    await ScheduleLayerByLayerAsync(_decoderWithPast);

                    Tensor logitsCopy = null;
                    _decoderWithPast.CopyOutput("logits", ref logitsCopy);

                    using (logitsCopy)
                    {
                        int nextToken = await ArgMaxLastAsync(logitsCopy, timestamp);

                        tokens.Add(nextToken);
                        TokenCallback?.Invoke(nextToken);
                    }

                    // Update ONLY decoder cache. Encoder cache is constant.
                    UpdateCacheFromWorker(_decoderWithPast, cache, false);
                }

                if (logging)
                {
                    Debug.Log($"[STTInference] Inference completed in {sw.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STTInference] Exception during inference: {ex}");

                Dispose();

                tokens.Clear();
            }
            finally
            {
                cache.Dispose();
                _transcribeLock.Release();
            }

            return tokens;
        }

        public void RequestStop() => _stopRequested = true;

        private void UpdateCacheFromWorker(Worker worker, WhisperCache cache, bool updateEncoder)
        {
            for (int i = 0; i < NUM_LAYERS; i++)
            {
                int kIdx = i * 2;
                int vIdx = i * 2 + 1;

                // Because Decoder cache size GROWS every step, we cannot reuse the same buffer.
                // We must dispose the old one so CopyOutput can allocate a larger one.
                if (cache.DecoderKV[kIdx] != null)
                {
                    cache.DecoderKV[kIdx].Dispose();
                    cache.DecoderKV[kIdx] = null;
                }
                worker.CopyOutput($"present.{i}.decoder.key", ref cache.DecoderKV[kIdx]);

                if (cache.DecoderKV[vIdx] != null)
                {
                    cache.DecoderKV[vIdx].Dispose();
                    cache.DecoderKV[vIdx] = null;
                }
                worker.CopyOutput($"present.{i}.decoder.value", ref cache.DecoderKV[vIdx]);

                if (updateEncoder)
                {
                    worker.CopyOutput($"present.{i}.encoder.key", ref cache.EncoderKV[kIdx]);
                    worker.CopyOutput($"present.{i}.encoder.value", ref cache.EncoderKV[vIdx]);
                }
            }
        }

        private void BindCacheToWorker(Worker worker, WhisperCache cache)
        {
            for (int i = 0; i < NUM_LAYERS; i++)
            {
                worker.SetInput($"past_key_values.{i}.decoder.key", cache.DecoderKV[i * 2]);
                worker.SetInput($"past_key_values.{i}.decoder.value", cache.DecoderKV[i * 2 + 1]);
                worker.SetInput($"past_key_values.{i}.encoder.key", cache.EncoderKV[i * 2]);
                worker.SetInput($"past_key_values.{i}.encoder.value", cache.EncoderKV[i * 2 + 1]);
            }
        }

        async Task<int> ArgMaxLastAsync(Tensor logits, bool timestamp)
        {
            int seqLen = logits.shape[1];
            var activePenalty = timestamp ? _penaltyTensor : _noPenaltyTensor;

            _argmaxWorker.SetInput("input_0", logits);
            _argmaxWorker.SetInput("input_1", activePenalty);

            await ScheduleLayerByLayerAsync(_argmaxWorker);

            var result = _argmaxWorker.PeekOutput() as Tensor<int>;
            using var cpuResult = await result.ReadbackAndCloneAsync() as Tensor<int>;

            // Return the final token in the sequence
            return cpuResult[0, seqLen - 1];
        }

        public void Dispose()
        {
            _spectrogram?.Dispose();
            _encoder?.Dispose();
            _decoder?.Dispose();
            _decoderWithPast?.Dispose();
            _argmaxWorker?.Dispose();
            _penaltyTensor?.Dispose();
            _noPenaltyTensor?.Dispose();

            _spectrogram = null;
            _encoder = null;
            _decoder = null;
            _decoderWithPast = null;
            _argmaxWorker = null;
            _penaltyTensor = null;
            _noPenaltyTensor = null;
        }
    }
}