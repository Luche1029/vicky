mergeInto(LibraryManager.library, {

    InitWebGLMic: function () {
        if (window.webglMicInitialized)
            return;

        const audioContext = new (window.AudioContext || window.webkitAudioContext)();
        window.webglAudioContext = audioContext;

        navigator.mediaDevices.getUserMedia({ audio: true }).then(stream => {
            window.webglMicStream = stream;

            SendMessage(
                'WebGLMic',
                'OnSampleRate',
                audioContext.sampleRate.toString()
            );

            window.webglMicInitialized = true;

            console.log('[WebGLMic] Microphone permission granted');
        }).catch(err => {
            console.error('[WebGLMic] Microphone permission denied', err);
        });
    },

    StartWebGLMic: function () {
        if (!window.webglMicStream || !window.webglAudioContext)
            return;

        console.log('[WebGLMic] Got WebGLMic Stream and Context');

        if (window.webglAudioContext.state === 'suspended') {
            window.webglAudioContext.resume();
        }

        const source = window.webglAudioContext.createMediaStreamSource(window.webglMicStream);
        const processor = window.webglAudioContext.createScriptProcessor(4096, 1, 1);

        source.connect(processor);
        processor.connect(window.webglAudioContext.destination);

        window.webglMicSource = source;
        window.webglMicProcessor = processor;

        processor.onaudioprocess = e => {
            const input = e.inputBuffer.getChannelData(0);
            const pcm16 = new Int16Array(input.length);

            for (let i = 0; i < input.length; i++) {
                const s = Math.max(-1, Math.min(1, input[i]));
                pcm16[i] = s * 32767;
            }

            const bytes = new Uint8Array(pcm16.buffer);
            let binary = '';
            for (let i = 0; i < bytes.length; i++) {
                binary += String.fromCharCode(bytes[i]);
            }

            SendMessage(
                'WebGLMic',
                'OnAudioChunk',
                btoa(binary)
            );
        };
    },

    StopWebGLMic: function () {
        if (window.webglMicProcessor) {
            window.webglMicProcessor.disconnect();
            window.webglMicProcessor = null;
        }

        if (window.webglMicSource) {
            window.webglMicSource.disconnect();
            window.webglMicSource = null;
        }
    }
});
