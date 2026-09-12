using DG.Tweening;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Moow.Audio {
    public class BaseAudioPlayer<TAudioType> : SingletonDontDestroy<BaseAudioPlayer<TAudioType>> where TAudioType : System.Enum {
        [SerializeField] AudioData<TAudioType>[] _audioList;

        #region UNITY
        protected override void OnDestroy() {
            base.OnDestroy();
            resetIndexes();
        }
        #endregion

        #region METHODS
        static public void PlaySFX(TAudioType soundFX, bool mode3D = false, bool onlyOnce = false) {
            if(onlyOnce && IsPlaying(soundFX)) {
                return;
            }
            instance?.playSFX(soundFX, mode3D);
        }

        static public void Play3DSFX(TAudioType soundFX, Vector3 position) {
            if(IsPlaying(soundFX)) {
                return;
            }
            instance?.play3DSFX(soundFX, position);
        }

        static public void StopSFX(TAudioType soundFX) {
            instance?.stopSFX(soundFX);
        }

        static public bool IsPlaying(TAudioType soundFX) {
            return instance.isPlaying(soundFX);
        }

        static public AudioSO BringAuidoSO(TAudioType soundFX) {
            return instance?.bringAudioSO(soundFX);
        }

        static public void StopAllSFX() {
            instance.stopAllSFX();
        }

        public void playSFX(TAudioType soundFX, float delay) {
            DOVirtual.DelayedCall(delay, () => {
                playSFX(soundFX);
            });
        }

        public AudioSource playSFX(TAudioType soundFX, bool mode3D = false) {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                //if (EqualityComparer<TAudioType>.Default.Equals(audioData.soundFX, soundFX)) {
                if(audioData.soundFX.Equals(soundFX)) {
                    AudioManager manager = AudioManager.instance;
                    AudioSource source = manager?.play(audioData.audio, mode3D);

                    return source;
                }
            }

            return null;
            Debug.LogFormat("[AudioPlayer::playerSFX] Could not found SFX: '{0}'", soundFX);
        }

        public void play3DSFX(TAudioType soundFX, Vector3 position) {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                //if (EqualityComparer<TAudioType>.Default.Equals(audioData.soundFX, soundFX)) {
                if(audioData.soundFX.Equals(soundFX)) {
                    AudioManager manager = AudioManager.instance;
                    manager?.play3D(audioData.audio, position);
                    return;
                }
            }
            Debug.LogFormat("[AudioPlayer::playerSFX] Could not found SFX: '{0}'", soundFX);
        }

        public void stopSFX(TAudioType soundFX) {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                if(audioData.soundFX.Equals(soundFX)) {
                    BasicAudioSO audio = audioData.audio;
                    AudioManager manager = AudioManager.instance;
                    manager?.stop(audio);
                    break;
                }
            }
        }

        public bool isPlaying(TAudioType soundFX) {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                if(audioData.soundFX.Equals(soundFX)) {
                    BasicAudioSO audio = audioData.audio;
                    AudioManager manager = AudioManager.instance;
                    if(manager != null)
                        return manager.isPlaying(audio);

                    return false;
                }
            }
            return false;
        }

        public AudioSO bringAudioSO(TAudioType soundFX) {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                if(audioData.soundFX.Equals(soundFX)) {
                    BasicAudioSO audio = audioData.audio;
                    return audio;
                }
            }
            return null;
        }

        public void stopAllSFX() {
            foreach(AudioData<TAudioType> audioData in _audioList) {
                BasicAudioSO audio = audioData.audio;
                AudioManager manager = AudioManager.instance;
                if(manager != null) {
                    manager.stop(audio);
                }
            }
        }

        void resetIndexes() {
            for(int i = 0; i < _audioList.Length; i++) {
                var audioData = _audioList[i];
                if(audioData.audio != null) {
                    audioData.audio.resetIndexes();
                }
            }
        }
        #endregion
    }

    [System.Serializable]
    public struct AudioData<TType> {
        public TType soundFX;
        public BasicAudioSO audio;
    }
}