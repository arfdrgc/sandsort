using Moow;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsSendEmail : MonoBehaviour
{
    [SerializeField] Button _button;
    private string appVersionID, deviceUID;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
    }

    void onClick() {
        appVersionID = Application.version;
        deviceUID = SystemInfo.deviceUniqueIdentifier.ToString();
        string mailRecipient = "info@mankrik.com";
        string subject = Uri.EscapeDataString(Application.productName);
        string body = Uri.EscapeDataString("\n\n\n\n\n\n------------------------------\nPlease write the message above\nDevice ID : " + deviceUID + "\nApp Version : " + appVersionID);
        string mailtoURL = "mailto:" + mailRecipient + "?subject=" + subject + "&body=" + body;
        Application.OpenURL(mailtoURL);
    }
}
