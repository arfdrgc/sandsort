using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;
using Moow;

namespace Moow.Audio {
#pragma warning disable 0649
	public class AudioManager : BaseSingleton<AudioManager> {
		[SerializeField] BaseDataSO _baseDataSO;
		[SerializeField] AudioMixer _mixer;
		[SerializeField] Transform _audioParent;

		AudioMixerGroup _masterGroup;
		// AudioMixer Exposed Params
		const string ExposedVolumeParam = "MasterVolume";

		// Active playing list.
		private List<AudioElement> _playingAudioElements;

		// Internal Generated AudioSources.
		private Stack<AudioSource> _availableAudioSources;
		private List<AudioSource> _inUseAudioSources;

		#region BASE
		override protected void Awake() {
			_availableAudioSources = new Stack<AudioSource>();
			_inUseAudioSources = new List<AudioSource>();
			_playingAudioElements = new List<AudioElement>();

			AudioMixerGroup[] groups = _mixer.FindMatchingGroups("Master");
			if (groups.Length > 0) {
				_masterGroup = _mixer.FindMatchingGroups("Master")[0];
			} else {
				Debug.Log("[AudioManager::Awake] could not found 'Master' group in audio mixer!");
			}
		}

		private void Start() {
#if UNITY_ANDROID
			bool result = _mixer.SetFloat(ExposedVolumeParam, 10.0f);
#endif
		}

		private void OnEnable() {
			this.addListener<bool>(Events.SETTINGS_SOUND_STATE_CHANED, onSoundStateChange);
			//this.addListener<bool>(AdenConstants.Events.SETTINGS_MUSIC_STATE_CHANED, onMusicStateChange);
		}

		private void OnDisable() {
			stopAllAudio();
			this.removeListener<bool>(Events.SETTINGS_SOUND_STATE_CHANED, onSoundStateChange);
			//this.removeListener<bool>(AdenConstants.Events.SETTINGS_MUSIC_STATE_CHANED, onMusicStateChange);
		}

		private void Update() {
			List<AudioElement> removeList = new List<AudioElement>();
			foreach (AudioElement element in _playingAudioElements) {
				if (element.audioSource == null || !element.audioSource.isPlaying) {
					//Debug.LogFormat("[AudioManager::Update] remove AudioSource: {0} isPlaying: {1}",
					//element.audioSO.name, element.audioSource.isPlaying);
					removeList.Add(element);
				}
			}
			foreach (var element in removeList) {
				removeAudioElement(element);
				if (hasInternalAudioSource(element.audioSource)) {
					returnPoolAudioSource(element.audioSource);
				}
			}
		}
		#endregion

		#region METHODS
		// Sound FX Operations
		public void play(AudioSO audioSO, AudioSource audioSource) {
			if (!isSoundOn) return;

			//Debug.LogFormat("[AudioManager::play(AudioSO _, AudioSource _)] AudioSO: '{0}' AudioSource: '{1}'", audioSO, audioSource.clip);
			if (!hasAudioElement(audioSO, audioSource)) {
				AudioElement audioElement = new AudioElement(audioSO, audioSource);
				addAudioElement(audioElement);
			} else {
				//Debug.LogFormat("[AudioManager::play(AudioSO _, AudioSource _)] AudioElement already added!");
			}

			IAudioInternal audioInternal = (BasicAudioSO)audioSO;
			audioInternal.playInternal(audioSource);
		}

		public void stop(AudioSO audioSO, AudioSource audioSource) {
			Debug.LogFormat("[AudioManager::stop(AudioSO _, AudioSource _)] AudioSO: '{0}' AudioSource: '{1}'", audioSO, audioSource.clip);
			 if (hasAudioElement(audioSO, audioSource)) {
			 	AudioElement element = bringAudioElement(audioSO, audioSource);
			 	IAudioInternal audioInternal = (BasicAudioSO)element.audioSO;
			 	audioInternal.stopInternal();
			 }
			
			 if (hasInternalAudioSource(audioSource)) {
			 	returnPoolAudioSource(audioSource);
			 }
		}

		public AudioSource play(AudioSO audioSO, bool mode3D = false) {

			if (!isSoundOn || audioSO == null) return null;
			//Debug.LogFormat("[AudioManager::play(AudioSO)] AudioSO: '{0}'", audioSO);

			AudioSource audioSource = generateAudioSourceIfNeeded(audioSO);
			audioSource.spatialBlend = mode3D ? 1.0f : 0.0f;
			if (mode3D == false)
				audioSource.transform.position = Vector3.zero;

			audioSO.source = audioSource;
			AudioElement audioElement = new AudioElement(audioSO, audioSource);
			addAudioElement(audioElement);

			IAudioInternal audioInternal = (BasicAudioSO)audioSO;
			audioInternal.playInternal(audioElement.audioSource);
			
			return audioSource;
		}

