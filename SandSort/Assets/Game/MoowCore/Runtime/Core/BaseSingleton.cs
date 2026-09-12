using System.Linq;
using System.Threading;
using UnityEngine;

namespace Moow
{
    // This object's lifetime depends on the related Scene. When the scene destroyed, this object is destroyed.
    public class BaseSingleton<T> : RootSingleton where T : BaseSingleton<T>
    {
        private static T _instance;

        public static T instance
        {
            get
            {
                if (!_instance)
                {
                    //var components = FindObjectsOfType(typeof(T));
                    var components =  Object.FindObjectsByType(typeof(T), FindObjectsSortMode.InstanceID);
                    if (components.Length == 1)
                    {
                        _instance = (T)components[0];
                    }
                    else if (components.Length > 1)
                    {
                        Debug.LogError($"[Singleton] Something went really wrong - there should never be more then 1 singletion! Reopening the scene might fix it.\nType: {typeof(T).FullName} - Thread: {Thread.CurrentThread.Name}");
                    }
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            string name = typeof(T).Name;
            if (!_instance)
            {
                _instance = (T) this;
                Debug.Log($"[{name}::Awake] BaseSingleton object initialized.");
            }
            else if (_instance != this)
            {
                // If has other components on the game gameobject, just destroy the singleton component itself.
                if (gameObject.GetComponents<Component>().Length > 1)
                {
                    Debug.Log($"[{name}::Awake] '{name}' already created! GameObject has other components, so just destroying newly created component.");
                    DestroyImmediate(this);
                }
                else
                {
                    Debug.Log($"[{name}::Awake] '{gameObject.name}' already created! Destroying newly created one");
                    DestroyImmediate(gameObject);
                }
            }
        }
    }
}