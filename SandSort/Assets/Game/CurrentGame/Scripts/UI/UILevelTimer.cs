using Moow;
using TMPro;
using UnityEngine;

// Shows Level's countdown as mm:ss. Level owns all timer rules (start on first drag, Settings
// pause, stop on Win/Lose, reset on restart) and dispatches LEVEL_TIMER_CHANGED whenever the
// whole-second value changes; this only formats it.
public class UILevelTimer : MonoBehaviour {
    [SerializeField] TextMeshProUGUI _timerText;

    void OnEnable() => this.addListener<int>(Events.LEVEL_TIMER_CHANGED, onTimerChanged);
    void OnDisable() => this.removeListener<int>(Events.LEVEL_TIMER_CHANGED, onTimerChanged);

    void onTimerChanged(Object sender, Event<int> e) {
        int seconds = Mathf.Max(0, e.data);
        _timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
