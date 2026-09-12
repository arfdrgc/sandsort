using DG.Tweening;
using Moow;
using NaughtyAttributes;
using UnityEngine;

namespace Aden.UI
{
    public class CPIHand : MonoBehaviour
    {
        static readonly int Clicking = Animator.StringToHash("Clicking");
        [SerializeField] float scaleOnPress;
        [SerializeField] float scaleOnRelase;
        [SerializeField] float maxSpeed;
        Animator animator;

        void Awake() {
            animator = GetComponent<Animator>();
            transform.DOScale(scaleOnRelase, 0f);
        }
        
        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, Input.mousePosition, maxSpeed * Time.deltaTime);
            if (Input.GetMouseButtonDown(0))
            {
                transform.DOKill();
                transform.DOScale(scaleOnPress, 0.15f);
                animator?.SetBool(Clicking, true);
            }

            if (Input.GetMouseButtonUp(0))
            {
                transform.DOKill();
                transform.DOScale(scaleOnRelase, 0.15f);
                animator?.SetBool(Clicking, false);
            }
        }

        [Button("CPI Active")]
        private void CPIactive()
        {
            this.dispatchEvent<object>(Events.CPI_ACTIVE, null);
        }
    }
}

