using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravitySource : MonoBehaviour
{
    /// New gravity source types will do their work by overriding the GetGravity method with their own implementation. 
    /// To make this possible we have to declare this method to be virtual.
    public virtual Vector3 GetGravity(Vector3 position)
    {
        return Physics.gravity;
    }

    private void OnEnable()
    {
        CustomGravity.Register(this);
    }

    private void OnDisable()
    {
        CustomGravity.Unregister(this);
    }
}
