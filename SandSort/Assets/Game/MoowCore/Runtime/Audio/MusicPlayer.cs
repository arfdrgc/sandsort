using System.Collections.Generic;
using UnityEngine;
using Moow;
using Moow.Audio;
using UnityEditor;
using DG.Tweening;
using NaughtyAttributes;

namespace MoowCore
{
    public class MusicPlayer : SingletonDontDestroy<MusicPlayer>
    {
        [SerializeField] BaseDataSO _dataSO;
        [SerializeField] MusicData[] _musicSOList;
        [SerializeField] AudioSource _source;
        [SerializeField] MusicData _activeMusicData;
        [SerializeField] float _volumeHeighRatio = 1;
        [SerializeField] float _volumeLowRatio = .3f;
        [SerializeField] float _fadeDuration = .5f;
        [SerializeField] Ease _fadeAnimation = Ease.Linear;
        //
        // Private Fields
        //
        private bool _isMusicActive;
        // True only while the source holds a mid-track position from pauseMusic(); innerPlay/continueMusic
        // then UnPause instead of restarting the clip from 0.
        private bool _isPaused;

        private MusicFX _currentMusicFX;
        private Dictionary<MusicFX, MusicData> _cached;

        public bool isMusicActive => _isMusicActive;
        public bool isLevelHard;

        #region UNITY METHODS   
        protected override void Awake()
        {
            base.Awake();
            cacheData();
        }

        private void OnEnable()
        {
            this.addListener<bool>(Events.SETTINGS_MUSIC_STATE_CHANED, onMusicStateChaned);
            this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
            this.addListener<object>(Events.UI_REVIVE_CLICKED, onReviveClicked);
        }

        private void OnDisable()
        {
            this.removeListener<bool>(Events.SETTINGS_MUSIC_STATE_CHANED, onMusicStateChaned);
            this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
            this.removeListener<object>(Events.UI_REVIVE_CLICKED, onReviveClicked);

            if (Application.isPlaying == false &&  MusicData._Previewer != null) {
                _activeMusicData.music.stop(MusicData._Previewer);
            }
        }
        #endregion

        #region METHOD
        public void playMusic(MusicFX musicFX)
        {
            if (!_dataSO.music)
                return;

            if(_cached.ContainsKey(musicFX) == false) {
                 throw new System.Exception($"[MusicPlayer::playMusic] MusicFX: {musicFX} could not be found!");
            }

            _activeMusicData = _cached[musicFX];
            _currentMusicFX = musicFX;
            if(_dataSO.music) {
                innerPlay();
            }
        }

        void onMusicStateChaned(UnityEngine.Object sender, Event<bool> eventData)
        {
            Debug.Log("onMusicStateChaned");
            if (eventData.data)
            {
                if (isLevelHard)
                    playMusic(MusicFX.InGameHard);
                else
                    playMusic(MusicFX.InGame);
            }
            else
                stopMusic();
        }

        // Every level load (first load, restart, next level): play the difficulty's theme, resuming it
        // from where the win/fail pause left it when it is the same track.
        void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData)
        {
            LevelSO level = LevelManager.instance != null ? LevelManager.instance.currentLevel : null;
            isLevelHard = level != null && level.difficulty != LevelDifficulty.EASY;
            playMusic(isLevelHard ? MusicFX.InGameHard : MusicFX.InGame);
        }

        // Revive continues the same level, so the fail pause is lifted.
        void onReviveClicked(UnityEngine.Object sender, Event<object> eventData)
        {
            continueMusic();
        }

        public void playCurrentMusicIfAvailable()
        {
            if (!_source.isPlaying) {
                innerPlay();
            }
        }

        public void stopMusic()
        {
            if(_source.clip != null) {
                innerStop();
            }
        }

        public void lowerMusic() {
            if (_source.clip != null)
            {
                innerLowerMusic();
            }
        }

        public void pauseMusic()
        {
            if (_source.clip != null && _source.isPlaying) {
                _activeMusicData.music.pause(_source);
                _isPaused = true;
                _isMusicActive = false;
            }
        }

        public void continueMusic()
        {
            if (_isPaused) {
                _source.enabled = true;
                _source.UnPause();
                _isPaused = false;
                _isMusicActive = true;
            }
        }

        void cacheData()
        {
            _cached = new Dictionary<MusicFX, MusicData>();
            if(_musicSOList == null) {
                throw new System.Exception($"[{name}::cacData] _musicSOList is null! Could not cache the data.");
            }

            for (int i = 0; i < _musicSOList.Length; i++)
            {
                var musicData = _musicSOList[i];
                _cached[musicData.musicFX] = musicData;
            }
        }

        void innerPlay()
        {
            _isMusicActive = true;
            _source.enabled = true;
            if(_isPaused && _source.clip != null && _source.clip == _activeMusicData.music.clip) {
                _source.UnPause();
                _isPaused = false;
            } else if(!_source.isPlaying || _source.clip != _activeMusicData.music.clip) {
                _isPaused = false;
                _activeMusicData.music.play(_source);
            } else {
                _activeMusicData.music.setVolume(_volumeHeighRatio, _fadeDuration, _fadeAnimation, _source);
            }
        }

        void innerLowerMusic()
        {
            _activeMusicData.music.setVolume(_volumeLowRatio, _fadeDuration, _fadeAnimation, _source);
        }

        void innerStop()
        {
            _isMusicActive = false;
            _isPaused = false;
            _source.enabled = false;
            _activeMusicData.music.stop(_source);
        }
        #endregion

        #region ACTION
        #endregion

        #region EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                cacheData();
            }
        }
        #endregion

        [System.Serializable]
        public struct MusicData
        {
            public static AudioSource _Previewer;
            public MusicFX musicFX;
            public MusicSO music;
#if UNITY_EDITOR
            [Button("Play")]
            void onPlay()
            {
                _Previewer ??= EditorUtility.CreateGameObjectWithHideFlags("Music Preview", HideFlags.HideAndDontSave, typeof(AudioSource)).GetComponent<AudioSource>();
                music.play(_Previewer);
            }

            [Button("Stop")]
            void onStop()
            {
                if(_Previewer != null) {
                    music.stop(_Previewer);
                }
            }
#endif
        }
    }
}
public enum MusicFX {
    Menu,
    InGame,
    InGameHard,
}