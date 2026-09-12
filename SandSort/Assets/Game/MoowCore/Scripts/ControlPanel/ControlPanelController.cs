using Moow;
using MoowCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlPanelController : MonoBehaviour {
    [SerializeField] DataSO data;
    [SerializeField] InventoryDataSO inventory;

    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] TextMeshProUGUI goldText;
    //[SerializeField] Toggle skipAdstoggle;
    [SerializeField] Dropdown languageDropdown;
    [SerializeField] GameObject panel;

    [Header("Quick Skip Level")]
    [SerializeField] private bool isQuickSkipActive;
    [SerializeField] Text quickSkipText;
    [SerializeField] GameObject quickSkipController;

    [SerializeField] TextMeshProUGUI fpsText;

    [Header("Recorder Panel")]
    //[SerializeField] private bool isRecorderActive;
    //[SerializeField] private Text recorderStatusText;
    //[SerializeField] GameObject recorderPanel;

    int check3FingerDuration = 60;
    int check3FingerCounter;
    private float timer = 0f;

    void Start() {
        check3FingerCounter = check3FingerDuration;
        updateData();
        gameObject.SetActive(true);
        panel.SetActive(false);
        //languageDropdown.value = data.skinIndex;
        //languageDropdown.onValueChanged.AddListener(onSkinChanged);
        //setQuicSkipPanel();
    }
    // Update is called once per frame
    void Update() {
#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)) {
            toggleShow();
        }

        if(Input.GetKeyDown(KeyCode.RightArrow)) {
            goToNextLevel();
        } else if(Input.GetKeyDown(KeyCode.LeftArrow)) {
            goToPreviousLevel();
        }

        if(Input.GetKeyDown(KeyCode.F)) {
            this.dispatchEvent<object>(Events.LEVEL_OBJECTIVE_COMPLETE, null);
        }
        if(Input.GetKeyDown(KeyCode.G)) {
            this.dispatchEvent<object>(Events.FAIL_CONDITION_MET, null);
        }
