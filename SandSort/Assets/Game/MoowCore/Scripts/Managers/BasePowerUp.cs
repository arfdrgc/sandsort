using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BasePowerUp : MonoBehaviour {
    [SerializeField] PowerUpSO _powerUpSO;
    public PowerUpSO powerUpSO => _powerUpSO;

    public abstract void useIfAvailable();

    public void failedToUse() {
        this.dispatchEvent(Events.POWER_UP_FAILED_TO_USE, _powerUpSO);
    }

    public void used() {
        this.dispatchEvent(Events.POWER_UP_USED, _powerUpSO);
    }
}
