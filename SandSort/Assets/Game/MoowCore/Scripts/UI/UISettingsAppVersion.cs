using Moow;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsAppVersion : MonoBehaviour {
    [SerializeField] TextMeshProUGUI appVersionIDtext;

    private void OnEnable() {
        onUpdate();
    }

    private void OnDisable() {
        onUpdate();
    }

    void onUpdate() {
        appVersionIDtext.text = "v." + Application.version;
    }
}
