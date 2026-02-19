// VoiceLabs JavaScript utilities

window.downloadFile = (fileName, base64Data) => {
    const link = document.createElement('a');
    link.href = `data:audio/wav;base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
