using DG.Tweening;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsItem : MonoBehaviour {
    [SerializeField] Button _button;
    [SerializeField] private Transform _swicthButton;
    [SerializeField] private Image _swicthButtonImg, _swicthButtonBG;

    public bool state { get; private set; }
    public System.Action onStateChanged;

    private void OnEnable() {
        _button.onClick.AddListener(toggle);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(toggle);
    }

    public void setState(bool newState) {
        state = newState;
        SwitchButtonAnim();
    }

    private void SwitchButtonAnim()
    {
        if (state)
        {
            _swicthButton.DOLocalMoveX(80.2f, 0.2f);
            _swicthButtonImg.DOColor(new Color(1f, 1f, 1f, 1f), 0.2f); // FFFFFF
            _swicthButtonBG.DOColor(new Color(1f, 1f, 1f, 1f), 0.2f); // FFFFFF
        }
        else
        {
            _swicthButton.DOLocalMoveX(-80.2f, 0.2f);
            _swicthButtonImg.DOColor(new Color(0.455f, 0.455f, 0.455f, 1f), 0.2f); // 747474
            _swicthButtonBG.DOColor(new Color(0.8f, 0.8f, 0.8f, 1f), 0.2f); // A6A6A6
        }
    }

    [Button]
    public void toggle() {
        AudioPlayer.instance.playSFX(AudioFX.UI_BUTTON_CLICK);
        setState(!state);
        onStateChanged?.Invoke();
    }
}
