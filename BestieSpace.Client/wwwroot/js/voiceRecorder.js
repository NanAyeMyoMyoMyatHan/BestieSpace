// Kawaii Voice Recorder JS Interop for BestieSpace
window.voiceRecorder = {
    mediaRecorder: null,
    audioChunks: [],
    startTime: 0,
    timerInterval: null,
    stream: null,

    isSupported: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
    },

    startRecording: async function (dotNetHelper) {
        try {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                alert("Microphone recording is not supported in this browser.");
                return false;
            }

            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            this.stream = stream;
            this.audioChunks = [];
            this.startTime = Date.now();

            let options = {};
            if (MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) {
                options = { mimeType: 'audio/webm;codecs=opus' };
            } else if (MediaRecorder.isTypeSupported('audio/webm')) {
                options = { mimeType: 'audio/webm' };
            } else if (MediaRecorder.isTypeSupported('audio/mp4')) {
                options = { mimeType: 'audio/mp4' };
            }

            this.mediaRecorder = new MediaRecorder(stream, options);

            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data && event.data.size > 0) {
                    this.audioChunks.push(event.data);
                }
            };

            this.mediaRecorder.start(200);

            // Notify Blazor that recording has started
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnRecordingStarted');
            }

            return true;
        } catch (err) {
            console.error("Failed to start voice recording:", err);
            alert("Please allow microphone access to record voice notes for your bestie! 🎤💖");
            return false;
        }
    },

    stopAndUpload: function (apiUrl, senderName, dotNetHelper) {
        return new Promise((resolve, reject) => {
            if (!this.mediaRecorder || this.mediaRecorder.state === 'inactive') {
                resolve(null);
                return;
            }

            const durationSec = Math.max(1, Math.round((Date.now() - this.startTime) / 1000));

            this.mediaRecorder.onstop = async () => {
                try {
                    const mimeType = this.mediaRecorder.mimeType || 'audio/webm';
                    const audioBlob = new Blob(this.audioChunks, { type: mimeType });

                    // Clean up tracks
                    if (this.stream) {
                        this.stream.getTracks().forEach(track => track.stop());
                        this.stream = null;
                    }

                    // Prepare FormData
                    const formData = new FormData();
                    const ext = mimeType.includes('mp4') ? '.mp4' : '.webm';
                    formData.append('voice', audioBlob, `voice-note-${Date.now()}${ext}`);
                    formData.append('senderName', senderName);
                    formData.append('durationSeconds', durationSec.toString());

                    const response = await fetch(`${apiUrl}/api/chat/upload-voice`, {
                        method: 'POST',
                        body: formData
                    });

                    if (!response.ok) {
                        throw new Error(`Upload failed with status ${response.status}`);
                    }

                    const result = await response.json();
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnRecordingComplete', result);
                    }
                    resolve(result);
                } catch (error) {
                    console.error("Error uploading voice note:", error);
                    alert("Could not upload voice note. Please try again! 🌸");
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnRecordingError', error.message);
                    }
                    reject(error);
                }
            };

            this.mediaRecorder.stop();
        });
    },

    cancelRecording: function () {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }
        this.audioChunks = [];
    }
};

// Pastel Audio Player interop
window.pastelAudioPlayer = {
    play: function (audioId) {
        const audio = document.getElementById(audioId);
        if (audio) {
            audio.play();
        }
    },
    pause: function (audioId) {
        const audio = document.getElementById(audioId);
        if (audio) {
            audio.pause();
        }
    }
};
