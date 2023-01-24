using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    // Ground Control
    [SerializeField, Range(0f, 100f)] float maxAcceleration = 10f;
    [SerializeField, Range(0f, 100f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 90f)] float maxGroundAngle = 25f, maxStairsAngle = 50f;

    // Air Control
    [SerializeField, Range(0f, 100f)] float maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 10f)] float jumpHeight = 2f;
    [SerializeField, Range(0, 5)] int maxAirJumps = 0;

    // Climbing 
    [SerializeField, Range(90, 180)] float maxClimbAngle = 140f;
    [SerializeField, Range(0f, 100f)] float maxClimbSpeed = 2f, maxClimbAcceleration = 20f;

    // Swimming
    [SerializeField] float submergenceOffset = 0.5f;
    [SerializeField, Min(0.1f)] float submergenceRange = 1f;
    [SerializeField, Range(0f, 10f)] float waterDrag = 1f;
    [SerializeField, Min(0f)] float buoyancy = 1f;
    [SerializeField, Range(0.01f, 1f)] float swimTreshold = 0.5f;
    [SerializeField, Range(0f, 100f)] float maxSwimSpeed = 5f, maxSwimAcceleration = 5f;


    // Snapping
    [SerializeField, Range(0f, 100f)] float maxSnapSpeed = 100f;
    [SerializeField, Min(0f)] float probeDistance = 1f; // Searching distance below sphere (for snapping)

    // -1 matches all layers, manually exclude raycasting and agent layers etc.
    [SerializeField]
    LayerMask probeMask = -1,
              stairsMask = -1,
              climbMask = -1,
              waterMask = 0;

    [SerializeField]
    Material normalMaterial = default,
             climbingMaterial = default,
             swimmingMaterial = default;
    
    MeshRenderer meshRenderer;

    // Ball
    [SerializeField] Transform ball = default;
    [SerializeField, Min(0f)] float ballAlignSpeed = 180f;
    [SerializeField, Min(0f)] float ballAirRotation = 0.5f, ballSwimRotation = 2f;
    Vector3 lastContactNormal, lastSteepNormal, lastConnectionVelocity;


    [SerializeField]
    Transform playerInputSpace = default;

    [SerializeField, Min(0.1f)] float ballRadius = 0.5f;

    Rigidbody body, connectedBody, previousConnectedBody;

    Vector3 velocity, connectionVelocity;

    Vector3 connectionWorldPosition, connectionLocalPosition;

    Vector3 contactNormal,  // Slope's normal
            steepNormal, // Steep contact's normal -Walls-
            climbNormal, // Climbable surface normal up to 45 Degrees overhang
            lastClimbNormal; // for crevasses(special case)

    // Object's Y axis relative to its position, not gravity - Same for X and Z
    Vector3 upAxis, rightAxis, forwardAxis;

    Vector3 playerInput;

    // To see which jump are we at
    int jumpPhase;

    int groundContactCount,                         // How many ground planes we contacting,  
        stepsSinceLastGrounded, stepsSinceLastJump, // Physics steps while on air, physics steps since last jump
        steepContactCount,    /*Wall*/              // A steep contact is one that is too steep to count as ground, but isn't a ceiling or overhang
        climbContactCount;                          // Climbable surface up to overhangs (45degrees)

    float minGroundDotProduct, minStairsDotProduct, minClimbDotProduct;

    bool desiredJump, // Willing to jump or not
         desiresClimbing; // Climb or don't

    float submergence;

    // Short way to define a single-statement readonly property
    bool OnGround => groundContactCount > 0; // returns true if at least 1 contact available
    bool OnSteep => steepContactCount > 0;
    bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2;
    bool InWater => submergence > 0f;
    bool Swimming => submergence >= swimTreshold;

    // With OnValidate, treshold remains synchronized with the angle when we change it via the inspector while in play mode.
    private void OnValidate()
    {
        // The configured angle defines the minimum result that still counts as ground / stairs.
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad); // Method takes radians
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
        minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false; // dont use standard gravity
        meshRenderer = ball.GetComponent<MeshRenderer>();
        OnValidate();
    }

    // Update is called once per frame
    void Update()
    {
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.z = Input.GetAxis("Vertical");
        playerInput.y = Swimming ? Input.GetAxis("Dive") : 0f;

        playerInput = Vector3.ClampMagnitude(playerInput, 1f);

        // Player Movement Relative to the Camera POV
        if (playerInputSpace)
        {
            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
        }
        else // Keep Player input in world space
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
        }

        if (Swimming)
        {
            desiresClimbing = false;
        }
        else
        {
            // We might end up not invoking FixedUpdate next frame, in which case desiredJump is set back to false and the desire to jump will be forgotten.
            // We can prevent that by combining the check with its previous value via the boolean OR operation, or the OR assignment.
            // That way it remains true once enabled until we explicitly set it back to false.
            desiredJump |= Input.GetButtonDown("Jump");
            desiresClimbing = Input.GetKey(KeyCode.C);
        }

        UpdateBall();

        /// Color for Debugging
        // ColorOnGroundContacts();
        // ColorOnAir();

        // To test submergence value
        //meshRenderer.material.color = Color.white * submergence;

    }

    // Visual Updating
    void UpdateBall()
    {
        Material ballMaterial = normalMaterial;
        Vector3 rotationPlaneNormal = lastContactNormal;
        float rotationFactor = 1f;

        if (Climbing)
        {
            ballMaterial = climbingMaterial;
        }
        else if (Swimming)
        {
            ballMaterial = swimmingMaterial;
            rotationFactor = ballSwimRotation;

        }
        else if (!OnGround)
        {
            if (OnSteep)
            {
                lastContactNormal = lastSteepNormal;
            }
            else
            {
                rotationFactor = ballAirRotation;
            }
        }

        meshRenderer.material = ballMaterial;

        Vector3 movement = (body.velocity - lastConnectionVelocity) * Time.deltaTime;

        // Stable Y movement in complex gravity
        movement -= rotationPlaneNormal * Vector3.Dot(movement, rotationPlaneNormal);

        float distance = movement.magnitude;

        // Prevent rotation along with connected bodies(platforms)
        Quaternion rotation = ball.localRotation;

        if (connectedBody && connectedBody == previousConnectedBody)
        {
            rotation = Quaternion.Euler(connectedBody.angularVelocity * (Mathf.Rad2Deg * Time.deltaTime)) * rotation;

            if (distance < 0.001f)
            {
                ball.localRotation = rotation;
                return;
            }
        }
        else if (distance < 0.001f)
        {
            return;
        }

        float angle = distance * rotationFactor * (180f / Mathf.PI) / ballRadius;

        Vector3 rotationAxis = Vector3.Cross(rotationPlaneNormal, movement).normalized;

        rotation = Quaternion.Euler(rotationAxis * angle) * rotation;

        if (ballAlignSpeed > 0f)
        {
            rotation = AlignBallRotation(rotationAxis, rotation, distance);
        }

        ball.localRotation = rotation;
    }
    Quaternion AlignBallRotation(Vector3 rotationAxis, Quaternion rotation, float traveledDistance)
    {
        Vector3 ballAxis = ball.up;
        float dot = Mathf.Clamp(Vector3.Dot(ballAxis, rotationAxis), -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float maxAngle = ballAlignSpeed * traveledDistance;

        Quaternion newAlignment = Quaternion.FromToRotation(ballAxis, rotationAxis) * rotation;

        if (angle <= maxAngle)
        {
            return newAlignment;
        }
        else
        {
            return Quaternion.SlerpUnclamped(rotation, newAlignment, maxAngle / angle);
        }
    }

    // For the physics updates FixedUpdate is prefered, this way the sphere doesn't get jittery when colliding something
    private void FixedUpdate()
    {
        // Opposite direction of the gravity vector is our upwards axis
        Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);

        // On ground or not
        UpdateState();

        if (InWater) // Apply drag first so acceleration is possible
        {
            velocity *= 1f - waterDrag * submergence * Time.deltaTime;
        }

        // Calculate velocity relative to the ground angles (won't bounce / lose grip)
        AdjustVelocity();
        
        // Jump
        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }

        if (Climbing)
        {
            velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
        }
        else if (InWater)
        {
            velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
        }
        else if (OnGround && velocity.sqrMagnitude < 0.01f)
        {
            velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
        }
        else if(desiresClimbing && OnGround)
        {
            velocity += (gravity - contactNormal * (maxClimbAcceleration * 0.9f)) * Time.deltaTime;
        }
        else
        {
            velocity += gravity * Time.deltaTime;
        }

        body.velocity = velocity;
        ClearState();
    }

    void ClearState()
    {
        // For Visual Alignment
        lastContactNormal = contactNormal;
        lastSteepNormal = steepNormal;
        lastConnectionVelocity = connectionVelocity;

        groundContactCount = steepContactCount = climbContactCount = 0;
        contactNormal = steepNormal = connectionVelocity = climbNormal = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
        submergence = 0f;
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;

        if (CheckClimbing() || CheckSwimming() || OnGround || SnapToGround() || CheckSteepContacts()) // if on ground or trying to stay
        {
            stepsSinceLastGrounded = 0;

            if (stepsSinceLastJump > 1) // while on air, be able to jump again
            {
                jumpPhase = 0;
            }

            if (groundContactCount > 1) // if there are multiple ground contacts
            {
                contactNormal.Normalize(); // normalize the accumulated vector
            }
        }
        else // if on air, use global Y
        {
            contactNormal = upAxis;
        }

        if (connectedBody)
        {
            if (connectedBody.isKinematic || connectedBody.mass >= body.mass)
            {
                UpdateConnectionState();
            }
        }
    }



    bool CheckClimbing()
    {
        if (Climbing)
        {
            if(climbContactCount > 1)
            {
                climbNormal.Normalize();
                float upDot = Vector3.Dot(upAxis, climbNormal);
                if(upDot >= minGroundDotProduct)
                {
                    climbNormal = lastClimbNormal;
                }
            }
            groundContactCount = 1;
            contactNormal = climbNormal;
            return true;
        }
        return false;
    }

    // return true if steep contacts are converted into a virtual ground normal 
    bool CheckSteepContacts()
    {
        if (steepContactCount > 1) // if multiple steep surfaces present
        {
            steepNormal.Normalize();

            float upDot = Vector3.Dot(upAxis, steepNormal);
            if (upDot >= minGroundDotProduct) // check if the result can be classified as ground
            {
                steepContactCount = 0;
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true; // conversion accomplished
            }
        }
        return false; // failed
    }


    bool CheckSwimming()
    {
        if (Swimming)
        {
            groundContactCount = 0;
            contactNormal = upAxis;
            return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if((waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence(other);
        }    
    }

    private void OnTriggerStay(Collider other)
    {
        if ((waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence(other);
        }
    }

    void EvaluateSubmergence(Collider collider)
    {
        if(Physics.Raycast(body.position + upAxis * submergenceOffset, -upAxis, out RaycastHit hit, submergenceRange + 1f, waterMask, QueryTriggerInteraction.Collide))
        {
            // Slowly submerging to water
            submergence = 1f - hit.distance / submergenceRange;
        }
        else 
        {
            // Fully submerged in water
            submergence = 1f;
        }

        if (Swimming)
        {
            connectedBody = collider.attachedRigidbody;
        }
    }

    void UpdateConnectionState()
    {

        if (connectedBody == previousConnectedBody)
        {
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;

        }
        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);

    }

    void Jump(Vector3 gravity)
    {
        Vector3 jumpDirection;

        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep)
        {
            jumpDirection = steepNormal;
            jumpPhase = 0; // reset air jumps
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) // if able to jump 
        {
            if (jumpPhase == 0) // prevent extra air jump while falling off a surface without jumping
            {
                jumpPhase = 1;
            }

            jumpDirection = contactNormal;
        }
        else
        {
            return;
        }

        stepsSinceLastJump = 0; // jumping, so reset
        jumpPhase += 1;

        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);

        if (InWater)
        {
            jumpSpeed += Mathf.Max(0f, 1f - submergence / swimTreshold);
        }

        // this way we get upward momentum while wall jumping, ground won't be affected
        jumpDirection = (jumpDirection + upAxis).normalized;

        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);

        // We don't want limitless jumpspeed which means,
        // repeated jumping shouldn't add more speed to the movement
        if (alignedSpeed > 0f) // if already jumping(on air) 
        {
            // subtract this velocity from the mew jump action(stable jump velocity)
            // also if we're already going faster than the jump speed then we don't want a jump to slow us down,
            // either subtract from the jumpspeed or don't change anything ensuring that the modified jump speed never goes negative
            jumpSpeed = Mathf.Max(jumpSpeed - velocity.y, 0f);
        }

        velocity += jumpDirection * jumpSpeed;

    }

    public void PreventSnapToGround()
    {
        stepsSinceLastJump = -1;
    }

    bool SnapToGround()
    {
        // trying to snap right after losing contact to ground
        // if on air more than 1 physics step don't snap
        // or don't snap if a jump is initiated just now
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }

        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed) // if at high speeds our sphere gets launched anyway
        {
            return false;
        }

        // Use Raycasting to see if there is ground below, get info as to what we hit, exclude other spheres from raycasts
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hitInfo, probeDistance, probeMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float upDot = Vector3.Dot(upAxis, hitInfo.normal);

        // compare raycast info's normal(true surface normal) with minimum angle we count as ground
        if (upDot < GetMinDot(hitInfo.collider.gameObject.layer)) // checking ground can be stairs
        {
            return false;
        }

        // otherwise we are considered on ground
        groundContactCount = 1;
        contactNormal = hitInfo.normal;
        // Align velocity with ground
        float dot = Vector3.Dot(velocity, hitInfo.normal);

        /* 
         * At this point we are still floating above the ground, but gravity will take care of pulling us down to the surface.
         * the velocity might already point somewhat down, in which case realigning it would slow convergence to the ground. 
         * So we should only adjust the velocity when the dot product of it and the surface normal is positive.
         */
        if (dot > 0f)
        {
            velocity = (velocity - hitInfo.normal * dot).normalized * speed;
        }

        // When ground detected get its rigidbody
        connectedBody = hitInfo.rigidbody;

        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }
    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision (Collision collision)
    {
        if (Swimming)
        {
            return;
        }

        // Compare collisioned gameobjects layer
        int layer = collision.gameObject.layer;
        float minDot = GetMinDot(layer); // Ground, Stairs or Climbable Dot Product

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            /// No matter where, we jump on global Y Axis (plane's normal)
            // Assuming a Plane - Set true if normal vector from contact surface is 1(on ground)
            // onGround |= normal.y >= minGroundDotProduct; // min Angle we accept as ground

            /// This way jump will be angled on the slope's Y Axis (slope's normal)
            float upDot = Vector3.Dot(upAxis, normal);
            if (upDot >= minDot)
            {
                groundContactCount += 1; 
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }
            else
            {
                if (upDot > -0.01f) // if we don't have a ground contact check whether it's a steep contact
                {
                    steepContactCount += 1;
                    steepNormal += normal;

                    // only assign a slope body if there isn't already a ground contact
                    if (groundContactCount == 0)
                    {
                        connectedBody = collision.rigidbody;
                    }
                }
                if(desiresClimbing && upDot >= minClimbDotProduct && (climbMask & (1 << layer)) != 0)
                {
                    climbContactCount += 1;
                    climbNormal += normal;
                    lastClimbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }


    void AdjustVelocity()
    {
        float acceleration, speed;
        Vector3 xAxis, zAxis;
        if (Climbing)
        {
            acceleration = maxClimbAcceleration;
            speed = maxClimbSpeed;
            xAxis = Vector3.Cross(contactNormal, upAxis);
            zAxis = upAxis;
        }
        else if (InWater)
        {
            float swimFactor = Mathf.Min(1f, submergence / swimTreshold);
            acceleration = Mathf.LerpUnclamped(OnGround ? maxAcceleration : maxAirAcceleration, maxSwimAcceleration, swimFactor);
            speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }
        else
        {
            acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
            speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed;
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }

        // Vectors aligned with the ground, but they are only of unit length when the ground is perfectly flat. So normalized to get proper directions.
        xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
        zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);

        Vector3 relativeVelocity = velocity - connectionVelocity;

        Vector3 adjustment;
        adjustment.x = playerInput.x * speed - Vector3.Dot(relativeVelocity, xAxis);
        adjustment.z = playerInput.z * speed - Vector3.Dot(relativeVelocity, zAxis);
        adjustment.y = Swimming ? playerInput.y * speed - Vector3.Dot(relativeVelocity, upAxis) : 0f;

        adjustment = Vector3.ClampMagnitude(adjustment, acceleration * Time.deltaTime);       
        
        // Adjust the velocity by adding the differences between the new and old speeds along the relative axes.
        velocity += xAxis * adjustment.x + zAxis * adjustment.z;

        if (Swimming)
        {
            velocity += upAxis * adjustment.y;
        }
    }

 




    // returns appropriate minimum for a given layer 
    float GetMinDot(int layer)
    {
        // See Link for layering info
        // https://catlikecoding.com/unity/tutorials/movement/surface-contact/#:~:text=colliding%20with%20stairs.-,Max%20Stairs%20Angle,-If%20we%27re%20able
        // Assuming we can directly compare the stairs mask and layer, return correct dot product for given layer
        // (layer is integer but layermask is bitmask -can be considered integer-, see https://docs.unity3d.com/Manual/layers-and-layermasks.html)
        // There can be multiple layers for given mask so we need to support a mask for any combination of layers
        return (stairsMask & (1 << layer)) == 0 ? minGroundDotProduct : minStairsDotProduct;

    }

    // Need to project directions on a plane to make Axes relative to ground we are on
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }


    // Color Spheres if multiple ground contacts present
    void ColorOnGroundContacts()
    {
        // For Default Render Pipeline, Change color based on contacted ground count 
        GetComponent<Renderer>().material.SetColor(
            "_Color", Color.white * (groundContactCount * 0.25f)
        );
    }

    // Color Spheres if on air
    void ColorOnAir()
    {
        GetComponent<Renderer>().material.SetColor(
            "_Color", OnGround ? Color.black : Color.white
        );
    }

}
