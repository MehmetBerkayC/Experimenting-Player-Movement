using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class StableFloatingRigidbody : MonoBehaviour
{
    [SerializeField] bool floatToSleep = false;
    [SerializeField] float destroyObjectBelowPosition = -50f;

    // In Water
    [SerializeField] float submergenceOffset = 0.5f;
    [SerializeField, Min(0.1f)] float submergenceRange = 1f;
    [SerializeField, Min(0f)] float buoyancy = 1f;
    [SerializeField, Range(0f, 10f)] float waterDrag = 1f;
    [SerializeField] LayerMask waterMask = 0;
    [SerializeField] Vector3[] buoyancyOffsets = default;
    [SerializeField] bool safeFloating = false;

    float[] submergence;

    Vector3 gravity;

    Rigidbody body;
    float floatDelay;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        submergence = new float[buoyancyOffsets.Length];
    }

    private void FixedUpdate()
    {
        if(body.position.y < destroyObjectBelowPosition)
        {
            Destroy(gameObject);
        }

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
        float dragFactor = waterDrag * Time.deltaTime / buoyancyOffsets.Length;
        float buoyancyFactor = -buoyancy / buoyancyOffsets.Length;

        for (int i = 0; i < buoyancyOffsets.Length; i++)
        {
            if (submergence[i] > 0f)
            {
                float drag = Mathf.Max(0f, 1f - dragFactor * submergence[i]);

                body.velocity *= drag;
                body.angularVelocity *= drag;

                body.AddForceAtPosition(gravity * (buoyancyFactor * submergence[i]), transform.TransformPoint(buoyancyOffsets[i]), ForceMode.Acceleration);
                submergence[i] = 0f;
            }
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
        Vector3 down = gravity.normalized;
        Vector3 offset = down * -submergenceOffset;
        for (int i = 0; i < buoyancyOffsets.Length; i++)
        {
            Vector3 p = offset + transform.TransformPoint(buoyancyOffsets[i]);

            if (Physics.Raycast(p, down, out RaycastHit hit, submergenceRange + 1f, waterMask, QueryTriggerInteraction.Collide))
            {
                submergence[i] = 1f - hit.distance / submergenceRange;
            }
            else if (!safeFloating || Physics.CheckSphere(p, 0.01f, waterMask, QueryTriggerInteraction.Collide))
            {
                submergence[i] = 1f;
            }
        }
    }

    void ColorSleep()
    {
        // IsSleeping never returns true because rb is asleep? idk
        GetComponent<Renderer>().material.SetColor(
           "_Color", floatDelay > 0f ? Color.yellow : body.IsSleeping() ? Color.gray : Color.red);
    }
}
