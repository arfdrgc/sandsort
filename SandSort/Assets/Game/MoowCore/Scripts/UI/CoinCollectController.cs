using System.Collections;
using DG.Tweening;
using Moow;
using Moow.Utility;
using MoowCore;
using TMPro;
using UnityEngine;

public class CoinCollectController : BaseSingleton<CoinCollectController>
{
    [SerializeField] private GameObject coinTurnGO, coinTop, coinsParent;
    [SerializeField] private int coinCount;
    [SerializeField] private Vector3[] coinPos;
    [SerializeField] private GameObject[] coins;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private TextMeshProUGUI _coinText;

    private void Start()
    {
        this.addListener<int>(Events.GIVE_COIN_ANIMATION, onGiveCoinAnim);
        coinPos = new Vector3[coinCount];
        coins = new GameObject[coinCount];

        float width = ((float)Screen.width / 2) - 50;

        for (int i = 0; i < coinCount; i++)
        {
            coinPos[i] = new Vector3(Random.Range(-width, width), Random.Range( -width * 2, -width / 2), 0);
        }
    }

    private void OnDisable()
    {
        this.removeListener<int>(Events.GIVE_COIN_ANIMATION, onGiveCoinAnim);
    }

    private void onGiveCoinAnim(UnityEngine.Object sender, Event<int> eventData)
    {
        float tempAmount = eventData.data;
        StartCoroutine(Collect(tempAmount));
    }

    private IEnumerator Collect(float tempAmount)
    {
        float coinStart = InventoryManager.instance.money;

        _canvasGroup.alpha = 1;
        coinTop.SetActive(true);

        for (int i = 0; i < 20; i++)
        {
            GameObject ct = Instantiate(coinTurnGO, canvas.transform);
            ct.transform.GetChild(1).gameObject.SetActive(false);
            coins[i] = ct;
            ct.transform.localPosition = coinPos[i];
            ct.transform.DOScale(0f, 0f);
            yield return new WaitForSeconds(0.01f);
            ct.transform.DOScale(3f, 0.1f);
        }

        Move(coinStart, tempAmount);
        
        coins = new GameObject[coinCount];
    }

    private void Move(float coinStart, float tempAmount)
    {
        float delay = 0f;
        float scaleDuration = 0.3f; //0.45f
        float eachPiece = tempAmount / coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            Transform tempCoin = coins[i].transform;
            tempCoin.DOScale(2f, scaleDuration).SetDelay(delay).SetEase(Ease.InBack);
            tempCoin.DOLocalMove(new Vector3(coinPos[i].x - 100, coinPos[i].y - 100, 0), scaleDuration).SetDelay(delay);

            tempCoin.DOMove(coinTop.transform.position, scaleDuration)
            .SetDelay(delay + scaleDuration)
            .SetEase(Ease.OutSine)
            .OnComplete(() => {

                 AudioPlayer.instance.playSFX(AudioFX.COIN_COLLECT);
                coinStart += eachPiece;
                _coinText.text = Helper.AbbreviateNumber((int)coinStart).ToString();

                coinTop.transform.DOScale(1.2f, 0.05f).OnComplete(() =>
                {
                    coinTop.transform.DOScale(1f, 0.05f);
                });
            });

            tempCoin.DOScale(0f, 0f).SetDelay(delay + (scaleDuration * 2)).SetEase(Ease.InBack).OnComplete(()=> { Destroy(tempCoin.gameObject); });
            delay += 0.05f;
        }

        DOVirtual.DelayedCall(2, ()=>
        {
            coinTop.SetActive(false);
            _canvasGroup.alpha = 0;
        });
    }
}
