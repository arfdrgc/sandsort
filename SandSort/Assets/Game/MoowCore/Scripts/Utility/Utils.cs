using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Moow.Utility {

    static public class Helper {

        static public void makeSquarePngFromOurVirtualThingy(Camera camera) {
            // capture the virtuCam and save it as a square PNG.
            int sqr = 512;

            camera.aspect = 1.0f;
            // recall that the height is now the "actual" size from now on
            // the .aspect property is very tricky in Unity, and bizarrely is NOT shown in the editor
            // the editor will still incorrectly show the frustrum being screen-shaped

            RenderTexture tempRT = new RenderTexture(sqr, sqr, 24);
            // the "24" can be 0,16,24 or formats like RenderTextureFormat.Default, ARGB32 etc.

            camera.targetTexture = tempRT;
            camera.Render();

            RenderTexture.active = tempRT;
            Texture2D virtualPhoto = new Texture2D(sqr, sqr, TextureFormat.RGB24, false);
            // false, meaning no need for mipmaps
            virtualPhoto.ReadPixels(new Rect(0, 0, sqr, sqr), 0, 0); // you get the center section

            RenderTexture.active = null; // "just in case"
            camera.targetTexture = null;
            //////Destroy(tempRT); - tricky on android and other platforms, take care

            byte[] bytes;
            bytes = virtualPhoto.EncodeToPNG();

            System.IO.File.WriteAllBytes(OurTempSquareImageLocation(), bytes);
            // virtualCam.SetActive(false); ... not necesssary but take care

            // now use the image somehow...
            //YourOngoingRoutine(OurTempSquareImageLocation());
        }
        static string OurTempSquareImageLocation() {
            string r = Application.dataPath + "/p.png";
            return r;
        }

        public static Texture2D getScreenshot(Camera camera) {
            RenderTexture activeRenderTexture = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            Texture2D texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = activeRenderTexture;

            return texture;
        }

        public static Texture2D textureFromSprite(Sprite sprite) {
            if (sprite.rect.width != sprite.texture.width) {
                Texture2D newText = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
                Color[] newColors = sprite.texture.GetPixels((int)sprite.textureRect.x,
                                                             (int)sprite.textureRect.y,
                                                             (int)sprite.textureRect.width,
                                                             (int)sprite.textureRect.height);
                newText.SetPixels(newColors);
                newText.Apply();
                return newText;
            } else
                return sprite.texture;
        }

        static Dictionary<long, string> abbrevations = new Dictionary<long, string> { { 1000000000, "B" }, { 1000000, "M" }, { 1000, "K" } };
        public static string AbbreviateNumber(float number, bool showCurrency = true) {
            static string RoundTheNumber(float value) => value % 1 == 0 ? value.ToString() : value.ToString("n1");
            string currency = showCurrency ? "$" : "";

            foreach (KeyValuePair<long, string> pair in abbrevations)
            {
                if (Mathf.Abs(number) >= pair.Key)
                {
                    float roundedNumber = number / (float)pair.Key;

                    float aa = (float)System.Math.Round(roundedNumber, 2);
                    // return currency + RoundTheNumber(aa) + pair.Value;
                    return currency + aa.ToString("0.##") + pair.Value;
                }
            }

            return currency + RoundTheNumber(number);
        }

        public static class ReflectionHelper {
            public static object GetAttributeValue<T>(object entity, string propertyName) where T : Attribute
            {
                var attribute = (T)entity.GetType().GetCustomAttributes(typeof(T), true)[0];
                return attribute != null ? attribute.GetType().GetProperty(propertyName).GetValue(attribute) : null;
            }
        }

        public static long timestamp()
        {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }

        public static int getMinutesFrom(long timestamp)
        {
            DateTime before = UnixTimeStampToDateTime(timestamp);
            DateTime newDate = DateTime.Now;

            TimeSpan difference = newDate.Subtract(before);

            return difference.Minutes;
        }

        public static int getSecondsFrom(long timestamp)
        {
            DateTime before = UnixTimeStampToDateTime(timestamp);
            DateTime newDate = DateTime.Now;

            TimeSpan difference = newDate.Subtract(before);

            return difference.Seconds;
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static class MemoryAddress {
            public static string Get(object a) {
                System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(a, System.Runtime.InteropServices.GCHandleType.Pinned);
                IntPtr pointer = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle);
                handle.Free();
                return "0x" + pointer.ToString("X");
            }
        }

        public static Color hexToColor(string hex) {

            Regex reg = new Regex("^#[A-Fa-f0-9]{6}");
            if (reg.IsMatch(hex)) {
                int red = Convert.ToInt32(hex.Substring(1, 2), 16);
                int green = Convert.ToInt32(hex.Substring(3, 2), 16);
                int blue = Convert.ToInt32(hex.Substring(5, 2), 16);

                return new Color(red / (float)255.0f, green / (float)255.0f, blue / (float)255.0f);
            } else {

                return Color.black;
            }
        }

        public static string colorToHex(Color color) {

            string red = ((int)(color.r * 255)).ToString("X");
            red = fixOnlyOnceCharacter(red);


            string green = ((int)(color.g * 255)).ToString("X");
            green = fixOnlyOnceCharacter(green);

            string blue = ((int)(color.b * 255)).ToString("X");
            blue = fixOnlyOnceCharacter(blue);

            string regexExpression = "#" + red + green + blue;

            Regex reg = new Regex("^#[A-Fa-f0-9]{6}");
            if (reg.IsMatch(regexExpression)) {
                return regexExpression;
            } else {
                return "#000000";
            }

            string fixOnlyOnceCharacter(string val) {
                if (val.Length < 2) {
                    val += val;
                }
                return val;
            }
        }

		public static Vector3 scaleModifiedWithY(float newYScale, Vector3 originalScale) {
			float originalVolume = originalScale.x * originalScale.y * originalScale.z;

			float newXScale = originalVolume / (newYScale * originalScale.z);
			float newZScale = originalVolume / (newYScale * originalScale.x);

			return new Vector3(newXScale, newYScale, newZScale);
		}
		public static Vector3 scaleModifiedWithX(float newXScale, Vector3 originalScale) {
			float originalVolume = originalScale.x * originalScale.y * originalScale.z;

			float newYScale = originalVolume / (newXScale * originalScale.z);
			float newZScale = originalVolume / (newXScale * originalScale.y);

			return new Vector3(newXScale, newYScale, newZScale);
		}
		public static Vector3 scaleModifiedWithZ(float newZScale, Vector3 originalScale) {
			float originalVolume = originalScale.x * originalScale.y * originalScale.z;

			float newXScale = originalVolume / (newZScale * originalScale.y);
			float newYScale = originalVolume / (newZScale * originalScale.x);

			return new Vector3(newXScale, newYScale, newZScale);
		}
		public static Vector2 scale2DModifiedWithY(float newYScale, Vector2 originalScale) {

            float originalVolume = originalScale.x * originalScale.y;
			float newXScale = originalVolume / newYScale;

			return new Vector2(newXScale, newYScale);
		}
		public static Vector2 scale2DModifiedWithX(float newXScale, Vector2 originalScale) {

            float originalVolume = originalScale.x * originalScale.y;
			float newYScale = originalVolume / newXScale;

			return new Vector2(newXScale, newYScale);
		}
	}

    public struct ModifiedScreen {
        public static int width;
        public static int height;
        public static float topHeight;
        public static float bottomHeight;
    }

}

