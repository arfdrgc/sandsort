using System;
using MoowCore.Enums;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoowCore.LoadingScreens
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private LoadingScreenType _type;

        [SerializeField, Space] private bool _enableSeasonTheme;
        // [SerializeField, EnableIf("@_enableSeasonTheme")] private SeasonType _seasonType;

        // [TitleGroup("Data")]
        // [SerializeField] protected TipsSO _tipsSo;

        [Header("References")]
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected GameObject _progressBar;
        [SerializeField] protected Image _progressImage;
        [SerializeField] protected TMP_Text _progressText;
        [SerializeField] protected TMP_Text _tipsText;

        [Header("Settings")]
        [SerializeField] protected float _progressDuration = .25f;
        [SerializeField] protected float _transitionDuration = .5f;

        protected Tweener _transitionTweener;
        protected Tweener _progressImageTweener;
        protected Tweener _progressTextTweener;
        protected float _progress;
        protected float _fakeProgress;
        protected bool _enableProgress;

        public bool EnableSeasonTheme => _enableSeasonTheme;
        // public SeasonType SeasonType => _seasonType;

        public virtual bool EnableProgress
        {
            get => _enableProgress;
            set => _progressBar.SetActive(_enableProgress = value);
        }

        public virtual float Progress
        {
            get => _progress;
            set => OnProgressChanged(_progress, _progress = Mathf.Clamp01(value));
        }

        public LoadingScreenType Type => _type;

        private void OnEnable()
        {
            if (_progressImage)
                _progressImage.fillAmount = 0f;

            // if (_tipsSo && _tipsText)
            //     _tipsText.text = _tipsSo.list.GetRandom();
        }

        protected virtual void OnProgressChanged(float oldValue, float newValue)
        {
            if (Math.Abs(oldValue - newValue) < .01f)
                return;

            if (_progressImage)
            {
                _progressImageTweener?.Kill();
                _progressImageTweener = _progressImage.DOFillAmount(newValue, _progressDuration).SetLink(gameObject);
            }

            if (_progressText)
            {
                _progressTextTweener?.Kill();
                _progressTextTweener = DOVirtual.Float(_fakeProgress, newValue, _progressDuration, value =>
                {
                    _fakeProgress = value;
                    _progressText.text = $"{value * 100:0}%";
                }).SetLink(gameObject);
            }
        }

        public void SetActive(bool toggle)
        {
            _canvasGroup.alpha = toggle ? 1 : 0;
            gameObject.SetActive(toggle);
        }

        public Tweener FadeIn()
        {
            if (!_canvasGroup)
                return null;

            gameObject.SetActive(true);

            _transitionTweener?.Kill();
            _transitionTweener = _canvasGroup.DOFade(1f, _transitionDuration).From(0f).SetLink(gameObject);
            return _transitionTweener;
        }

        public Tweener FadeOut()
        {
            if (!_canvasGroup)
                return null;

            _transitionTweener?.Kill();
            _transitionTweener = _canvasGroup.DOFade(0f, _transitionDuration).From(1f).SetLink(gameObject);
            _transitionTweener.onComplete = () => gameObject.SetActive(false);
            return _transitionTweener;
        }
    }
}