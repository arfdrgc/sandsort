using System.Collections.Generic;
using System;
using UnityEngine;

namespace Moow {
    /*
		!!! UnityEvent class

		UnityEvent is a abstract class which wraps and manages the event/delegate data and
		gives interface to AddListener/RemoveListener/Invoke methods to handle
		callback mechanism on its own way. In my opinion, wrapping event delegate
		variables is both unjustifiable and costly; there is not any benefits from
		using it.

        !!! Unity overloaded null-check operator `==`

        Unity provides a custom implementation of the == operator (and naturally for != as well) for types that inherit from the UnityEngine.
        Object class (e.g. MonoBehaviour and ScriptableObject). For other types – like a custom class that doesn’t inherit from any other class –
        C#’s standard implementation will be used. When comparing a UnityEngine.Object against null, the engine not only checks if the operand it null by itself,
        but it also checks if its underlying entity was destroyed.
	*/

    // UnityEngine.Object must be used as base class instead obe System.object
    // for Unity's custom == control operator to be worked.
    public delegate void EventHandler<TEventData>(UnityEngine.Object sender, Event<TEventData> eventData);

    public sealed class Dispatcher : SingletonDontDestroy<Dispatcher> {
        private Dictionary<string, Dictionary<UnityEngine.Object, MulticastDelegate>> _listeners = new Dictionary<string, Dictionary<UnityEngine.Object, MulticastDelegate>>();
        private static readonly UnityEngine.Object _GlobalListener = new UnityEngine.Object();

        #region METHODS
        public void add<TEventData>(string eventName, EventHandler<TEventData> handler) {
            add(eventName, _GlobalListener, handler);
        }

        public void add<TEventData>(string eventName, UnityEngine.Object listener, EventHandler<TEventData> handler) {

            if (_listeners.ContainsKey(eventName)) {
                Dictionary<UnityEngine.Object, MulticastDelegate> innerDict = _listeners[eventName];
                if (innerDict.ContainsKey(listener)) {
                    if (innerDict[listener] is EventHandler<TEventData> multicastHandler) { // Pattern Matching syntax.
                        multicastHandler += handler;
                        innerDict[listener] = multicastHandler;
                    } else {
                        Debug.Log($"[Dispatcher::add<TEventData>] Delegate signatures is not matched with method signature! {innerDict[listener]}");
                    }
                } else {
                    innerDict[listener] = handler;
                }
            } else {
                Dictionary<UnityEngine.Object, MulticastDelegate> innerDict = new Dictionary<UnityEngine.Object, MulticastDelegate>();
                innerDict[listener] = handler;
                _listeners[eventName] = innerDict;
            }
        }

        public void remove<TEventData>(string eventName, EventHandler<TEventData> handler) {
            remove(eventName, _GlobalListener, handler);
        }

        public void remove<TEventData>(string eventName, UnityEngine.Object listener, EventHandler<TEventData> handler) {

            if (_listeners.ContainsKey(eventName)) {
                Dictionary<UnityEngine.Object, MulticastDelegate> innerDict = _listeners[eventName];
                if (innerDict.ContainsKey(listener)) {
                    if (innerDict[listener] is EventHandler<TEventData> multicastHandler) { // Pattern Matching syntax.
                        if (multicastHandler == null) {
                            throw new Exception("[Dispatcher::remove<TEventData>] multicastHandler is null, cannot operate subtutive operation.");
                        }

                        multicastHandler -= handler;
                        if (multicastHandler == null) {
                            innerDict.Remove(listener);
                        } else {
                            innerDict[listener] = multicastHandler;
                        }
                    } else {
                        Debug.Log($"[Dispatcher::remove<TEventData>] Delegate signatures is not matched with method signature! {innerDict[listener]}");
                    }
                }

                if (_listeners[eventName].Count <= 0) {
                    _listeners.Remove(eventName);
                }
            }
        }

