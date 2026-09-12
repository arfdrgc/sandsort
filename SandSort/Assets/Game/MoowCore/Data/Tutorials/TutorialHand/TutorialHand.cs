using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class TutorialHand : MonoBehaviour {

    [SerializeField] Image _image;
    [SerializeField] RectTransform _handRT;
    [SerializeField] Animator _animator;

    private float _screenWidth;
    private float _thresholdRatio;
    Tween _activeTween;

    private void Awake() {
        _screenWidth = Screen.width;
        _thresholdRatio = 0.5f;

        transform.forward = Camera.main.transform.forward;
    }

    private void setPosition(Vector3 pos) {
        _handRT.position = pos;
    }

    public void setAnimation(TutorialHandAnimation handAnimation) {
        if(handAnimation == TutorialHandAnimation.TAP) {
            _animator.SetTrigger("Tap");
        } else if(handAnimation == TutorialHandAnimation.DOUBLE_TAP) {
            _animator.SetTrigger("DoubleTap");
        } else if(handAnimation == TutorialHandAnimation.HOLD) {
            _animator.SetTrigger("Hold");
        } else if(handAnimation == TutorialHandAnimation.RELEASE) {
            _animator.SetTrigger("Release");
        }else if(handAnimation == TutorialHandAnimation.IDLE) {
            _animator.SetTrigger("Idle");
        }else if(handAnimation == TutorialHandAnimation.TAP_AND_HOLD) {
            _animator.SetTrigger("TapAndHold");
        }
    }

    public void show(Vector3 position) {
        gameObject.SetActive(true);
        _handRT.position = position;
        _animator.enabled = true;
    }

    public void hide() {
        gameObject.SetActive(false);
        _animator.enabled = false;
    }

    private Coroutine _activeCoroutine;

    public void dragAnimation(
        Func<Vector3> startPositionCalculator, Func<Vector3> endPositionCalculator,
        float startDelay, float moveDuration, float endDelay,
        Ease easeX = Ease.OutQuad, Ease easeY = Ease.OutQuad, Ease easeZ = Ease.OutQuad) {
        if(_activeCoroutine != null) {
            StopCoroutine(_activeCoroutine);
        }

        _activeCoroutine = StartCoroutine(dragAnimationRoutine(
            startPositionCalculator, endPositionCalculator, startDelay, moveDuration, endDelay, easeX, easeY, easeZ));
    }

    private IEnumerator dragAnimationRoutine(Func<Vector3> startPositionCalculator, Func<Vector3> endPositionCalculator,
        float startDelay, float moveDuration, float endDelay,
        Ease easeX, Ease easeY, Ease easeZ) {

        Vector3 calculatePosition(float t) {
            Vector3 startPosition = startPositionCalculator();
            Vector3 endPosition = endPositionCalculator();

            float easedX = Mathf.Lerp(startPosition.x, endPosition.x, DOVirtual.EasedValue(0, 1, t, easeX));
            float easedY = Mathf.Lerp(startPosition.y, endPosition.y, DOVirtual.EasedValue(0, 1, t, easeY));
            float easedZ = Mathf.Lerp(startPosition.z, endPosition.z, DOVirtual.EasedValue(0, 1, t, easeZ));

            return new Vector3(easedX, easedY, easedZ);
        }

        while(true) {
            setAnimation(TutorialHandAnimation.HOLD);
            float elapsed = 0f;
            while((elapsed += Time.deltaTime) < startDelay) {
                transform.position = calculatePosition(0);
                yield return null;
            }

            elapsed = 0f;
            while((elapsed += Time.deltaTime) < moveDuration) {
                transform.position = calculatePosition(elapsed / moveDuration);
                yield return null;
            }

            elapsed = 0f;
            setAnimation(TutorialHandAnimation.RELEASE);
            while((elapsed += Time.deltaTime) < endDelay) {
                transform.position = calculatePosition(1);
                yield return null;
            }
        }
    }
}

public enum TutorialHandAnimation {
    TAP,
    DOUBLE_TAP,
    HOLD,
    RELEASE,
    IDLE,
    TAP_AND_HOLD,
}