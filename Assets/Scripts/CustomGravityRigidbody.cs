using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour
{
    [SerializeField] bool floatToSleep = false;
    
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
        body.AddForce(
            CustomGravity.GetGravity(body.position), ForceMode.Acceleration);


        // For Debugging
        //ColorSleep();
        
    }

    void ColorSleep()
    {
        // IsSleeping never returns true because rb is asleep? idk
        GetComponent<Renderer>().material.SetColor(
           "_Color", floatDelay > 0f ? Color.yellow : body.IsSleeping() ? Color.gray : Color.red );
    }
}
