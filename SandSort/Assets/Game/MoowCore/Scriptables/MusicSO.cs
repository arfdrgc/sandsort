using System.Collections;
using UnityEngine;
using DG.Tweening;
using System;

namespace Moow.Audio
{
#pragma warning disable 0649
    [CreateAssetMenu(fileName = "Music", menuName = "_ScriptableObjects/Music")]
    public class MusicSO : ScriptableObject
    {
        const string kBasePath = "Audio/";

        [SerializeField] AudioClip _clip;
        [SerializeField] string _assetName;
        [Range(0.0f, 1.0f)]
        [SerializeField] float _volume = 1.0f;
        [Range(0.0f, 4.0f)]
        [SerializeField] float _pitch = 1.0f;
        [SerializeField] bool _loop = true;
        [SerializeField] float _fadeInOutDuration = .5f;

        public string assetFullPath { get { return kBasePath + _assetName; } }
        public AudioClip clip => _clip;

        private Tween _fadeTween;

        private void OnDisable()
        {
            //_clip = null;
        }

        public void play(AudioSource source)
        {
            if (_clip == null)
            {
                _clip = Resources.Load<AudioClip>(assetFullPath);
            }
            source.clip = _clip;
            source.volume = _volume;
            source.pitch = _pitch;
            source.loop = _loop;
            source.Play(0);
        }

        public void stop(AudioSource source)
        {
            source?.Stop();
        }

        public void pause(AudioSource source)
        {
            source?.Pause();
        }

        public void resume(AudioSource source)
        {
            source?.Play();
        }

        public void setVolume(float targetVolumeRatio, float duration, Ease animation, AudioSource source, System.Action onComplete = null)
        {

            float targetVolume = _volume * targetVolumeRatio;
            _fadeTween?.Kill();
            _fadeTween = source.DOFade(targetVolume, duration).SetEase(animation).OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        public void fadeIn(float duration, AudioSource source, System.Action onComplete = null)
        {

            _fadeTween?.Rewind();
            float targetVolume = source.volume;
            source.volume = 0.0f;
            _fadeTween = source.DOFade(_volume, duration).SetEase(Ease.InExpo).OnComplete(() =>
            {
                if (onComplete != null)
                {
                    onComplete();
                }
            });
        }

        public void fadeOut(float duration, AudioSource source, System.Action onComplete = null)
        {

            _fadeTween?.Rewind();
            float targetVolume = source.volume;
            _fadeTween = source.DOFade(0.0f, duration).SetEase(Ease.InExpo).OnComplete(() =>
            {
                source.Stop();
                if (onComplete != null)
                {
                    onComplete();
                }
            });
        }

        public void cache()
        {
            if (_clip == null)
            {
                _clip = Resources.Load<AudioClip>(assetFullPath);
            }
        }
    }
#pragma warning restore 0649
}