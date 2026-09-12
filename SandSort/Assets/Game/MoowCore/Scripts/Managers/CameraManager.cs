using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Moow {

    public class CameraManager : BaseCameraManager<CameraType> {
    }

    public enum CameraType {
        // Main Game
        Base,
        Main,
        Game,
    }

    public enum BrainCameraType {
        Base,
        Game,
    }


}