using System;
using Moow.Ads.Core;

namespace Moow.Ads.Providers.AdMob
{
    public class AdMobAdsProvider : IAdsProvider
    {
        public bool IsInterstitialReady() => true;
        public bool IsRewardedReady() => true;

        public void LoadInterstitial() { }
        public void LoadRewarded() { }

        public void ShowInterstitial(Action<AdResult> cb)
        {
            cb?.Invoke(AdResult.Displayed);
        }

        public void ShowRewarded(Action<AdResult> cb)
        {
            cb?.Invoke(AdResult.Rewarded);
        }
    }
}