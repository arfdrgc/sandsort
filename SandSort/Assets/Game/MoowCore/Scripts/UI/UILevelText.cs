using Moow;
using TMPro;
using UnityEngine;

public class UILevelText : MonoBehaviour {

    [SerializeField] TextMeshProUGUI _levelText;
    [SerializeField] TextMeshPro _levelText_2;
    [SerializeField] bool _isUGUItext;
    [SerializeField] float _customTextSize;
    [SerializeField] float _customNumSize;

    void OnEnable() => this.addListener<object>(Events.LEVEL_READY_TO_PLAY, onLevelReady);
    void OnDisable() => this.removeListener<object>(Events.LEVEL_READY_TO_PLAY, onLevelReady);

    void onLevelReady(UnityEngine.Object sender, Event<object> e) => UpdateLevelText();

    void UpdateLevelText() {
        string text = BuildLevelText();
        if (_isUGUItext) { if (_levelText != null) _levelText.text = text; }
        else { if (_levelText_2 != null) _levelText_2.text = text; }
    }

    string BuildLevelText() {
        int level = LevelManager.instance.level;
        if (_customTextSize <= 0 && _customNumSize <= 0) return $"Level {level}";

        string textPart = _customTextSize > 0 ? $"<size={_customTextSize}>Level</size>" : "Level";
        string numPart  = _customNumSize  > 0 ? $"<size={_customNumSize}>{level}</size>"  : level.ToString();
        return $"{textPart}\n{numPart}";
    }
}
