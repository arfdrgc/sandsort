using System;
using UnityEngine;

namespace Moow.Ads.Core
{
    public class AdsService
    {
        public static AdsService Instance { get; } = new();

        private IAdsProvider _primary;
        private IAdsProvider _fallback;
        private bool _rewardCallbackSent;

        public void Initialize(IAdsProvider primary, IAdsProvider fallback = null)
        {
            _primary = primary;
            _fallback = fallback;

            Debug.Log("[AdsService] Initialized");

            _primary.LoadRewarded();
            //_primary.LoadInterstitial();
        }

        public void ShowRewarded(Action<AdResult> callback)
        {
            if (_primary == null)
            {
                callback?.Invoke(AdResult.Failed);
                return;
            }

            if (!_primary.IsRewardedReady())
            {
                callback?.Invoke(AdResult.NotReady);
                return;
            }

            _primary.ShowRewarded(result =>
            {
                if (result == AdResult.Rewarded)
                {
                    callback?.Invoke(AdResult.Rewarded);
                    return;
                }

                // ❌ fallback'e raw callback VERME
                if (_fallback != null &&
                    (result == AdResult.Failed || result == AdResult.NotReady))
                {
                    _fallback.ShowRewarded(fallbackResult =>
                    {
                        // 🔥 IMPORTANT: normalize et
                        if (fallbackResult == AdResult.Rewarded)
                        {
                            callback?.Invoke(AdResult.Rewarded);
                        }
                        else
                        {
                            callback?.Invoke(fallbackResult);
                        }
                    });

                    return;
                }

                callback?.Invoke(result);
            });
        }

        public void ShowInterstitial(int level, Action<AdResult> callback = null)
        {
            if (_primary == null)
            {
                callback?.Invoke(AdResult.Failed);
                return;
            }

            _primary.ShowInterstitial(result =>
            {
                if (result == AdResult.Failed && _fallback != null)
                {
                    _fallback.ShowInterstitial(callback);
                    return;
                }

                callback?.Invoke(result);
            });
        }
    }
}