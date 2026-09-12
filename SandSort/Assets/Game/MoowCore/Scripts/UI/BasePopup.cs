using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
// disable IDE1006 // Naming Styles

namespace MoowCore {
    public class BasePopup<TData> : Popup, IPopup {
        [SerializeField] protected float _fadeInDuration = .5f;
        [SerializeField] protected Transform _container;
        [SerializeField] protected CanvasGroup _canvasGroup;

        #region UNITY METHODS

        virtual protected void Awake() {

            _canvasGroup = transform.GetChild(0).GetComponent<CanvasGroup>();
        }

        virtual protected void Start() {

        }

        virtual protected void OnEnable() {

        }

        virtual protected void OnDisable() {

        }
        #endregion

        #region OVERRIDED
        #endregion

        #region METHOD
        virtual public void show(TData data) {
            _canvasGroup.DOFade(1, _fadeInDuration).From(0);
            _container.gameObject.SetActive(true);
        }

        virtual public void hide(float duration = .3f, bool initialize = false) {
            _canvasGroup.DOFade(0, duration);
            _container.gameObject.SetActive(false);
        }

        virtual public void initialize() {
            throw new System.NotImplementedException();
        }
        #endregion

        #region INTERFACE
        #endregion

        #region ACTION
        #endregion

        #region HELPER
        #endregion
    }
}