using UnityEngine;
using UnityEditor;
using NaughtyAttributes;

namespace Moow.Audio
{
    [ExecuteAlways]
    [CreateAssetMenu(fileName = "Basic Audio", menuName = "_ScriptableObjects/Basic Audio")]
    public class BasicAudioSO : AudioSO, IAudioInternal
    {
        const string kNullAudioFileMessage = "Audio reference cannot be null!";
        const string kAudioClipListCannotBeEmpty = "Must have at least one audio";

        // =========================
        // CLIPS
        // =========================

        [SerializeField, Required]
        private AudioClip[] _clips = System.Array.Empty<AudioClip>();

        // =========================
        // VOLUME
        // =========================

        [Header("Audio")]

        [SerializeField]
        [OnValueChanged(nameof(OnVolumeTypeChanged))]
        private VolumeType _volumeType;

        [SerializeField]
        [HideIf(nameof(_volumeType), VolumeType.Ranged)]
        [Range(0f, 1f)]
        private float _singleVolume = 1f;

        [SerializeField]
        [ShowIf(nameof(_volumeType), VolumeType.Ranged)]
        [MinMaxSlider(0f, 1f)]
        private Vector2 _volumeRange = new Vector2(0.8f, 1f);

        [SerializeField]
        [OnValueChanged(nameof(OnLoopStateChanged))]
        private bool _loop;

        [SerializeField]
        [ShowIf(nameof(ShowTimeInterval))]
        private bool _enableTimeInterval;

        [SerializeField]
        [ShowIf(nameof(ShowIntervalField))]
        private float _interval = 0.05f;

        // =========================
        // PITCH
        // =========================

        [Header("Pitch")]

        [SerializeField]
        [OnValueChanged(nameof(OnResetPitchState))]
        private bool _usePitch;

        [SerializeField]
        [ShowIf(nameof(_usePitch))]
        [MinMaxSlider(0f, 4f)]
        private Vector2 _pitch = new Vector2(.5f, 1f);

        [SerializeField]
        [ShowIf(nameof(_usePitch))]
        private int _pitchStep = 10;

        [SerializeField]
        [ShowIf(nameof(ShowPitchAdvanced))]
        [Min(0)]
        private float _resetPitchInterval = 2f;

        [SerializeField]
        [ShowIf(nameof(ShowPitchAdvanced))]
        [OnValueChanged(nameof(OnPitchTypeChanged))]
        private PitchType _pitchType;

        [SerializeField]
        [ShowIf(nameof(ShowPitchAdvanced))]
        private bool _pitchRewind;

        [SerializeField]
        [ShowIf(nameof(_usePitch))]
        private bool _randomPitch;

        // =========================
        // RANDOMNESS
        // =========================

        [Header("Audio Clip Randomness")]

        [SerializeField]
        [ShowIf(nameof(CanShowRandomness))]
        [OnValueChanged(nameof(OnResetRandomnessState))]
        private bool _useRandomness;

        [SerializeField]
        [ShowIf(nameof(CanShowRandomness))]
        [OnValueChanged(nameof(OnRandomnessTypeChanged))]
        private RandomnessType _randomnessType;

        // =========================
        // HELPERS
        // =========================

        private bool ShowTimeInterval => !_loop;

        private bool ShowIntervalField => !_loop && _enableTimeInterval;

        private bool ShowPitchAdvanced => _usePitch && !_randomPitch;

        private bool CanShowRandomness =>
            _useRandomness && _clips != null && _clips.Length > 1;

        // =========================
        // STATE
        // =========================

        private AudioSource _source;

        private int _currentClipIndex = 0;
        private int _currentPitchStepIndex = 0;
        private double _lastPlayTime = double.NegativeInfinity;
        private double _pitchResetElapsedTime = double.NegativeInfinity;

        // =========================
        // UNITY
        // =========================

        private void OnEnable()
        {
            resetIndexes();
        }

        // =========================
        // AUDIO CORE
        // =========================

        public override void play(AudioSource source)
        {
            if (source == null)
            {
                Debug.LogError("[BasicAudioSO] Source is null!");
                return;
            }

            _source = source;
            AudioManager.instance.play(this, source);
        }

        public override void stop()
        {
            if (_source != null)
                AudioManager.instance.stop(this, _source);
            else
                AudioManager.instance.stop(this);
        }

        public override void resetIndexes()
        {
            _pitchResetElapsedTime = double.NegativeInfinity;
            _lastPlayTime = double.NegativeInfinity;

            resetPitchState();
            resetClipIndex();
            resetPitchStep();
        }

        public override float pitch
        {
            get
            {
                if (_source == null) return _pitch.x;
                _source.pitch = _pitch.x;
                return _source.pitch;
            }
            set
            {
                _pitch.x = value;
                _pitch.y = value;

                if (_source != null)
                    _source.pitch = _pitch.x;
            }
        }

