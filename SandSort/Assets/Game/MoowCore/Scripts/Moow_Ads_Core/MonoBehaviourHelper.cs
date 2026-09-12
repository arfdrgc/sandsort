using System;
using System.Collections;
using UnityEngine;

public class MonoBehaviourHelper : MonoBehaviour
{
    public void RunDelayed(double seconds, Action action)
    {
        StartCoroutine(Run(seconds, action));
    }

    private IEnumerator Run(double seconds, Action action)
    {
        yield return new WaitForSeconds((float)seconds);
        action?.Invoke();
        Destroy(gameObject);
    }
}