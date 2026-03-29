window.signageDisplay = window.signageDisplay || {};

window.signageDisplay.ensureVideosPlaying = (isMuted = true, volume = 0.8) => {
    const videos = document.querySelectorAll('.signage-video');
    const normalizedVolume = Math.min(1, Math.max(0, Number(volume ?? 0.8)));

    for (const video of videos) {
        if (!(video instanceof HTMLVideoElement)) {
            continue;
        }

        video.muted = !!isMuted;
        video.volume = normalizedVolume;
        video.autoplay = true;
        video.playsInline = true;
        video.preload = 'auto';
        video.controls = false;

        const playPromise = video.play();
        if (playPromise && typeof playPromise.catch === 'function') {
            playPromise.catch(() => {
                // Some browsers may briefly reject autoplay during source changes; retry on next render.
            });
        }
    }
};