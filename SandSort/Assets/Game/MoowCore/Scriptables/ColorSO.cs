using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Color", menuName = "_ScriptableObjects/Color")]
public class ColorSO : ScriptableObject {    
    [SerializeField] ItemColor _color;
    public ItemColor color => _color;

    [Header("OBJECTIVE")]
    [SerializeField] Material _objectiveItemMaterial;

    public Material objectiveItemMaterial => _objectiveItemMaterial;

    public enum ItemColor {
    // --- BLUE TONLARI (100) ---
    [InspectorName("BLUE")]
    BLUE = 101,
    [InspectorName("BLUE_LIGHT")]
    BLUE_LIGHT = 102,
    [InspectorName("BLUE_DARK")]
    BLUE_DARK = 103,

    // --- BROWN TONLARI (110) ---
    [InspectorName("BROWN")]
    BROWN = 111,
    [InspectorName("BROWN_LIGHT")]
    BROWN_LIGHT = 112,
    [InspectorName("BROWN_DARK")]
    BROWN_DARK = 113,

    // --- GREEN TONLARI (120) ---
    [InspectorName("GREEN")]
    GREEN = 121,
    [InspectorName("GREEN_LIGHT")]
    GREEN_LIGHT = 122,
    [InspectorName("GREEN_DARK")]
    GREEN_DARK = 123,

    // --- PURPLE TONLARI (130) ---
    [InspectorName("PURPLE")]
    PURPLE = 131,
    [InspectorName("PURPLE_LIGHT")]
    PURPLE_LIGHT = 132,
    [InspectorName("PURPLE_DARK")]
    PURPLE_DARK = 133,

    // --- DİĞER ANA RENKLER ---
    [InspectorName("ORANGE")]
    ORANGE = 141,

    [InspectorName("PINK")]
    PINK = 151,

    [InspectorName("RED")]
    RED = 161,

    [InspectorName("WHITE")]
    WHITE = 171,

    [InspectorName("YELLOW")]
    YELLOW = 181,


    ALL = 1000,
    RANDOM = 1001,
    NONE = 1002,
    }

}