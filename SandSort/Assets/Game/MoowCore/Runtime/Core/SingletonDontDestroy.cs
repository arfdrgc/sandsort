using UnityEngine;
using System.Threading;

namespace Moow {
	// This object wont be destroyed accross the scenes. Lifetime is same as App's
	public class SingletonDontDestroy<T> : RootSingleton where T : UnityEngine.MonoBehaviour {
		private static T _instance;

public static T instance
{
    get
    {
        if (_instance == null)
        {
            // 1. FindObjectOfType yerine FindFirstObjectByType kullanıyoruz (Daha hızlı)
            _instance = (T)FindFirstObjectByType(typeof(T));

            // 2. Birden fazla olup olmadığını kontrol ederken sıralama yapmadan (None) sayıyoruz
            var instances = FindObjectsByType(typeof(T), FindObjectsSortMode.None);
            
            if (instances.Length > 1)
            {
                Debug.LogError($"[Singleton] Sahnede birden fazla {typeof(T).Name} bulundu! " +
                               "Singleton yapısında sadece bir tane olmalıdır.");
            }

            if (_instance == null)
            {
                Debug.LogWarning($"[Singleton] Sahnede {typeof(T).Name} bulunamadı.");
            }
        }
        return _instance;
    }
}

		virtual protected void Awake() {

			string name = typeof(T).Name;
			if (!_instance)
			{
				_instance = GetComponent<T>();
				DontDestroyOnLoad(gameObject);
				Debug.Log($"[{name}::Awake] SingletonDontDestroy object initialized.");
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

		virtual protected void OnDestroy() { }
	}
}