		public void play3D(AudioSO audioSO, Vector3 position) {

			if (!isSoundOn) return;
			//Debug.LogFormat("[AudioManager::play(AudioSO)] AudioSO: '{0}'", audioSO);

			AudioSource audioSource = generateAudioSourceIfNeeded(audioSO);
			audioSource.spatialBlend = 1.0f;
			audioSO.source = audioSource;

			AudioElement audioElement = new AudioElement(audioSO, audioSource);
			addAudioElement(audioElement);

			IAudioInternal audioInternal = (BasicAudioSO)audioSO;
			audioInternal.playInternal(audioElement.audioSource);
		}

		public void stop(AudioSO audioSO) {
			//Debug.LogFormat("[AudioManager::stop(AudioSO)] AudioSO: '{0}'", audioSO);
			AudioSource source = audioSO.source;
			if (source == null)
				return;

			BasicAudioSO audio = (BasicAudioSO)audioSO;

			IAudioInternal audioInternal = (BasicAudioSO)audioSO;
			audioInternal.stopInternal();
			audioSO.source = null; //Shared audio, if we dont assign null to it,
								   // Stopping unrelated AudioSO would stop the other active audio.

			if (hasInternalAudioSource(source)) {
				returnPoolAudioSource(source);
			}
		}

		public bool isPlaying(AudioSO audioSO) {
			AudioSource source = audioSO.source;
			if (source != null)
				return source.isPlaying;

			return false;
		}

		void addAudioElement(AudioElement element) {
			_playingAudioElements.Add(element);
			//Debug.Log("[AudioManager::addAudioElement(AudioElement)] Added: " + element);
		}

		void removeAudioElement(AudioElement element) {
			_playingAudioElements.Remove(element);
			//Debug.Log("[AudioManager::removeAudioElement(AudioElement)] Removed: " + element);
		}

		void stopAllAudio() {

			if (_playingAudioElements == null)
				return;

			foreach (AudioElement element in _playingAudioElements) {
				IAudioInternal audio = (IAudioInternal)element.audioSO;
				audio.stopInternal();

				if (hasInternalAudioSource(element.audioSource)) {
					returnPoolAudioSource(element.audioSource);
				}
			}
		}
		#endregion

		#region ACTIONS
		void onSoundStateChange(object sender, Event<bool> eventData) {
			isSoundOn = eventData.data;
		}

		//void onMusicStateChange(object sender, Event<bool> eventData)
		//{
		//	isMusicOn = eventData.data;
		//}
		#endregion

		#region HELPER
		public AudioSource generateAudioSourceIfNeeded(AudioSO audioSO) {
			AudioSource source = null;
			if (_availableAudioSources.Count <= 0) {
				//Debug.Log("[AudioManager::generateAudioSourceIfNeeded] Generated!");
				source = _audioParent.gameObject.AddComponent<AudioSource>();
				source.outputAudioMixerGroup = _masterGroup;
				source.minDistance = 80;
				source.playOnAwake = false;
			} else {
				source = _availableAudioSources.Pop();
				//Debug.LogFormat("[AudioManager::generateAudioSourceIfNeeded] Used Available One: {0}",
				//source.clip);
			}
			_inUseAudioSources.Add(source);
			return source;
		}

		AudioElement bringAudioElement(AudioSO audioSO, AudioSource audioSource) {
			AudioElement element = new AudioElement();
			foreach (var audioElement in _playingAudioElements) {
				if (audioElement.audioSO == audioSO && audioElement.audioSource == audioSource) {
					element = audioElement;
					break;
				}
			}
			return element;
		}

		bool hasAudioElement(AudioSO audioSO, AudioSource audioSource) {
			foreach (var audioElement in _playingAudioElements) {
				if (audioElement.audioSO == audioSO && audioElement.audioSource == audioSource) {
					return true;
				}
			}
			return false;
		}

		bool hasInternalAudioSource(AudioSource source) {
			return _inUseAudioSources.Contains(source);
		}

		void returnPoolAudioSource(AudioSource source) {
			//source.clip = null;
			_inUseAudioSources.Remove(source);
			_availableAudioSources.Push(source);
		}

		public bool isSoundOn {
			get => _baseDataSO.sound;
			set {
				_baseDataSO.sound = value;
				if (!_baseDataSO.sound) stopAllAudio();
			}
		}

        public bool isMusicOn
        {
            get => _baseDataSO.music;
            set
            {
				_baseDataSO.music = value;
				this.dispatchEvent<bool>(Events.SETTINGS_MUSIC_STATE_CHANED, _baseDataSO.music);
            }
        }
        #endregion
    }

	public struct AudioElement {
		public AudioSource audioSource;
		public AudioSO audioSO;

		public AudioElement(AudioSO audioSO, AudioSource audioSource) {
			this.audioSource = audioSource;
			this.audioSO = audioSO;
		}

		public override string ToString() {
			return string.Format("AudioSO: '{0}', AudioSource: '{1}'", audioSO, audioSource.clip);
		}
	}
#pragma warning restore 0649
}