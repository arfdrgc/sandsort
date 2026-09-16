using DG.Tweening;
using Moow;
using MoowCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Level complete coin burst: coins pop out around the screen centre, then fly one after another into
// the real Gold UI (UIGoldContainer.iconTarget). Each landing sends UI_GOLD_ANIMATION_PROGRESS with its
// share of the amount, so the gold counter rises with the coins. The old coinTop counter (its own
// background, icon and text under _canvasGroup) is no longer shown.
public class CoinCollectController : BaseSingleton<CoinCollectController>
{
    [SerializeField] private GameObject coinTurnGO, coinTop, coinsParent;
    [SerializeField] private int coinCount;
    [SerializeField] private Vector3[] coinPos;
    [SerializeField] private GameObject[] coins;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private TextMeshProUGUI _coinText;

    // Burst timing (seconds) and size. With 20 coins the last one lands after
    // BURST_DURATION + HOLD + 19 * FLY_STAGGER + FLY_DURATION ≈ 1.5 s.
    const float BURST_RADIUS_RATIO = 0.22f; // of the canvas width
    const float BURST_DURATION = 0.25f;
    const float BURST_STAGGER = 0.01f;
    const float HOLD = 0.1f;
    const float FLY_DURATION = 0.45f;
    const float FLY_STAGGER = 0.035f;
    const float COIN_SCALE = 3f;

    private void Start()
    {
        this.addListener<int>(Events.GIVE_COIN_ANIMATION, onGiveCoinAnim);
    }

    private void OnDisable()
    {
        this.removeListener<int>(Events.GIVE_COIN_ANIMATION, onGiveCoinAnim);
    }

    private void onGiveCoinAnim(UnityEngine.Object sender, Event<int> eventData)
    {
        burst(eventData.data);
    }

    void burst(int amount)
    {
        int count = Mathf.Max(1, coinCount);
        RectTransform canvasRect = (RectTransform)canvas.transform;
        UIGoldContainer gold = FindFirstObjectByType<UIGoldContainer>();
        Vector3 target = targetWorldPosition(gold, canvasRect);
        float radius = canvasRect.rect.width * BURST_RADIUS_RATIO;

        for (int i = 0; i < count; i++)
        {
            // Whole-number shares that add up to exactly `amount`.
            int piece = amount / count + (i < amount % count ? 1 : 0);

            GameObject coin = Instantiate(coinTurnGO, canvas.transform);
            hideBrokenChildren(coin);

            Transform t = coin.transform;
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.zero;
            Vector3 offset = Random.insideUnitCircle * radius;

            float burstDelay = i * BURST_STAGGER;
            float flyDelay = BURST_DURATION + HOLD + i * FLY_STAGGER;

            DOTween.Sequence()
                .Insert(burstDelay, t.DOLocalMove(offset, BURST_DURATION).SetEase(Ease.OutBack))
                .Insert(burstDelay, t.DOScale(COIN_SCALE, BURST_DURATION).SetEase(Ease.OutBack))
                .Insert(flyDelay, t.DOMove(target, FLY_DURATION).SetEase(Ease.InSine))
                .Insert(flyDelay, t.DOScale(COIN_SCALE * 0.5f, FLY_DURATION).SetEase(Ease.InSine))
                .OnComplete(() => {
                    arrive(gold, piece);
                    Destroy(coin);
                })
                .SetLink(coin);
        }
    }

    void arrive(UIGoldContainer gold, int piece)
    {
        AudioPlayer.instance.playSFX(AudioFX.COIN_COLLECT);
        this.dispatchEvent<float>(Events.UI_GOLD_ANIMATION_PROGRESS, piece);

        if (gold != null)
        {
            Transform icon = gold.iconTarget;
            icon.DOKill(true);
            icon.DOPunchScale(Vector3.one * 0.25f, 0.12f, 1, 0f);
        }
    }

    // The gold icon lives on the game scene's camera canvas, the coins on this overlay canvas: go
    // through screen space to get the icon's position in this canvas.
    Vector3 targetWorldPosition(UIGoldContainer gold, RectTransform canvasRect)
    {
        Transform icon = gold != null ? gold.iconTarget : coinTop.transform;
        Canvas iconCanvas = icon.GetComponentInParent<Canvas>().rootCanvas;
        Camera cam = iconCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : iconCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, icon.position);
        RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screen, null, out Vector3 world);
        return world;
    }

    // MoneyTurnAnim carries two children that do not render correctly on this canvas: an Image with no
    // sprite (a white square) and "Sparks", a UIParticleSystem whose additive particle material
    // (glow1_ADD, Particles/Standard Unlit) draws as black squares under URP. The old code hid Sparks by
    // index (GetChild(1)); both are now hidden by what they are, and the animated coin stays as it is.
    static void hideBrokenChildren(GameObject coin)
    {
        foreach (Image image in coin.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) image.enabled = false;
        }

        Transform sparks = coin.transform.Find("Sparks");
        if (sparks != null) sparks.gameObject.SetActive(false);
    }
}
