using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour
{
    [SerializeField] bool floatToSleep = false;

    // In Water
    [SerializeField] float submergenceOffset = 0.5f;
    [SerializeField, Min(0.1f)] float submergenceRange = 1f;
    [SerializeField, Min(0f)] float buoyancy = 1f;
    [SerializeField, Range(0f, 10f)] float waterDrag = 1f;
    [SerializeField] LayerMask waterMask = 0;
    [SerializeField] Vector3 buoyancyOffset = Vector3.zero;

    float submergence;

    Vector3 gravity;

    Rigidbody body;
    float floatDelay;
    
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (floatToSleep)
        {
            if (body.IsSleeping()) // RB asleep
            {
                floatDelay = 0f;
                return;
            }

            if (body.velocity.sqrMagnitude < 0.0001f) // Stopping acceleration
            {
                floatDelay += Time.deltaTime;

                if (floatDelay >= 1f) // if stationary for more than 1sec~
                {
                    return; // don't apply gravity
                }
            }
            else
            {
                floatDelay = 0f;
                
            }
        }

        // Use custom gravity for this gameObject
        gravity = CustomGravity.GetGravity(body.position);
        if(submergence > 0f)
        {
            float drag = Mathf.Max(0f, 1f - waterDrag * submergence * Time.deltaTime);
            
            body.velocity *= drag;
            body.angularVelocity *= drag;

            body.AddForceAtPosition(
                gravity * -(buoyancy * submergence),
                transform.TransformPoint(buoyancyOffset),
                ForceMode.Acceleration
            ); 
        }
        body.AddForce(gravity, ForceMode.Acceleration);


        // For Debugging
        //ColorSleep();
        
    }

    void OnTriggerEnter(Collider other)
    {
        if ((waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence();
        }
    }

    void OnTriggerStay(Collider other)
    {   // Body can still sleep while floating, if thats the case shouldn't do calculations
        if (!body.IsSleeping() && (waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence();
        }
    }

    void EvaluateSubmergence()
    {
        Vector3 upAxis = -gravity.normalized;
        if (Physics.Raycast(
            body.position + upAxis * submergenceOffset,
            -upAxis, out RaycastHit hit, submergenceRange + 1f,
            waterMask, QueryTriggerInteraction.Collide
        ))
        {
            submergence = 1f - hit.distance / submergenceRange;
        }
        else
        {
            submergence = 1f;
        }
    }

    void ColorSleep()
    {
        // IsSleeping never returns true because rb is asleep? idk
        GetComponent<Renderer>().material.SetColor(
           "_Color", floatDelay > 0f ? Color.yellow : body.IsSleeping() ? Color.gray : Color.red );
    }
}
