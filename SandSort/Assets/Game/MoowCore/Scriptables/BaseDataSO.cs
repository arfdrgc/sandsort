using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Moow {
    public class BaseDataSO : BaseScriptableObject {
        public int level;
        public int version;// Do not reset version ever
        public long firstLaunchTime;
        public long initialPlayTime;

        public bool disableAds;
        public bool disableHaptic;
        public bool sound;
        public bool music;

        public override void reset() {
            level = 0;
            firstLaunchTime = 0;
            initialPlayTime = 0;
            version = 0;
            disableAds = false;
            disableHaptic = false;
            sound = true;
            music = true;
            Debug.Log("[BaseDataSO::reset]");

#if UNITY_ANDROID
            // disableHaptic = true;
#endif
        }
    }
}
