using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = System.Random;
namespace Moow {
    /// <summary>
    ///     Method extension in C# is a feature that gives an opportunity to extend built-in
    ///     class by defining/adding new methods but wont allow overriding built-in methods.
    ///     without needs of inheritance and using them like on its own.
    /// </summary>
    public static class MoowExtensions {

		const int Layer_UI = 5;
		static readonly Random rng = new Random();
		// Component Extensions
		public static T GetOrAddComponent<T>(this Component child) where T : Component {
			T result = child.GetComponent<T>();
			if (result == null) {
				result = child.gameObject.AddComponent<T>();
			}

			return result;
		}

		// Null coalescing operator is used for null check.
		// ?. null check is not working as expected for objects that inherited UnityEngine.object class.
		// use Unity custom implemented `==` null-check operator.
		public static void addListener<TEventData>(this Component child, string eventName, EventHandler<TEventData> handler) {
			Dispatcher.instance?.add(eventName, child.gameObject, handler);
		}

		public static void removeListener<TEventData>(this Component child, string eventName, EventHandler<TEventData> handler) {
			Dispatcher.instance?.remove(eventName, child.gameObject, handler);
		}

		public static void dispatchEvent<TEventData>(this Component child, Event<TEventData> e, bool localPropagation = false) {
			if (child != null) {
				Dispatcher.instance?.dispatch(child.gameObject, e, localPropagation);
			}
			else {
				Dispatcher.instance?.nullcheckOperation(e.eventName);
			}
		}

		public static void dispatchEvent<TEventData>(this Component child, string eventName, TEventData e, bool localPropagation = false) {
			dispatchEvent(child, new Event<TEventData>(eventName, e), localPropagation);
		}

		public static void propagateEvent<TEventData>(this Component child, Event<TEventData> e) {
			if (child != null) {
				Dispatcher.instance?.propagate(child.gameObject, e);
			}
			else {
				Dispatcher.instance?.nullcheckOperation(e.eventName);
			}
		}

		// Texture2D Extensions
		public static Sprite convertToSprite(this Texture2D texture) {
			Rect rect = new Rect(0, 0, texture.width, texture.height);
			return Sprite.Create(texture, rect, new Vector2(0, 0), 100f);
		}

		// Image Extensions
		public static void setAlpha(this Image image, float alpha) {
			Color color = image.color;
			color.a = alpha;
			image.color = color;
		}

		public static void setAlpha(this Text text, float alpha) {
			Color color = text.color;
			color.a = alpha;
			text.color = color;
		}


		// RectTransform Extensions
		public static void setScale(this RectTransform rectTransform, float scale) {
			rectTransform.localScale = Vector3.one * scale;
		}

		public static float setX(this RectTransform rectTransform, float x) {
			Vector3 pos = rectTransform.position;
			float prevVal = pos.x;
			pos.x = x;
			rectTransform.position = pos;
			return prevVal;
		}

		public static float setY(this RectTransform rectTransform, float y) {
			Vector3 pos = rectTransform.position;
			float prevVal = pos.y;
			pos.y = y;
			rectTransform.position = pos;
			return prevVal;

		}

		public static float setZ(this RectTransform rectTransform, float z) {
			Vector3 pos = rectTransform.position;
			float prevVal = pos.z;
			pos.z = z;
			rectTransform.position = pos;
			return prevVal;
		}

		// Vectors
		public static float random(this Vector2 vector) {
			return UnityEngine.Random.Range(vector.x, vector.y);
		}

