using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Cinemachine;

namespace Moow
{
    abstract public class BaseCameraManager<TCameraType> : BaseSingleton<BaseCameraManager<TCameraType>>
    {
        private CinemachineBrain _cameraBrain;
        private Camera[] _brains;
        private BaseMoowVirtualCamera<TCameraType>[] _virtualCameras;
        private Dictionary<TCameraType, BaseMoowVirtualCamera<TCameraType>> _cachedDict;
        private BaseMoowVirtualCamera<TCameraType> _activeCamera;

        public void initalize()
        {

            _brains = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            _cameraBrain = FindFirstObjectByType<CinemachineBrain>(
                FindObjectsInactive.Include);

            _cachedDict =
                new Dictionary<TCameraType, BaseMoowVirtualCamera<TCameraType>>();

            _virtualCameras =
                FindObjectsByType<BaseMoowVirtualCamera<TCameraType>>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < _virtualCameras.Length; i++)
            {
                _cachedDict[_virtualCameras[i].type] = _virtualCameras[i];
            }

            Array.Sort(_virtualCameras, (x, y) => y.getCameraPriority.CompareTo(x.getCameraPriority));

            deactivateVirtualCameras();
            _activeCamera = _virtualCameras[0];
            _activeCamera.activate();

            Debug.Log($"[BaseCameraManager::initialize()]");
        }

        #region METHODS
        public void switchCamera(TCameraType type)
        {

            if (_cachedDict.ContainsKey(type) == false)
                throw new System.Exception($"[BaseCameraManager::switchCamera] Type: '{type}' could not be found in cached dictionary.");

            var targetVirtualCamera = _cachedDict[type];
            if (_activeCamera != targetVirtualCamera)
            {
                _activeCamera.deactivate();
                targetVirtualCamera.activate();
                _activeCamera = targetVirtualCamera;
            }
        }

        void activateVirtualCameras()
        {
            for (int i = 0; i < _virtualCameras.Length; i++)
            {
                _virtualCameras[i].activate();
            }
        }

        void deactivateVirtualCameras()
        {
            for (int i = 0; i < _virtualCameras.Length; i++)
            {
                _virtualCameras[i].deactivate();
            }
        }

        #endregion
    }
}