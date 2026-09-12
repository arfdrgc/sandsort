using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "PowerUpSO", menuName = "_ScriptableObjects/PowerUpSO")]
public class PowerUpSO : ScriptableObject {
    [SerializeField] string _itemID;
    [SerializeField] Sprite _sprite;
    [SerializeField] Sprite _spriteLocked;
    [SerializeField] int _goldCost;
    [SerializeField] int _unlockLevel;
    [SerializeField] PowerUpType _type;
    [SerializeField] string _popupTitle;
    [SerializeField, TextArea] string _popupText;

    public string itemID => _itemID;
    public Sprite sprite => _sprite;
    public Sprite spriteLocked => _spriteLocked;
    public int goldCost => _goldCost;
    public PowerUpType type => _type;
    public int unlockLevel => _unlockLevel;
    public string popupTitle => _popupTitle;
    public string popupText => _popupText;
}

public enum PowerUpType {
    PUT_1,
    PUT_2,
    PUT_3,
    PUT_4
}
