using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravitySphere : GravitySource
{
    [SerializeField] float gravity = 9.81f;

    [SerializeField, Min(0f)] float outerRadius = 10f, outerFalloffRadius = 15f;
    [SerializeField, Min(0f)] float innerFalloffRadius = 1f, innerRadius = 5f;

    float outerFalloffFactor, innerFalloffFactor;

    private void Awake()
    {
        OnValidate();            
    }

    private void OnValidate()
    {
        /// Making sure that;
        // innerFalloffRadius is bigger than 0
        innerFalloffRadius = Mathf.Max(innerFalloffRadius, 0f);
        
        // innerRadius is bigger than innerFalloffRadius
        innerRadius = Mathf.Max(innerRadius, innerFalloffRadius);
        
        // outerRadius bigger than innerRadius
        outerRadius = Mathf.Max(outerRadius, innerRadius);
        
        // outerFalloffRadius is at least as big as outerRadius
        outerFalloffRadius = Mathf.Max(outerRadius, outerFalloffRadius);

        innerFalloffFactor = 1f / (innerRadius - innerFalloffRadius);
        outerFalloffFactor = 1f / (outerFalloffRadius - outerRadius);
    }

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 vector = transform.position - position;
        float distance = vector.magnitude;

        // When outside of range cut off gravity
        if(distance > outerFalloffRadius || distance < innerFalloffRadius)
        {
            return Vector3.zero;
        }
        float g = gravity / distance;

        if(distance > outerRadius)
        {
            g *= 1f - (distance - outerRadius) * outerFalloffFactor;
        }

        if(distance < innerRadius)
        {
            g *= 1f - (innerRadius - distance) * innerFalloffFactor;
        }

        return g * vector;
    }

    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;

        // for inverted gravity spheres
        if(innerFalloffRadius > 0f && innerFalloffRadius < innerRadius)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p, innerFalloffRadius);
        }
       
        Gizmos.color = Color.yellow;

        // for inverted gravity spheres
        if(innerRadius > 0f && innerRadius < outerRadius)
        {
            Gizmos.DrawWireSphere(p, innerRadius);
        }

        Gizmos.DrawWireSphere(p, outerRadius);

        if(outerFalloffRadius > outerRadius)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p, outerFalloffRadius);
        }
    }
}