#endif
        if(check3Finger()) {
            check3FingerCounter--;
        } else {
            reset3FingerCounter();
        }

        if(check3FingerCounter <= 0) {
            toggleShow();
        }

        if(panel.activeSelf)
        {
            timer += Time.unscaledDeltaTime;

            // Eğer 1 saniye (veya belirlediğin süre) geçtiyse içerideki kodu çalıştır
            if (timer >= 1.0f)
            {
                // 1 saniyeyi, son karenin süresine bölerek o anki FPS'i hesaplıyoruz
                int currentFPS = (int)(1f / Time.unscaledDeltaTime);
                
                // Ekrana yazdırıyoruz
                fpsText.text = currentFPS + " FPS";
                
                // Zamanlayıcıyı sıfırlıyoruz ki bir sonraki saniyeyi saymaya başlasın
                timer = 0f; 
            }
        }
    }

    void toggleShow() {
        reset3FingerCounter();
        panel.SetActive(!panel.activeSelf);
        updateData();
    }

    void reset3FingerCounter() {
        check3FingerCounter = check3FingerDuration;
    }

    bool check3Finger() {
        if(Input.touches.Length == 3) {
            if(Input.touches[0].position.y > Screen.height * 0.5f && Input.touches[1].position.y > Screen.height * 0.5f && Input.touches[2].position.y > Screen.height * 0.5f) {
                return true;
            }
        }

        return false;
    }

    public void hidePanelNoReload() {
        panel.SetActive(false);
    }

    public void hidePanel() {
        panel.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
    }

    public void increaseLevel(int level) {

        int tempLevel = data.level + level;

        if(tempLevel < 0)
            data.level = 0;
        else
            data.level += level;
            
        updateData();
    }

    public void increaseGold(int gold) {
        InventoryManager.instance.increase(gold);
        updateData();
        //this.dispatchEvent(new Event<int>(Events.GOLD_CHANGED, inventory.money));
    }

    public void goToNextLevel() {
        data.level += 1;
        updateData();
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
    }

    public void goToPreviousLevel() {

        if(data.level > 0)
        {
            data.level -= 1;
            updateData();
        }
    
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
    }

    public void setSkipAds(bool skipAds) {
        data.disableAds = skipAds;
    }

    public void resetSave() {
        Database.instance.ResetDatabase();
        Database.instance.Save();
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
    }

    void updateData() {
        goldText.text = "Money: " + inventory.money;
        levelText.text = "Level: " + (data.level + 1);
        //skipAdstoggle.isOn = data.disableAds;
        Database.SaveGame();
    }

    public void goToNextLevelWithImpossibleRecord()
    {
        this.dispatchEvent<object>(Events.RECORD_IMPOSSIBLE_LEVEL, null);
        data.level += 1;
        updateData();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }


    public void onRateUsPressed() {
        //this.dispatchEvent(new Event<int>(Events.RATE_US_NEED_SHOW, 0));
    }

    public void onMechanicCompletePressed() {
        //this.dispatchEvent(new Event<Mechanic>(Events.MECHANIC_COMPLETED, null));
    }

    public void onLevelCompletePressed() {
        this.dispatchEvent(new Event<object>(Events.LEVEL_COMPLETED, null));
    }

    public void onLevelFailedPressed() {
        this.dispatchEvent(new Event<object>(Events.LEVEL_FAILED, null));
    }

    public void showRewarded() {
        void onSuccess() {
            Debug.Log("showRewarded onSuccess");
        };

        void onFail() {
            Debug.Log("showRewarded onFail");
        };

        // RollicAdsManager.instance.showRewarded(onSuccess, onFail);
        Debug.Log("showRewarded()");
    }

    public void showInterstitial() {
        // RollicAdsManager.instance.showInterstitial();
        Debug.Log("showInterstitial()");
    }

    public void showBanner() {
        // RollicAdsManager.instance.loadBanner();
        Debug.Log("showBanner()");
    }

    public void addHeart() {
        //this.dispatchEvent<object>(Events.LEVEL_FAILED, null);
    }

    public void addSpeed() {
        Time.timeScale = Time.timeScale + 1;
        if(Time.timeScale >= 5) {
            Time.timeScale = 1;
        }
    }

    private void setQuicSkipPanel()
    {
        isQuickSkipActive = InventoryManager.instance.QuickSkipStatus;

        if (isQuickSkipActive)
            quickSkipText.text = "Quick Skip Status : Active";
        else
            quickSkipText.text = "Quick Skip Status : Passive";

        quickSkipController.SetActive(isQuickSkipActive);
    }

    public void updateQuickSkipStatus()
    {
        if (isQuickSkipActive)
        {
            isQuickSkipActive = false;
            quickSkipText.text = "Quick Skip Status : Passive";
        }
        else
        {
            isQuickSkipActive = true;
            quickSkipText.text = "Quick Skip Status : Active";
        }

        InventoryManager.instance.QuickSkipStatus = isQuickSkipActive;
        quickSkipController.SetActive(isQuickSkipActive);
    }

    public void onSkinChanged(int skinIndex) {
        data.skinIndex = skinIndex;
        Database.instance.Save();

        this.dispatchEvent("SkinChanged", skinIndex);
    }

    public void setDarkTheme()
    {
        Debug.Log("SetDarkTheme");
        this.dispatchEvent(new Event<object>(Events.SET_DARK_THEME, null));

        GameObject wall = GameObject.Find("ModularWall");

        if (wall != null)
        {
            Renderer rend = wall.GetComponent<Renderer>();
            if (rend != null)
            {
                //TODO setDarkTheme control panel
                //rend.material = SkinManager.instance.wallDarkThemeMaterial;
            }
        }
    }
    public void OpenMediationDebugger()
    {
        //MaxSdk.ShowMediationDebugger();
    }
}
