using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIRewardGold : MonoBehaviour {
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] CanvasGroup _canvasGroup;


    public void show(int reward) {
        gameObject.SetActive(true);
        _canvasGroup.DOFade(1, 0.3f).From(0);

        _text.text = reward + "";
    }
}
