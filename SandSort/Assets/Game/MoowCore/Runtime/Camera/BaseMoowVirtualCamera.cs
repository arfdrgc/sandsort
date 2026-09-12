
using Unity.Cinemachine;
using UnityEngine;

namespace Moow
{
    public class BaseMoowVirtualCamera<TCameraType> : MonoBehaviour
    {
        [SerializeField] protected TCameraType _type;
        [SerializeField] protected CinemachineCamera _virtualCamera;

        public TCameraType type => _type;
        public CinemachineCamera virtualCamera => _virtualCamera;

        private bool _isActive;

        public int getCameraPriority => _virtualCamera.Priority;

        public void activate() {

            _isActive = true;
            _virtualCamera.gameObject.SetActive(_isActive);
        }

        public void deactivate() {

            _isActive = false;
            _virtualCamera.gameObject.SetActive(_isActive);
        }
    }
}