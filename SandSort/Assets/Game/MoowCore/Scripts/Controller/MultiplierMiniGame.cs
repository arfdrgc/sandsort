using MoowCore;
using UnityEngine;

public class MultiplierMiniGame : MonoBehaviour
{
    [SerializeField] private GameSuccessPopup _popup;
    
    public void CheckMultiplier(float score)
    {
       _popup.UpdateDoubleButtonAmount(score);
    }
}