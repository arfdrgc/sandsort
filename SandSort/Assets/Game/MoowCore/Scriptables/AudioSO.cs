using UnityEngine;

namespace Moow.Audio {
    public abstract class AudioSO : ScriptableObject {
        abstract public void play(AudioSource source);
        abstract public void stop();
        abstract public void resetIndexes();
        abstract public float pitch { get; set; }
        abstract public bool isLooped { get; }
        abstract public AudioSource source { get; set; }
    }

    interface IAudioInternal {
        void playInternal(AudioSource source);
        void stopInternal();
    }
}

