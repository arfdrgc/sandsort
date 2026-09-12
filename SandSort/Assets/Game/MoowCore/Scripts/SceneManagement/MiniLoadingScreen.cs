using DG.Tweening;
using UnityEngine;

namespace MoowCore.LoadingScreens
{
    public class MiniLoadingScreen : LoadingScreen
    {
        private Tweener _punchTweener;

        public override bool EnableProgress { get; set; }

        protected override void OnProgressChanged(float oldValue, float newValue)
        {
            base.OnProgressChanged(oldValue, newValue);
            
            _punchTweener?.Rewind();
            _punchTweener?.Kill();
            _punchTweener = _progressBar.transform.DOPunchScale(Vector3.one * .2f, .33f);
        }
    }
}