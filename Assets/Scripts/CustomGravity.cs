using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomGravity : MonoBehaviour
{
    /// Output Paramater Usage:
    // It works like Physics.Raycast, which returns whether something was hit and puts the relevant data in a RaycastHit struct provided as an output argument.
    // The 'out' keyword tells us that the method is responsible for correctly setting the parameter, replacing its previous value.Not assigning a value to it will produce a compiler error.
    // The rationale in this case is that returning the gravity vector is the primary purpose of GetGravity, but you can also get the associated up axis at the same time via the output parameter.
    /// ------------------ ///


    // Returns customized gravity vector and current upAxis(using output parameter)
    public static Vector3 GetGravity (Vector3 position, out Vector3 upAxis)
    {
        Vector3 up = position.normalized;
        upAxis = Physics.gravity.y < 0f ? up : -up; // Away or Towards Ground
        return up * Physics.gravity.y;
    }

    // Returns just the upward axis of a position
    public static Vector3 GetUpAxis (Vector3 position)
    {
        Vector3 up = position.normalized;
        return Physics.gravity.y < 0f ? up: -up; // Away or Towards Ground
    }



}
