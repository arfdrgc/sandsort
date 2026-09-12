using UnityEngine;
using UnityEngine.SceneManagement;
using Moow;
using MoowCore;
using DG.Tweening;
using System;
using Cysharp.Threading.Tasks;

// ENABLE_ANALYTICS proprocessor macro defined in (Project Settings -> Player -> Scripting Define Symbols)

public class ApplicationInitializer : SingletonDontDestroy<ApplicationInitializer> {

    #region BASE
    protected override void Awake() {
        base.Awake();

#if ENABLE_ANALYTICS
        initializeGameAnalytics();
        initializeFacebookSDK();
#endif
        Application.targetFrameRate = 60;

        DebugTimer.initialize();

        initializeDoTween();

        //Database.instance.checkDatabaseOrder();
    }

    [RuntimeInitializeOnLoadMethod]
    static void RunOnStart() {
        Application.lowMemory += OnLowMemory;
    }

    void Start() {
        applicationDidStart();
        StartCoroutine(ForceSixtyFPSCoroutine());
        //StartCoroutine(SafeFPSKeeperRoutine());
    }

    private System.Collections.IEnumerator SafeFPSKeeperRoutine()
    {
        // SingletonDontDestroy sayesinde oyun kapanana kadar tüm sahnelerde arka planda çalışır
        while (true)
        {
            if (Application.targetFrameRate != 60 || QualitySettings.vSyncCount != 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[FPS Keeper] Kare hızı global olarak 60'a tazelendi. Mevcut: " + Application.targetFrameRate);
                #endif
            }

            // İşlemciyi yormamak için 3 saniyede bir kontrol dalgası atar
            yield return new WaitForSecondsRealtime(3f);
        }
    }

    void OnDisable() {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnEnable() {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnApplicationQuit() {
        setDatabaseOperations();
    }

    static void OnLowMemory() {
        Debug.Log("[ApplicationInitializer::OnLowMemory]");
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    void OnApplicationFocus(bool focus) {
        if(focus) {
            Debug.Log("[ApplicationInitializer::OnApplicationFocus]");
            updateGameData();
        }
    }

    // Unity will call OnApplicationPause(false) when an app is resumed
    // from the background
    void OnApplicationPause(bool pauseStatus) {
        //Check the pauseStatus to see if we are in the foreground
        //or background
        Debug.Log("[ApplicationInitializer::OnApplicationPause] pause: " + pauseStatus);
        if(!pauseStatus) {
            //app resume
// #if ENABLE_ANALYTICS
//             if (FB.IsInitialized) {
//                 FB.ActivateApp();
//             }
// #endif
        }
    }
    #endregion

    #region METHODS
    void applicationDidStart() {
        // If vSyncCount takes any value but 0, targetFrameRate value which
        // is set by us will be ignored.
        Debug.Log("[ApplicationInitializer::applicationDidStart]");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        setLanguage();
        setConfigData();
        updateGameData();
        setDatabaseOperations();
    }

    System.Collections.IEnumerator ForceSixtyFPSCoroutine()
    {
        yield return new WaitForEndOfFrame();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Debug.Log("[FPS] Target Frame Rate zorla 60 yapıldı. Mevcut: " + Application.targetFrameRate);
    }

#if ENABLE_ANALYTICS
    void initializeFacebookSDK() {
        // if (!FB.IsInitialized) {
        //     // Initialize the Facebook SDK
        //     FB.Init(initCallback, onHideUnity);
        // } else {
        //     // Already initialized, signal an app activation App Events
        //     FB.ActivateApp();
        // }
    }

    void initializeGameAnalytics() {
        // GameAnalytics.Initialize();
    }
#endif


    virtual protected void setLanguage() {
        //if (!configData.isAppLanched) {
        //    string systemLanguage = Application.systemLanguage.ToString();
        //    configData.language = systemLanguage;
        //}
    }
    virtual protected void setConfigData() {
        //if (configData.isAppLanched == false) {
        //    // Do not take offline earning in first app open.
        //    // Assume as if it is already taken.
        //}
        //configData.isAppLanched = true;
    }
    virtual protected void updateGameData() {
        //long gameLaunchedCount = gameData.gameLaunched();
        //Analytics.instance.appLaunch(gameLaunchedCount);
    }
    virtual protected void setDatabaseOperations() {
        //Database.instance.saveGame();
    }

    void initializeDoTween()
    {
        DOTween.Init(recycleAllByDefault: false, useSafeMode: true, LogBehaviour.Default).SetCapacity(500, 250);
    }
    #endregion

    #region ACTIONS
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Debug.Log("Loaded scene: " + scene.name + " mode: " + mode.ToString());
        Event<Scene> e = new Event<Scene>("SceneLoaded", scene);
        var dispatcher = Dispatcher.instance;
        if(dispatcher != null) {
            dispatcher.dispatch(this, e);
        }
        else {
            // CrashlyticsProxy.LogException($"[ApplicationInitializer::OnSceneLoaded] Dispatcher is null!");
        }
    }

#if ENABLE_ANALYTICS
    void initCallback() {
        // Debug.Log("[ApplicationInitializer::initCallback]");
        // if (FB.IsInitialized) {
        //     // Signal an app activation App Events
        //     FB.ActivateApp();
        //     // Continue with Facebook SDK
        //     // ...
        // } else {
        //     Debug.Log("[AppplicationInitializer::]Failed to Initialize the Facebook SDK");
        // }
    }
#endif

    void onHideUnity(bool isGameShown) {
        if(!isGameShown) {
            // Pause the game - we will need to hide
            Time.timeScale = 0;
        }
        else {
            // Resume the game - we're getting focus again
            Time.timeScale = 1;
        }
    }
    #endregion
}