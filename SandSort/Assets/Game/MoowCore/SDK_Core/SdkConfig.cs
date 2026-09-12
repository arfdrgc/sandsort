using UnityEngine;

[CreateAssetMenu(menuName = "_ScriptableObjects/SDK Config")]
public class SdkConfig : ScriptableObject
{
    [Header("AppMetrica")]
    public string appMetricaApiKey;

    [Header("AppsFlyer")]
    public string appsFlyerDevKey;
    public string appsFlyerAppId;

    [Header("Facebook")]
    public string facebookAppId;
    public string facebookClientToken;

    [Header("AppLovin")]
    public string appLovinSdkKey;

    [Header("Android Ad Units")]
    public string androidInterstitial;
    public string androidRewarded;
    public string androidBanner;

    [Header("iOS Ad Units")]
    public string iosInterstitial;
    public string iosRewarded;
    public string iosBanner;

    public bool enableMaxDebugger;
}