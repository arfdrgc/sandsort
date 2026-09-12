using System.Collections.Generic;
using UnityEngine;
using static ColorSO;

[CreateAssetMenu(fileName = "ColorPalette", menuName = "_ScriptableObjects/ColorPalette", order = 0)]
public class ColorPaletteSO : ScriptableObject 
{
    [SerializeField] private List<ColorMapEntry> _colorMap = new();

    private Dictionary<ItemColor, Color> _colorLookup;

    public Color GetColor(ItemColor c)
    {
        if (_colorLookup == null || _colorLookup.Count == 0)
            BuildColorLookup();

        return _colorLookup.TryGetValue(c, out var col) ? col : Color.magenta;
    }

    private void BuildColorLookup()
    {
        _colorLookup = new Dictionary<ItemColor, Color>();
        foreach (var entry in _colorMap)
        {
            if (entry == null) continue;
            _colorLookup[entry.color] = entry.unityColor;
        }
    }

    private void OnValidate()
    {
        _colorLookup = null; // Editor'de renk değiştirilirse lookup tablosunu yenile
    }
}

[System.Serializable]
public class ColorMapEntry {
    public ItemColor color;
    public Color unityColor;
}