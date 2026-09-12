using Moow;
using Moow.Utility;
using MoowCore;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIGoldContainer : MonoBehaviour {
    [SerializeField] TextMeshProUGUI _goldText;
    [SerializeField] Transform _iconTarget;
    public Transform iconTarget => _iconTarget;

    [SerializeField] private CanvasGroup _canvas;

    float _currentValue;

    private void Start() {
        _currentValue = InventoryManager.instance.money;
        updateText();

        this.addListener<float>(Events.UI_GOLD_ANIMATION_PROGRESS, onGoldAnimation);
        this.addListener<int>(Events.MONEY_CHANGED, onGoldChanged);
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    private void OnDestroy() {
        this.removeListener<float>(Events.UI_GOLD_ANIMATION_PROGRESS, onGoldAnimation);
        this.removeListener<int>(Events.MONEY_CHANGED, onGoldChanged);
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    private void onLevelCompleted(UnityEngine.Object sender, Event<object> eventData)
    {
       _canvas.alpha = 0;
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
         _canvas.alpha = 1;
        updateText();
    }

    private void onGoldAnimation(UnityEngine.Object sender, Event<float> eventData) {
        _currentValue += eventData.data;
        updateText();
    }

    private void onGoldChanged(UnityEngine.Object sender, Event<int> eventData) {
        _currentValue = eventData.data;
        updateText();
    }

    void updateText() {
        _goldText.text = Helper.AbbreviateNumber(_currentValue).ToString();
    }
}
