using Moow;
using Moow.Audio;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioPlayer : BaseAudioPlayer<AudioFX> {
    #region CONSTANTS
    #endregion

    #region BASE
    void OnEnable() {
    }

    void OnDisable() {
    }

    #endregion

    #region OVERRIDED
    #endregion

    #region METHODS
    #endregion

    #region INTERFACE
    #endregion

    #region ACTIONS
    #endregion

    #region COROUTINE
    #endregion

    #region HELPER
    #endregion
}

public enum AudioFX {
    NO_SOUND = 0,

    LEVEL_COMPLETE = 1001,
    LEVEL_FAILED= 1002,
    LEVEL_DIFFICULTY = 1003,

    COUNTER = 1101,
    UI_BUTTON_CLICK = 1102,
    VIDEO_TUTORIAL_SHOWN = 1103,
    MECHANIC_UNLOCK_SHOWN = 1104,
    MECHANIC_POSITIVE = 1105,
    LOCK_CLICK = 1106,
    OBJECTIVE_COMPLETE = 1107,
    WHOOSH_SHORT_1 = 1108,
    LEVEL_CONFETTI = 1109,
    ICE_CRACK= 1110,
    ITEM_WHOOSH = 1111,
    ITEM_SELECTED = 1112,
    ITEM_COMPLETE_1 = 1113,
    ITEM_COMPLETE_2 = 1114,

    CARTOON_DRIP_1 = 2001,
    WATER_DROP_1 = 2002,
    WHOOSH_SHORT_2 = 2003,
    PLATE_SELECTED = 2004,
    WATER_STREAM_1 = 2005,
    MAGNET = 2006,
    POSITIVE_1 = 2007,
    POSITIVE_2= 2008,
    BUBBLE_HIT = 2009,
    COIN_COLLECT = 2010,
    ITEM_LOADED = 2011


}