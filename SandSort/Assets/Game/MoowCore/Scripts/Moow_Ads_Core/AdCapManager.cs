using UnityEngine;

namespace Moow.Ads.Cap
{
    public class AdCapManager
    {
        private float _sessionStart;
        private float _lastInterstitial;
        private float _lastRewarded;

        public int NoAdsFirstLevels = 2;
        public float InterstitialCooldown = 45f;
        public float RewardedBlockTime = 60f;

        public void Initialize()
        {
            _sessionStart = Time.realtimeSinceStartup;
        }

        public bool CanShowInterstitial(int level)
        {
            if (level <= NoAdsFirstLevels)
                return false;

            if (Time.realtimeSinceStartup - _lastInterstitial < InterstitialCooldown)
                return false;

            if (Time.realtimeSinceStartup - _sessionStart < 30f)
                return false;

            if (Time.realtimeSinceStartup - _lastRewarded < RewardedBlockTime)
                return false;

            return true;
        }

        public void OnInterstitialShown()
        {
            _lastInterstitial = Time.realtimeSinceStartup;
        }

        public void OnRewardedShown()
        {
            _lastRewarded = Time.realtimeSinceStartup;
        }
    }
}