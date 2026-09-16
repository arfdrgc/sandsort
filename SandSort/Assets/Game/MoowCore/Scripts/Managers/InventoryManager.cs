using Moow;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;

namespace MoowCore {
    public class InventoryManager : BaseSingleton<InventoryManager> {
        [SerializeField] InventoryDataSO _inventorySO;
        [SerializeField] List<PowerUpSO> _powerUps;

        #region METHOD
        private void OnEnable() {
            //this.addListener<object>(Events.LEVEL_COMPLETED, onLevelComplete);
            this.addListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
            this.addListener<object>(Events.UI_REVIVE_CLICKED, onReviveUsed);
        }

        private void OnDisable() {
            //this.removeListener<object>(Events.LEVEL_COMPLETED, onLevelComplete);
            this.removeListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
            this.removeListener<object>(Events.UI_REVIVE_CLICKED, onReviveUsed);
        }

        // private void onLevelComplete(UnityEngine.Object sender, Event<object> eventData) {
        //     increase(GameDataManager.instance.levelCompleteReward);
        // }

        private void onPowerUpUsed(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
            float freeUse = getItemCount(eventData.data.itemID);
            if(freeUse > 0) {
                setItemCount(eventData.data.itemID, freeUse - 1);
                this.dispatchEvent<object>(Events.FREE_POWER_UP_CHANGED, null);
            } else {
                decrease(eventData.data.goldCost);
            }
        }

        private void onReviveUsed(UnityEngine.Object sender, Event<object> eventData) {
            // GameFailurePopup sends the price of this revive (GameDataSO.reviveCosts, by revive count).
            if (eventData.data is int cost) decrease(cost);
        }


        public void initialize() {
        }

        public bool RecorderStatus
        {
            get => _inventorySO.isRecorderActive;
            set => _inventorySO.UpdateRecorderStatus(value);
        }

        public bool QuickSkipStatus
        {
            get => _inventorySO.isQuickSkipActive;
            set => _inventorySO.UpdatQuickSkipStatus(value);
        }

        public bool hasEnoughMoney(float requiredMoney) {
            if(_inventorySO.money >= requiredMoney) {
                return true;
            }
            return false;
        }

        public void increase(float value) {
            _inventorySO.increase(value);
            this.dispatchEvent<int>(new Event<int>(Events.MONEY_CHANGED, (int)_inventorySO.money));
        }

        public void decrease(float value) {
            _inventorySO.decrease(value);
            this.dispatchEvent<int>(new Event<int>(Events.MONEY_CHANGED, (int)_inventorySO.money));
        }

        public float getItemCount(string id) {
            return _inventorySO.getItemCount(id);
        }

        public void setItemCount(string id, float value) {
            _inventorySO.setItemCount(id, value);
        }

        public void setItemShowed(string id) {
            _inventorySO.setItemShowed(id);
        }

        public ItemSaveData getItemSaveData(string id) {
            return _inventorySO.getItemSaveData(id);
        }

        public bool canUsePowerUp(PowerUpSO powerUp) {
            if(getItemCount(powerUp.itemID) > 0) {
                return true;
            }
            if(money >= powerUp.goldCost) {
                return true;
            }

            return false;
        }
        #endregion

        #region ACTION
        #endregion

        #region HELPER
        public float money => _inventorySO.money;
        #endregion
    }
}