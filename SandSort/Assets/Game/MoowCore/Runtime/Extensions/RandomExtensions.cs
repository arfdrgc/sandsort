using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Moow {
    static public class RandomExtensions {
        static public T getRandomElement<T>(this IList<T> collection) {
            return collection[Random.Range(0, collection.Count)];
        }

        static public float randomBetweenXY(this Vector2 v) {
            return Random.Range(v.x, v.y);
        }

        static public int randomBetweenXYInt(this Vector2 v) {
            return Random.Range((int)v.x, (int)v.y + 1);
        }
    }
}


[System.Serializable]
public class WeightedRandomizer<T> {
    [SerializeField] public List<WeightedRandomizerItem<T>> items;
    [SerializeField] public float totalWeight = -1;

    [Button]
    public T getRandom() {
        totalWeight = 0;
        foreach(WeightedRandomizerItem<T> item in items) {
            totalWeight += item.weight;
        }

        float random = Random.Range(0, totalWeight);
        foreach(WeightedRandomizerItem<T> item in items) {
            if(random < item.weight) {
                return item.value;
            }

            random -= item.weight;
        }

        return items[0].value;
    }

    [Button]
    public T getRandomWithExclude(List<T> excludeList = null) {
        if(excludeList == null || excludeList.Count == 0) {
            return getRandom();
        }

        List<WeightedRandomizerItem<T>> includedItems = new List<WeightedRandomizerItem<T>>();
        foreach(WeightedRandomizerItem<T> item in items) {
            if(excludeList.Contains(item.value) == false) {
                includedItems.Add(item);
            }
        }

        totalWeight = 0;
        foreach(WeightedRandomizerItem<T> item in includedItems) {
            totalWeight += item.weight;
        }

        float random = Random.Range(0, totalWeight);
        foreach(WeightedRandomizerItem<T> item in includedItems) {
            if(random < item.weight) {
                return item.value;
            }

            random -= item.weight;
        }

        return items[0].value;
    }
}

[System.Serializable]
public class WeightedRandomizerItem<T> {
    public T value;
    public float weight = 1;
}