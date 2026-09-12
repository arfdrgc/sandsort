using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Moow {
    public class MathExtension {
        public static Vector3 closestPointOnLine(Vector3 pos, Vector3 lineP1, Vector3 lineP2) {
            float y = (lineP1.y + lineP2.y) / 2;
            lineP1.y = y;
            lineP2.y = y;
            pos.y = y;

            return lineP1 + Vector3.Project(pos - lineP1, lineP2 - lineP1);
        }

        public static float distanceToLine(Vector3 pos, Vector3 lineP1, Vector3 lineP2) {
            Vector3 closestPos = closestPointOnLine(pos, lineP1, lineP2);
            return Vector3.Distance(pos, closestPos);
        }

        public static float distanceToLinePart(Vector3 point, Vector3 lineStart, Vector3 lineEnd) {
            return Vector3.Magnitude(projectPointLine(point, lineStart, lineEnd) - point);
        }

        public static Vector3 projectPointLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd) {
            Vector3 relativePoint = point - lineStart;
            Vector3 lineDirection = lineEnd - lineStart;
            float length = lineDirection.magnitude;
            Vector3 normalizedLineDirection = lineDirection;
            if(length > .000001f)
                normalizedLineDirection /= length;

            float dot = Vector3.Dot(normalizedLineDirection, relativePoint);
            dot = Mathf.Clamp(dot, 0.0F, length);

            return lineStart + normalizedLineDirection * dot;
        }

        public static Quaternion averageRotationOfObject<T>(List<T> objects) where T : MonoBehaviour {
            if(objects == null || objects.Count == 0) {
                Debug.LogWarning("No objects provided to calculate average rotation.");
                return Quaternion.identity;
            }

            Quaternion averageRotation = objects[0].transform.rotation;

            for(int i = 1; i < objects.Count; i++) {
                averageRotation = Quaternion.Slerp(averageRotation, objects[i].transform.rotation, 1.0f / (i + 1));
            }

            return averageRotation;
        }

        public static Vector3 averagePositionOfObject<T>(List<T> objects) where T : MonoBehaviour {
            if(objects == null || objects.Count == 0) {
                Debug.LogWarning("No objects provided to calculate center position.");
                return Vector3.zero;
            }

            Vector3 centerPosition = Vector3.zero;

            foreach(T obj in objects) {
                centerPosition += obj.transform.position;
            }

            centerPosition /= objects.Count;
            return centerPosition;
        }
    }
}