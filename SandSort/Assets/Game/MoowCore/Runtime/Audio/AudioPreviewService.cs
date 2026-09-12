using UnityEngine;

namespace Moow.Audio.Editor
{
    public static class AudioPreviewService
    {
        private static AudioSource _previewSource;

        public static AudioSource GetPreviewSource()
        {
            if (_previewSource != null)
                return _previewSource;

            var go = new GameObject("Audio Preview Source");
            go.hideFlags = HideFlags.HideAndDontSave;

            _previewSource = go.AddComponent<AudioSource>();
            return _previewSource;
        }

        public static void Play(BasicAudioSO audio)
        {
            if (audio == null) return;

            var source = GetPreviewSource();
            AudioManager.instance.play(audio, source);
        }

        public static void Stop()
        {
            if (_previewSource == null) return;
            _previewSource.Stop();
        }

        public static void Reset(AudioSO audio)
        {
            audio?.resetIndexes();
        }
    }
}