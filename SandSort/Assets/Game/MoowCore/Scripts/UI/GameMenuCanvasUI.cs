using Moow;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;

namespace MoowCore {
    public class GameMenuCanvasUI : MonoBehaviour {
        [SerializeField] DataSO _dataSO;
        [SerializeField] GameSuccessPopup _successPopup;
        [SerializeField] GameFailurePopup _failurePopup;

        [SerializeField] float _showSuccessDelay;
        [SerializeField] float _showFailDelay;

        [SerializeField] GameObject _container;

        Tween _showFailedTween;

        [SerializeField] Transform topArea, normalArea, booster, boosterTuto;

        private void Awake() {
            _successPopup.hide();
            _failurePopup.hide();
        }

        private void OnEnable() {
            this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
            this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
            this.addListener<object>(Events.FAIL_CONDITION_MET, onLevelFailed);
            this.addListener<object>(Events.CPI_ACTIVE, onCpiActive);
        }

        private void OnDisable() {
            this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
            this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
            this.removeListener<object>(Events.FAIL_CONDITION_MET, onLevelFailed);
            this.removeListener<object>(Events.CPI_ACTIVE, onCpiActive);
        }

        private void onCpiActive(Object sender, Event<object> eventData) {
            _container.SetActive(false);
        }


        private void onLevelFailed(Object sender, Event<object> eventData) {
            _showFailedTween = DOVirtual.DelayedCall(_showFailDelay, () => {
                _failurePopup.show(null);
            });
        }

        private void onLevelCompleted(Object sender, Event<object> eventData) {

            Debug.Log("GameMenu-Completed");
            // DOVirtual.DelayedCall(_showSuccessDelay, () => {
            //     _successPopup.show(null);
            // });

             _successPopup.show(null);
            _showFailedTween?.Kill();
        }

        private void onLevelLoaded(Object sender, Event<object> eventData) {
            _successPopup.hide();
            _failurePopup.hide();
        }
    }
}