        public override AudioSource source
        {
            get => _source;
            set => _source = value;
        }

        public override bool isLooped => _loop;

        // =========================
        // PLAY LOGIC
        // =========================

        bool isAvailableToPlay
        {
            get
            {
                if (_clips == null || _clips.Length == 0)
                {
                    Debug.LogError($"[BasicAudioSO] '{name}' No audio clip available.");
                    return false;
                }

                if (!_enableTimeInterval)
                    return true;

                return (time - _lastPlayTime) > _interval;
            }
        }

        void pitchModule()
        {
            if (!_usePitch)
            {
                resetPitchState();
                return;
            }

            if (_source == null) return;

            if (_randomPitch)
            {
                _source.pitch = Random.Range(_pitch.x, _pitch.y);
                return;
            }

            if (time - _pitchResetElapsedTime >= _resetPitchInterval)
                resetPitchStep();

            resetPitchStepElapsedTime();

            float normalized = _currentPitchStepIndex / (float)Mathf.Max(1, pitchStepOneMinus);
            _source.pitch = Mathf.Lerp(_pitch.x, _pitch.y, normalized);

            switch (_pitchType)
            {
                case PitchType.Default:
                    _currentPitchStepIndex++;
                    _currentPitchStepIndex = _pitchRewind
                        ? _currentPitchStepIndex % _pitchStep
                        : Mathf.Min(pitchStepOneMinus, _currentPitchStepIndex);
                    break;

                case PitchType.Reverse:
                    _currentPitchStepIndex--;
                    _currentPitchStepIndex = _pitchRewind
                        ? (_currentPitchStepIndex < 0 ? pitchStepOneMinus : _currentPitchStepIndex)
                        : Mathf.Min(pitchStepOneMinus, _currentPitchStepIndex);
                    break;
            }
        }

        void randomnessModule()
        {
            if (_source == null) return;

            if (_clips == null || _clips.Length == 0)
            {
                _source.clip = null;
                return;
            }

            if (!isRandomnessAvailable)
            {
                _source.clip = _clips[0];
                return;
            }

            switch (_randomnessType)
            {
                case RandomnessType.Sequence:
                    _source.clip = _clips[_currentClipIndex];
                    _currentClipIndex = (_currentClipIndex + 1) % _clips.Length;
                    break;

                case RandomnessType.Random:
                    _source.clip = _clips[Random.Range(0, _clips.Length)];
                    break;
            }
        }

        void audioModule()
        {
            if (_source == null) return;

            _source.loop = _loop;
            _source.volume = volume;
        }

        void IAudioInternal.playInternal(AudioSource source)
        {
            if (!isAvailableToPlay)
                return;

            pitchModule();
            randomnessModule();
            audioModule();

            source.Play(0);

            _lastPlayTime = time;
        }

        void IAudioInternal.stopInternal()
        {
            if (_source != null)
                _source.Stop();
        }

        // =========================
        // EVENTS
        // =========================

        private void OnVolumeTypeChanged(VolumeType type) { }

        private void OnRandomnessTypeChanged(RandomnessType type)
        {
            if (type == RandomnessType.Sequence)
                resetClipIndex();
        }

        private void OnPitchTypeChanged(PitchType type)
        {
            resetPitchStep();
        }

        private void OnResetPitchState(bool state)
        {
            if (!state) resetPitchState();
        }

        private void OnResetRandomnessState(bool state)
        {
            if (!state) resetClipIndex();
        }

        private void OnLoopStateChanged(bool state)
        {
            if (_source != null)
                _source.loop = state;
        }

        // =========================
        // HELPERS
        // =========================

        private float volume =>
            _volumeType == VolumeType.Ranged ? Random.Range(_volumeRange.x, _volumeRange.y) : _singleVolume;

        private double time =>
#if UNITY_EDITOR
            EditorApplication.isPlaying ? Time.time : EditorApplication.timeSinceStartup;
#else
            Time.time;
#endif

        private bool isRandomnessAvailable =>
            _useRandomness && _clips != null && _clips.Length > 1;

        private int pitchStepOneMinus => Mathf.Max(1, _pitchStep - 1);

        private void resetClipIndex() => _currentClipIndex = 0;

        private void resetPitchStep()
        {
            _currentPitchStepIndex =
                _pitchType == PitchType.Default ? 0 : pitchStepOneMinus;
        }

        private void resetPitchState()
        {
            if (_source != null)
                _source.pitch = 1f;
        }

        private void resetPitchStepElapsedTime()
        {
            _pitchResetElapsedTime = time;
        }

        // =========================
        // ENUMS
        // =========================

        public enum VolumeType { Default = 0, Ranged }
        public enum RandomnessType { Sequence = 0, Random }
        public enum PitchType { Default = 0, Reverse }
    }
}