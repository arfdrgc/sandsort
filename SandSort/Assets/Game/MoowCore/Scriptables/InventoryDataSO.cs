using Moow;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoowCore {

    [CreateAssetMenu(fileName = "Inventory Data", menuName = "_ScriptableObjects/Inventory Data")]
    public class InventoryDataSO : BaseScriptableObject {
        public int heart;
        public long _lastHearthUpdate;
        public float money = 0;
        public List<ItemSaveData> _items;
        public bool isRecorderActive;
        public bool isQuickSkipActive;

        public DateTime lastHearthUpdate {
            get {
                return new DateTime(_lastHearthUpdate);
            }
            set {
                _lastHearthUpdate = value.Ticks;
            }
        }

        private void OnEnable() {
        }

        public void increase(float value) {
            money += value;

            Database.SaveGame();
        }

        public void decrease(float value) {
            money -= value;
            money = Mathf.Max(money, 0);

            Database.SaveGame();
        }

        public void UpdateRecorderStatus(bool status)
        {
            isRecorderActive = status;
            Database.SaveGame();
        }

        public void UpdatQuickSkipStatus(bool status)
        {
            isQuickSkipActive = status;
            Database.SaveGame();
        }

        public override void reset() {
            base.reset();

            isRecorderActive = false;
            isQuickSkipActive = false;
            money = 1000;
            heart = 5;
            _lastHearthUpdate = 0;
            _items = new List<ItemSaveData> {
                new ItemSaveData(PowerUpType.PUT_1.ToString(), 3, false),
                new ItemSaveData(PowerUpType.PUT_2.ToString(), 3, false),
                new ItemSaveData(PowerUpType.PUT_3.ToString(), 3, false),
                new ItemSaveData(PowerUpType.PUT_4.ToString(), 3, false)
            };
        }

        public float getItemCount(string id) {
            ItemSaveData saveData = _items.Find(x => x.id == id);
            return saveData?.value ?? 0f;
        }

        public ItemSaveData getItemSaveData(string id) {
            ItemSaveData saveData = _items.Find(x => x.id == id);
            return saveData;
        }

        public void setItemCount(string id, float value) {
            ItemSaveData saveData = _items.Find(x => x.id == id);
            if(saveData != null) {
                saveData.value = value;
                Database.SaveGame();
            } else {
                Debug.Log("ItemSaveData Not Found for setItemCount");
            }
        }

        public void setItemShowed(string id) {
            ItemSaveData saveData = _items.Find(x => x.id == id);
            if(saveData != null) {
                saveData.isShowed = true;
                Database.SaveGame();
            } else {
                Debug.Log("ItemSaveData Not Found for setItemShowed");
            }
        }

        public void increaseItemCount(string id, float value)
        {
            ItemSaveData saveData = _items.Find(x => x.id == id);
            if (saveData != null)
            {
                saveData.value += value;
                Database.SaveGame();
            }
            else
            {
                Debug.Log("ItemSaveData Not Found for increaseItemCount");
            }
        }

        class DictionaryGenericComparer<TType> : IComparer<TType> where TType : IComparable {
            int IComparer<TType>.Compare(TType x, TType y) {
                return x.CompareTo(y);
            }
        }
        class DictionaryIntComparer : DictionaryGenericComparer<int> { }
        class DictionaryLongComparer : DictionaryGenericComparer<long> { }
        class DictionaryFloatComparer : DictionaryGenericComparer<float> { }
        class DictionaryDoubleComparer : DictionaryGenericComparer<double> { }
        class DictionaryStringComparer : DictionaryGenericComparer<string> { }
        class DictionaryBoolComparer : DictionaryGenericComparer<bool> { }
    }

    [Serializable]
    public sealed class ItemSaveData {
        public string id;
        public float value;

        public bool isShowed;

        public ItemSaveData(string id, float value, bool showed) {
            this.id = id;
            this.value = value;
            this.isShowed = showed;
        }
    }
}