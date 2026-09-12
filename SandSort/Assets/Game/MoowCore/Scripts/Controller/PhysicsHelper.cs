using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsHelper {
    public static Vector3 raycastPlane(Plane plane) {
        return raycastPlane(plane, Input.mousePosition);
    }

    public static Vector3 raycastPlane(Plane plane, Vector3 mousePosition) {
        float d;
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

        plane.Raycast(ray, out d);
        return ray.GetPoint(d);
    }

    public static void raycast(int layerMask, Action<RaycastHit> onHit) {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        raycast(ray, layerMask, onHit);
    }

    public static void raycast(Ray ray, int layerMask, Action<RaycastHit> onHit) {
        RaycastHit hit;

        if(Physics.Raycast(ray, out hit, 1000, layerMask)) {
            onHit(hit);
        }
    }
}
