using Moow;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "_ScriptableObjects/Data")]
public class DataSO : BaseDataSO {
    [Header("DataSO Properties")]
    public int levelAttemptCount;
    public bool isRated;
    public int levelFreq;
    public bool isBannerActive;
    public bool debugIsRewardedVideoAvailable;
    public bool debugIsInterstitialReady;
    public int skinIndex;

    //Moow - Azur oynanan her bölüm için artar kazanma kaybetme restart farketmez
    public int incrementalLevelCount;


    public override void reset() {
        base.reset();

        Debug.Log("levelAttemptCount : " + levelAttemptCount);
        levelAttemptCount = 0;

        isBannerActive = false;
        debugIsInterstitialReady = true;
        debugIsInterstitialReady = true;
        skinIndex = 0;
        incrementalLevelCount = 0;
    }
}