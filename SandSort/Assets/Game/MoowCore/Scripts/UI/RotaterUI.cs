using System.Collections.Generic;
using UnityEngine;
using Moow;
using UnityEngine.UI;
using System;

namespace MoowCore
{
    public class RotaterUI : MonoBehaviour
    {
        [SerializeField] float _rotateSpeed = 90;
        [SerializeField] RectTransform _rectTransform;

        private void Awake()
        {
            if(_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

        }

        private void Update()
        {
            _rectTransform.Rotate(Vector3.forward, _rotateSpeed * Time.deltaTime);
        }
    }
}