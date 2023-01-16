using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    /// Limited Area and Bounciness 
    [SerializeField] Rect allowedArea = new Rect(-5f, -5f, 10f, 10f);
    [SerializeField, Range(0f, 1f)] float bounciness = 0.5f;

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


    // Snapping
    [SerializeField, Range(0f, 100f)] float maxSnapSpeed = 100f;
    [SerializeField, Min(0f)] float probeDistance = 1f; // Searching distance below sphere (for snapping)
    [SerializeField] LayerMask probeMask = -1, stairsMask = -1, climbMask = -1; // -1 matches all layers, manually exclude raycasting and agent layers

    [SerializeField] Material normalMaterial = default, climbingMaterial = default;
    MeshRenderer meshRenderer;

    [SerializeField]
    Transform playerInputSpace = default;

    Rigidbody body, connectedBody, previousConnectedBody;

    Vector3 velocity, desiredVelocity, connectionVelocity;

    Vector3 connectionWorldPosition, connectionLocalPosition;

    Vector3 contactNormal,  // Slope's normal
            steepNormal, // Steep contact's normal -Walls-
            climbNormal; // Climbable surface normal up to 45 Degrees overhang

    // Object's Y axis relative to its position, not gravity - Same for X and Z
    Vector3 upAxis, rightAxis, forwardAxis; 

    // To see which jump are we at
    int jumpPhase;

    int groundContactCount,                         // How many ground planes we contacting,  
        stepsSinceLastGrounded, stepsSinceLastJump, // Physics steps while on air, physics steps since last jump
        steepContactCount,    /*Wall*/              // A steep contact is one that is too steep to count as ground, but isn't a ceiling or overhang
        climbContactCount;                          // Climbable surface up to overhangs (45degrees)

    float minGroundDotProduct, minStairsDotProduct, minClimbDotProduct;

    bool desiredJump; // Willing to jump or not
    
    // Short way to define a single-statement readonly property
    bool OnGround => groundContactCount > 0; // returns true if at least 1 contact available
    bool OnSteep => steepContactCount > 0;
    bool Climbing => climbContactCount > 0;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false; // dont use standard gravity
        meshRenderer.material = Climbing ? climbingMaterial : normalMaterial;
        OnValidate();
    }

    // With OnValidate, treshold remains synchronized with the angle when we change it via the inspector while in play mode.
    private void OnValidate()
    {
        // The configured angle defines the minimum result that still counts as ground / stairs.
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad); // Method takes radians
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
        minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
    }

    // Update is called once per frame
    void Update()
    {
        // Easy access to the inputs
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");

        /// Choose one of them for your sphere (Without Rigidbody)
        // JoystickBehavior(playerInput);
        // BasicBouncySphereWithinArea(playerInput);

        // With Rigidbody
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        // Player Movement Relative to the Camera POV
        if (playerInputSpace)
        {
            /// if we just take the camera's transform direction, camera's Y movement affects sphere's movement
            //desiredVelocity = playerInputSpace.TransformDirection(playerInput.x, 0f, playerInput.y) * maxSpeed;

            /// Movement independent of Camera's Y Axis 
            //Vector3 forward = playerInputSpace.forward;
            //forward.y = 0f;
            //forward.Normalize();

            //Vector3 right = playerInputSpace.right;
            //right.y = 0f;
            //right.Normalize();

            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);

        }
        else // Keep Player input in world space
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
        }

        desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

        // We might end up not invoking FixedUpdate next frame, in which case desiredJump is set back to false and the desire to jump will be forgotten.
        // We can prevent that by combining the check with its previous value via the boolean OR operation, or the OR assignment.
        // That way it remains true once enabled until we explicitly set it back to false.
        desiredJump |= Input.GetButtonDown("Jump");

        /// Color for Debugging
        // ColorOnGroundContacts();
        // ColorOnAir();
    }

    // For the physics updates FixedUpdate is prefered
    // this way the sphere doesn't get jittery when colliding something
    private void FixedUpdate()
    {
        // Opposite direction of the gravity vector is our upwards axis
        Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);

        // On ground or not
        UpdateState();

        // Calculate velocity relative to the ground angles (won't bounce / lose grip)
        AdjustVelocity();
        
    
        // Jump
        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }

        if (!Climbing)
        {
            velocity += gravity * Time.deltaTime;
        }

        body.velocity = velocity;
        ClearState();
    }

    // Need to project directions on a plane to make Axes relative to ground we are on
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }

    bool CheckClimbing()
    {
        if (Climbing)
        {
            groundContactCount = climbContactCount;
            contactNormal = climbNormal;
            return true;
        }
        return false;
    }

    void ClearState()
    {
        groundContactCount = steepContactCount = climbContactCount = 0;
        contactNormal = steepNormal = connectionVelocity = climbNormal = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;

        if (CheckClimbing() || OnGround || SnapToGround() || CheckSteepContacts()) // if on ground or trying to stay
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
            if(connectedBody.isKinematic || connectedBody.mass >= body.mass)
            {
                UpdateConnectionState();
            }
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
                if(upDot >= minClimbDotProduct && (climbMask & (1 << layer)) != 0)
                {
                    climbContactCount += 1;
                    climbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
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
            if(jumpPhase == 0) // prevent extra air jump while falling off a surface without jumping
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

        // this way we get upward momentum while wall jumping, ground won't be affected
        jumpDirection = (jumpDirection + upAxis).normalized;

        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
            
         // We don't want limitless jumpspeed which means,
        // repeated jumping shouldn't add more speed to the movement
        if(alignedSpeed > 0f) // if already jumping(on air) 
        {
            // subtract this velocity from the mew jump action(stable jump velocity)
            // also if we're already going faster than the jump speed then we don't want a jump to slow us down,
            // either subtract from the jumpspeed or don't change anything ensuring that the modified jump speed never goes negative
            jumpSpeed = Mathf.Max(jumpSpeed - velocity.y, 0f);
        }

        velocity += jumpDirection * jumpSpeed;
        
    }

    void AdjustVelocity()
    {
        Vector3 xAxis, zAxis;
        if (Climbing)
        {
            xAxis = Vector3.Cross(contactNormal, upAxis);
            zAxis = upAxis;
        }
        else
        {
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }

        // Vectors aligned with the ground, but they are only of unit length when the ground is perfectly flat. So normalized to get proper directions.
        xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
        zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);

        Vector3 relativeVelocity = velocity - connectionVelocity;

        // Project the current velocity on both vectors to get the relative X and Z speeds.
        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);

        // if onground use acceleration else air acceleration
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        
        float maxSpeedChange = acceleration * Time.deltaTime;

        // Calculate new X and Z speeds relative to the ground
        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        // Adjust the velocity by adding the differences between the new and old speeds along the relative axes.
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
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
        if(speed > maxSnapSpeed) // if at high speeds our sphere gets launched anyway
        {
            return false;
        }

        // Use Raycasting to see if there is ground below, get info as to what we hit, exclude other spheres from raycasts
        if(!Physics.Raycast(body.position, -upAxis, out RaycastHit hitInfo, probeDistance, probeMask))
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

    void BasicBouncySphereWithinArea(Vector2 playerInput)
    {

        // Just normalizing the input makes the values between 0 and 1 inaccessible, but this way range[0,1] is accessible
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        // acceleration makes movement smoother but less responsive!
        Vector3 desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

        float maxSpeedChange = maxAcceleration * Time.deltaTime;

        velocity.x = Mathf.MoveTowards(velocity.x, desiredVelocity.x, maxSpeedChange);
        velocity.z = Mathf.MoveTowards(velocity.z, desiredVelocity.z, maxSpeedChange);

        // using speed formulas in physics
        Vector3 displacement = velocity * Time.deltaTime;

        Vector3 newPosition = transform.localPosition + displacement;

        // if new position is not within the rectangle
        if (!allowedArea.Contains(new Vector2(newPosition.x, newPosition.z)))
        {
            // We need to give it a new Vector2 because contains only checks x,y in Vector3 we would sent
            if (newPosition.x < allowedArea.xMin)
            {
                newPosition.x = allowedArea.xMin; // Make it stay inside
                velocity.x = -velocity.x * bounciness; // Make it bounce away 
            }
            else if (newPosition.x > allowedArea.xMax)
            {
                newPosition.x = allowedArea.xMax;
                velocity.x = -velocity.x * bounciness;
            }
            if (newPosition.z < allowedArea.yMin)
            {
                newPosition.z = allowedArea.yMin;
                velocity.z = -velocity.z * bounciness;
            }
            else if (newPosition.z > allowedArea.yMax)
            {
                newPosition.z = allowedArea.yMax;
                velocity.z = -velocity.z * bounciness;
            }
        }

        transform.localPosition = newPosition;

    }


    // Just to see how key inputs work 
    void JoystickBehavior(Vector2 playerInput)
    {
        // Normalizing the input so that even using keyboard, input behaves like a joystick would ()
        // playerInput.Normalize();

        // Just normalizing the input makes the values between 0 and 1 inaccessible, but this way range[0,1] is accessible
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        // Use the sphere to display the max ranges of inputs
        transform.localPosition = new Vector3(playerInput.x, 0.5f, playerInput.y);

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