		// List
		public static void AddAt<T>(this List<T> list, int index, T entity) {


		}
		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while (n > 1) {
				n--;
				int k = rng.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
		public static Vector2 RotateVector(this Vector2 vector, float theta) {
			float radians = Mathf.Deg2Rad * theta;

			float cosTheta = Mathf.Cos(theta);
			float sinTheta = Mathf.Sin(theta);

			float x = vector.x * cosTheta - vector.y * sinTheta;
			float y = vector.x * sinTheta + vector.y * cosTheta;

			return new Vector2(x, y);
		}
		public static Tweener DOText(this TMP_Text text, int from, int to, float duration) {
			Tweener tween = null;
			if (duration <= 0) {
				text.text = to.ToString();
			}
			else {
				tween = DOVirtual.Float(0, 1, duration,
					t =>
					{
						int value = Mathf.CeilToInt((to - from) * t + from);
						text.text = "" + value;
					});
			}
			return tween;
		}

		public static Color RandomColor(int value) {
			// Set seed for consistency
			UnityEngine.Random.InitState(value);

			// Generate random RGB values
			float red = UnityEngine.Random.value;
			float green = UnityEngine.Random.value;
			float blue = UnityEngine.Random.value;

			// Create a new Color object with the random RGB values
			Color randomColor = new Color(red, green, blue);

			return randomColor;
		}
		public static Color ToGrayscale(this Color color) {
			// Calculate the luminance using the standard weights for RGB
			float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
			// Return the grayscale color with the original alpha
			return new Color(gray, gray, gray, color.a);
		}

		public static Color HSVtoRGB(this Vector3 HSV) {
			return Color.HSVToRGB(HSV.x, HSV.y, HSV.z);
		}

		public static Vector3 RGBtoHSV(this Color color) {
			Vector3 hsv = Vector3.zero;
			float h;
			float s;
			float v;
			Color.RGBToHSV(color, out h, out s, out v);
			hsv.x = h;
			hsv.y = s;
			hsv.z = v;
			return hsv;
		}

		public static Tweener DOTextCount(this TMP_Text text, float from, float to, int total, float duration) {
			Tweener tween = null;
			if (duration <= 0) {
				text.text = to.ToString();
			}
			else {
				tween = DOVirtual.Float(0, 1, duration,
					t =>
					{
						int value = Mathf.CeilToInt((to - from) * t + from);
						text.text = $"{value} / {total}";
					});
			}
			return tween;
		}

		public static string lastComponent(this string str) {
			string[] words = str.Split(".");
			str = words[words.Length - 1];
			return str;
		}

		// Component Extensions
		public static Tween nextFrame(this Component child, TweenCallback closure) {
			return DOVirtual.DelayedCall(0, closure);
		}

		public static Tween delayCall(this Component child, float delay, TweenCallback closure) {
			return DOVirtual.DelayedCall(delay, closure);
		}

		//Returns 'true' if we touched or hovering on Unity UI element.
		public static bool IsPointerOverUIElement() {
			return IsPointerOverUIElement(GetEventSystemRaycastResults());

		}
		static bool IsPointerOverUIElement(List<RaycastResult> eventSystemRaysastResults) {
			for (int index = 0; index < eventSystemRaysastResults.Count; index++) {
				RaycastResult curRaysastResult = eventSystemRaysastResults[index];
				if (curRaysastResult.gameObject.layer == Layer_UI)
					return true;
			}
			return false;
		}

		//Gets all event system raycast results of current mouse or touch position.
		static List<RaycastResult> GetEventSystemRaycastResults() {
			PointerEventData eventData = new PointerEventData(EventSystem.current);
			eventData.position = Input.mousePosition;
			List<RaycastResult> raysastResults = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventData, raysastResults);
			return raysastResults;
		}

		public static Vector2 AddX(this Vector2 v, float f) {
			return new Vector2(v.x + f, v.y + 0);
		}
		public static Vector2 AddY(this Vector2 v, float f) {
			return new Vector2(v.x + 0, v.y + f);
		}
		public static Vector2Int SetX(this Vector2Int v, int f) { return new Vector2Int(f, v.y); }
		public static Vector2Int SetY(this Vector2Int v, int f) { return new Vector2Int(v.x, f); }

		public static Vector3 AddX(this Vector3 v, float f) {
			return new Vector3(v.x + f, v.y + 0, v.z + 0);
		}
		public static Vector3 AddY(this Vector3 v, float f) {
			return new Vector3(v.x + 0, v.y + f, v.z + 0);
		}
		public static Vector3 AddZ(this Vector3 v, float f) {
			return new Vector3(v.x + 0, v.y + 0, v.z + f);
		}

		public static Vector3 MulX(this Vector3 v, float f) {
			return new Vector3(v.x * f, v.y + 0, v.z + 0);
		}
		public static Vector3 MulY(this Vector3 v, float f) {
			return new Vector3(v.x + 0, v.y * f, v.z + 0);
		}
		public static Vector3 MulZ(this Vector3 v, float f) {
			return new Vector3(v.x + 0, v.y + 0, v.z * f);
		}

		public static Vector3 SetX(this Vector3 v, float f) {
			return new Vector3(f, v.y, v.z);
		}
		public static Vector3 SetY(this Vector3 v, float f) {
			return new Vector3(v.x, f, v.z);
		}
		public static Vector3 SetZ(this Vector3 v, float f) {
			return new Vector3(v.x, v.y, f);
		}

