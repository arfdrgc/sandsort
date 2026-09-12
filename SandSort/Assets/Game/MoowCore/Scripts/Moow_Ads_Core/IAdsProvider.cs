namespace Moow.Ads.Core
{
    public interface IAdsProvider
    {
        void LoadInterstitial();
        void LoadRewarded();

        bool IsInterstitialReady();
        bool IsRewardedReady();

        void ShowInterstitial(System.Action<AdResult> callback);
        void ShowRewarded(System.Action<AdResult> callback);
    }
}