        // Local propogation true means send event only to components whose are part of the event dispatcher entity.
        public void dispatch<TEventData>(UnityEngine.Object sender, Event<TEventData> eventData, bool localPropagation = false) {

            if (_listeners.ContainsKey(eventData.eventName)) {

                Dictionary<UnityEngine.Object, MulticastDelegate> innerDict = _listeners[eventData.eventName];

                // Local Propagation
                if (localPropagation == false) {
                    List<UnityEngine.Object> listeners = new List<UnityEngine.Object>(innerDict.Keys);
                    foreach (UnityEngine.Object listener in listeners) {
                        if (innerDict.ContainsKey(listener)) {
                            if (innerDict[listener] is EventHandler<TEventData> multicastDelegate) {
                                multicastDelegate(sender, eventData);
                            } else {
                                Debug.Log($"[Dispatcher::dispatch] Delegate signatures is not matched with method signature! Listener:{listener} Signature:{innerDict[listener]}");
                            }
                        } else {
                            Debug.LogWarning($"[Dispatcher::dispatch] Dictionary does not contain {listener}. That means Dictionary is modified in runtime!");
                        }
                    }
                } else {
                    if (innerDict.ContainsKey(sender)) {
                        if (innerDict[sender] is EventHandler<TEventData> multicastDelegate) {
                            multicastDelegate(sender, eventData);
                        } else {
                            Debug.Log($"[Dispatcher::dispatch] Delegate signatures is not matched with method signature! Signature: {innerDict[sender]}");
                        }
                    }
                }
            }
        }

        public void propagate<TEventData>(UnityEngine.Object sender, Event<TEventData> eventData) {

            if (sender is GameObject parentGO) {
                Transform parent = parentGO.transform;

                if (_listeners.ContainsKey(eventData.eventName)) {
                    Dictionary<UnityEngine.Object, MulticastDelegate> innerDict = _listeners[eventData.eventName];

                    if (innerDict.ContainsKey(parentGO)) {

                        if (innerDict[parentGO] is EventHandler<TEventData> multicastDelegate) {
                            multicastDelegate(parentGO, eventData);

                            if (parent.parent != null) {
                                parent = parent.parent;
                                if (!eventData.stopPropagate)
                                    propagateRecursively(sender, innerDict, parent.gameObject, eventData);
                            }
                        } else {
                            Debug.Log($"[Dispatcher::propagate] Delegate signatures is not matched with method signature! {innerDict[parent.gameObject]}");
                        }
                    } else {

                        parent = parent.parent;
                        propagateRecursively(sender, innerDict, parent.gameObject, eventData);
                    }
                }
            }
        }

        void propagateRecursively<TEventData>(UnityEngine.Object sender, Dictionary<UnityEngine.Object, MulticastDelegate> innerDict, GameObject listener, Event<TEventData> eventData) {

            Transform parent = null;
            if (innerDict.ContainsKey(listener)) {
                if (innerDict[listener] is EventHandler<TEventData> multicastDelegate) {
                    multicastDelegate(sender, eventData);
                    parent = listener.transform.parent;
                    if (!eventData.stopPropagate && parent != null) {
                        propagateRecursively(sender, innerDict, parent.gameObject, eventData);
                    }
                }
            }

            parent = listener.transform.parent;
            if (parent != null) {
                propagateRecursively(sender, innerDict, parent.gameObject, eventData);
            }
        }

        // Unity custom implementation of the == operator
        public void nullcheckOperation(string eventName) {

            if (_listeners.ContainsKey(eventName)) {

                var relatedEventDict = _listeners[eventName];
                var nullCleanedDict = new Dictionary<UnityEngine.Object, MulticastDelegate>(relatedEventDict);
                foreach (var element in relatedEventDict)
                    if (element.Key == null) {
#if UNITY_EDITOR
                        Debug.Log($"[Dispatcher::nullcheckOperation] Event Listener for: `{eventName}` has already been destroyed!\n" +
                                    $"*** Class: `{element.Value.Method.ReflectedType}`\n" +
                                    $"*** Method: `{element.Value.Method.GetBaseDefinition()}`" +
                                    ". Removed from listener dictionary!");
#endif
                        nullCleanedDict.Remove(element.Key);
                    }

                if (nullCleanedDict.Count > 0)
                    _listeners[eventName] = nullCleanedDict;
                else
                    _listeners.Remove(eventName);
            }
        }
        #endregion
    }
}