		public static Vector2 xx(this Vector3 v) { return new Vector2(v.x, v.x); }
		public static Vector2 xy(this Vector3 v) { return new Vector2(v.x, v.y); }
		public static Vector2 xz(this Vector3 v) { return new Vector2(v.x, v.z); }
		public static Vector2 yx(this Vector3 v) { return new Vector2(v.y, v.x); }
		public static Vector2 yy(this Vector3 v) { return new Vector2(v.y, v.y); }
		public static Vector2 yz(this Vector3 v) { return new Vector2(v.y, v.z); }
		public static Vector2 zx(this Vector3 v) { return new Vector2(v.z, v.x); }
		public static Vector2 zy(this Vector3 v) { return new Vector2(v.z, v.y); }
		public static Vector2 zz(this Vector3 v) { return new Vector2(v.z, v.z); }

		public static Color SetR(this Color c, float r) {
			return new Color(r, c.g, c.b, c.a);
		}
		public static Color SetG(this Color c, float g) {
			return new Color(c.r, g, c.b, c.a);
		}
		public static Color SetB(this Color c, float b) {
			return new Color(c.r, c.g, b, c.a);
		}
		public static Color SetA(this Color c, float a) {
			return new Color(c.r, c.g, c.b, a);
		}

		public static Rect SetSize(this Rect rect, float width, float height) {
			return new Rect(rect.center.x - width / 2f, rect.center.y - height / 2f, width, height);
		}


		public static void DestroyAllChildren(this Transform parent) {
			foreach (Transform child in parent) {
				Object.Destroy(child.gameObject);
			}
		}

		public static void DestroyAllChildrenImmediate(this Transform parent) {
			foreach (Transform child in parent) {
				Object.DestroyImmediate(child.gameObject);
			}
		}

		public static Vector3 GetCenterPosition<T>(this List<T> objects) where T : MonoBehaviour {
			if (objects == null || objects.Count == 0)
				return Vector3.zero; // Return zero if the list is empty

			Vector3 sum = Vector3.zero;
			foreach (T obj in objects) {
				sum += obj.transform.position;
			}
			return sum / objects.Count; // Get the average position
		}
		
		public static T GetClosestObject<T>(this List<T> objects, Vector3 position) where T : MonoBehaviour
		{
			T closestObject = null;
			float minDistanceSqr = float.MaxValue;

			foreach (T obj in objects)
			{
				if (obj == null) continue;

				float distSqr = (obj.transform.position - position).sqrMagnitude;
				if (distSqr < minDistanceSqr)
				{
					minDistanceSqr = distSqr;
					closestObject = obj;
				}
			}

			return closestObject;
		}

		public static Vector3 GetClosestPosition<T>(this List<T> objects, Vector3 position) where T : MonoBehaviour
		{
			T closest = objects.GetClosestObject(position);
			return closest != null ? closest.transform.position : Vector3.zero;
		}
		
		public static TextMeshProUGUI FaceCamera(this TextMeshProUGUI t)
		{
			var cam = Camera.main;
			if (cam == null) return t;

			// Face the camera directly (billboard style)
			Vector3 camForward = cam.transform.forward;
			Vector3 camUp = cam.transform.up;

			// If parent has a negative scale, we correct it by using inverse rotation
			Vector3 scale = t.transform.lossyScale;
			if (scale.x * scale.y * scale.z < 0f)
			{
				camForward = -camForward;
			}

			t.transform.rotation = Quaternion.LookRotation(camForward, camUp);
			return t;
		}

#if UNITY_EDITOR
		public static T GetFirstAssetFromPath<T>(string fullPath, string filter = null) where T : Object {
			string[] GUIDs = AssetDatabase.FindAssets(filter, new[] { fullPath });
			string filePath = AssetDatabase.GUIDToAssetPath(GUIDs[0]);
			T asset = AssetDatabase.LoadAssetAtPath<T>(filePath);
			return asset;
		}

		public static string GetFirstAssetNameFromPath(string fullPath, string filter = null) {
			string[] GUIDs = AssetDatabase.FindAssets(filter, new[] { fullPath });
			string filePath = AssetDatabase.GUIDToAssetPath(GUIDs[0]);
			string fileName = Path.GetFileNameWithoutExtension(filePath);
			return fileName;
		}
#endif
	}
}
