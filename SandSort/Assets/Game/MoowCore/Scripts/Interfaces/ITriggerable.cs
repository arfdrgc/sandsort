using UnityEngine;

public interface ITriggerable
{
    void OnTriggeredEnter(Collider other);
    void OnTriggeredExit(Collider other);
}