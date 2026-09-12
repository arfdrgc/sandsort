using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Moow;
using MoowCore.Enums;
using MoowCore.LoadingScreens;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoowCore
{
    public class SceneManager : BaseSingleton<SceneManager>
    {
        public enum TransitionType
        {
            None,
            Fading
        }

        [Header("References")]
        [SerializeField] private LoadingScreen[] _loadingScreens;

        [Header("Settings")]
        [SerializeField] private float _finishWait;
        [SerializeField] private float _initialDelay;
        [SerializeField] private string _initialSceneName;

        private List<AsyncOperation> _sceneOperations;
        private Tweener _transitionTweener;
        private Action<string> _onSceneLoaded;

        private string _currentScene;
        private TransitionType _currentTransitionType;
        private LoadingScreen _currentLoadingScreen;

        private List<Func<bool>> _customWaitFunctions;

        public string CurrentScene => _currentScene;

        protected override void Awake()
        {
            base.Awake();
            foreach (var loadingScreen in _loadingScreens)
            {
                loadingScreen.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            StartCoroutine(OperationCoroutine());

            //todo _initialScene Main or Game
            //Scene Manager

            //if (currentLevelNum >= 10)
            //    _initialSceneName = "MainScene";
            //else
            //    _initialSceneName = "GameScene";

            _initialSceneName = "GameScene";

            LoadScene(_initialSceneName, LoadingScreenType.Main, delay: _initialDelay);
        }

        private void OnSceneLoaded()
        {
            ToggleLoadingScreen(_currentLoadingScreen, _currentTransitionType, false);
            _currentLoadingScreen = null;

            _onSceneLoaded?.Invoke(_currentScene);
            _onSceneLoaded = null;
        }

        public void LoadScene(string sceneName, LoadingScreenType loadingScreenType, TransitionType transitionType = TransitionType.None, float delay = 1f, Action<string> onSceneLoaded = null)
        {
            _customWaitFunctions = new List<Func<bool>>();
            _onSceneLoaded = onSceneLoaded;

            if (_currentLoadingScreen)
                _currentLoadingScreen.gameObject.SetActive(false);

            _currentLoadingScreen = GetLoadingScreen(loadingScreenType);

            _transitionTweener?.Kill();
            _transitionTweener = ToggleLoadingScreen(_currentLoadingScreen, _currentTransitionType = transitionType, true);
            if (_transitionTweener == null)
            {
                transitionCompleted();
            }
            else
            {
                _transitionTweener.onComplete = transitionCompleted;
            }

            void transitionCompleted()
            {
                _currentLoadingScreen.EnableProgress = false;

                if (delay > 0)
                    DOVirtual.DelayedCall(delay, initializeOperations);
                else
                    initializeOperations();
            }

            void initializeOperations()
            {
                _currentLoadingScreen.EnableProgress = true;

                _sceneOperations ??= new List<AsyncOperation>();
                if (!string.IsNullOrWhiteSpace(_currentScene))
                {
                    var unloadOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_currentScene);
                    _sceneOperations.Add(unloadOperation);
                }

                var loadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_currentScene = sceneName, LoadSceneMode.Additive);
                loadOperation.completed += x =>
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_currentScene);
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                };

                _sceneOperations.Add(loadOperation);
            }
        }

        public void AddCustomWaitFunction(Func<bool> customFunc)
        {
            _customWaitFunctions?.Add(customFunc);
        }

        public LoadingScreen GetLoadingScreen(LoadingScreenType type)
        {
            LoadingScreen retLoadingScreen = null;

            var screens = _loadingScreens.Where(x => x.Type == type);
            foreach (var loadingScreen in screens)
            {
                if (!loadingScreen.EnableSeasonTheme)
                {
                    retLoadingScreen = loadingScreen;
                    break;
                }

                // if (loadingScreen.EnableSeasonTheme && loadingScreen.SeasonType == SeasonManager.instance.CurrentSeason)
                // {
                //     retLoadingScreen = loadingScreen;
                //     break;
                // }
            }

            return retLoadingScreen;
        }

        public Tweener ToggleLoadingScreen(LoadingScreen loadingScreen, TransitionType transitionType, bool toggle)
        {
            switch (transitionType)
            {
                case TransitionType.None:
                {
                    loadingScreen.SetActive(toggle);
                    return null;
                }

                case TransitionType.Fading:
                {
                    return toggle ? loadingScreen.FadeIn() : loadingScreen.FadeOut();
                }

                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(transitionType), transitionType, null);
                }
            }
        }

        private IEnumerator OperationCoroutine()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                if (_sceneOperations == null || _sceneOperations.Count == 0)
                {
                    yield return wait;
                    continue;
                }

                var totalOperation = _sceneOperations.Count;
                var progress = Mathf.Clamp01(_sceneOperations.Sum(x => x?.progress ?? 0) / totalOperation);
                _currentLoadingScreen.Progress = progress;

                if (progress >= 1f)
                {
                    _sceneOperations.Clear();

                    yield return new WaitForSeconds(_finishWait);
                    if (_customWaitFunctions != null)
                    {
                        foreach (var waitFunction in _customWaitFunctions)
                            yield return new WaitUntil(waitFunction);
                    }

                    OnSceneLoaded();
                }

                yield return wait;
            }
        }
    }
}