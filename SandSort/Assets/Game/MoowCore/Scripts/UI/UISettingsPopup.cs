using DG.Tweening;
using Moow;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UISettingsPopup : MonoBehaviour {
    [SerializeField] DataSO _data;
    [SerializeField] GameObject _popup;
    [SerializeField] UISettingsItem _vibration;
    [SerializeField] UISettingsItem _sound;
    [SerializeField] UISettingsItem _music;
    [SerializeField] private TextMeshProUGUI appVersionIDtext;
    private string appVersionID, deviceUID;

    private void OnEnable() {
        this.addListener<object>(Events.UI_OPEN_SETTINGS, show);
        this.addListener<object>(Events.UI_CLOSE_SETTINGS, hide);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.UI_OPEN_SETTINGS, show);
        this.removeListener<object>(Events.UI_CLOSE_SETTINGS, hide);
    }

    private void Awake() {
        _vibration.onStateChanged += onStateChange;
        _sound.onStateChanged += onStateChange;
        _music.onStateChanged += onStateChange;
        appVersionID = Application.version;
        appVersionIDtext.text = "v." + appVersionID;
        deviceUID = SystemInfo.deviceUniqueIdentifier.ToString();
        hide();
    }

    private void hide(UnityEngine.Object sender, Event<object> eventData) {
        hide();
    }

    void hide() {
        
        _popup.transform.GetChild(0).transform.DOScale(0, 0.2f).SetEase(Ease.InBack);
        _popup.transform.GetComponent<CanvasGroup>().DOFade(0, 0.2f).OnComplete(() => {
            _popup.SetActive(false);
        });
    }

    private void show(UnityEngine.Object sender, Event<object> eventData) {
        show();
    }

    void show() {
        _popup.SetActive(true);

        _vibration.setState(!_data.disableHaptic);
        _sound.setState(_data.sound);
        _music.setState(_data.music);

        _popup.transform.GetComponent<CanvasGroup>().DOFade(1, 0.2f);
        _popup.transform.GetChild(0).transform.DOScale(1, 0.2f).SetEase(Ease.OutBack);
    }

    void toggle() {
        if(_popup.gameObject.activeSelf) {
            hide();
        } else {
            show();
        }
    }

    void onStateChange() {
        bool previousMusicState = _data.music;

        _data.disableHaptic = !_vibration.state;
        _data.sound = _sound.state;
        _data.music = _music.state;

        if(previousMusicState != _data.music) {
            this.dispatchEvent(Events.SETTINGS_MUSIC_STATE_CHANED, _data.music);
        }

        Database.SaveGame();
    }

        public void OpenUrl(string key)
    {
        if (key.Equals("privacy"))
        {
            Application.OpenURL("https://mankrik.com/privacy");
        }
    }

    public void SendEmail()
    {
        string mailRecipient = "info@mankrik.com";
        string subject = Uri.EscapeDataString(Application.productName);
        string body = Uri.EscapeDataString("\n\n\n\n\n\n------------------------------\nPlease write the message above\nDevice ID : " + deviceUID + "\nApp Version : " + appVersionID);
        string mailtoURL = "mailto:" + mailRecipient + "?subject=" + subject + "&body=" + body;
        Application.OpenURL(mailtoURL);
    }
}
