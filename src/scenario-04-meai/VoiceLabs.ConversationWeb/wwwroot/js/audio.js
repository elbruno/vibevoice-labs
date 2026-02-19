// Audio interop for microphone capture and playback
let mediaRecorder = null;
let audioChunks = [];

window.audioInterop = {
    startRecording: async function () {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });
        audioChunks = [];
        mediaRecorder.ondataavailable = (e) => audioChunks.push(e.data);
        mediaRecorder.start();
    },
    stopRecording: async function () {
        return new Promise(resolve => {
            mediaRecorder.onstop = async () => {
                const blob = new Blob(audioChunks, { type: 'audio/webm' });
                const arrayBuffer = await blob.arrayBuffer();
                const bytes = new Uint8Array(arrayBuffer);
                // Stop all tracks to release the microphone
                mediaRecorder.stream.getTracks().forEach(t => t.stop());
                resolve(bytes);
            };
            mediaRecorder.stop();
        });
    },
    playAudio: function (audioBytes) {
        const blob = new Blob([new Uint8Array(audioBytes)], { type: 'audio/wav' });
        const url = URL.createObjectURL(blob);
        const audio = new Audio(url);
        audio.onended = () => URL.revokeObjectURL(url);
        audio.play();
    }
};
