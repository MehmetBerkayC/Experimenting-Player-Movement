using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    /// Limited Area and Bounciness 
    [SerializeField] Rect allowedArea = new Rect(-5f, -5f, 10f, 10f);
    [SerializeField, Range(0f, 1f)] float bounciness = 0.5f;


    [SerializeField, Range(0f, 100f)] float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 10f)] float jumpHeight = 2f;
    [SerializeField, Range(0, 5)] int maxAirJumps = 0;
    [SerializeField, Range(0f, 90f)] float maxGroundAngle = 25f;

    Rigidbody body;

    // To see which jump are we at
    int jumpPhase;

    float minGroundDotProduct;

    Vector3 velocity, desiredVelocity;
    Vector3 contactNormal; // Slope's normal

    bool desiredJump, onGround;

    // With OnValidate, treshold remains synchronized with the angle when we change it via the inspector while in play mode.
    private void OnValidate()
    {
        // The configured angle defines the minimum result that still counts as ground.
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad); // Method takes radians
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
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

        desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

        // We might end up not invoking FixedUpdate next frame, in which case desiredJump is set back to false and the desire to jump will be forgotten.
        // We can prevent that by combining the check with its previous value via the boolean OR operation, or the OR assignment.
        // That way it remains true once enabled until we explicitly set it back to false.
        desiredJump |= Input.GetButtonDown("Jump");
    }

    // For the physics updates FixedUpdate is prefered
    // this way the sphere doesn't get jittery when colliding something
    private void FixedUpdate()
    {
        // On ground or not
        UpdateState();

        // Calculate velocity relative to the ground angles (won't bounce / lose grip)
        AdjustVelocity();
        
    
        // Jump
        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }

        body.velocity = velocity;

        ClearState();
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
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            /// No matter where, we jump on global Y Axis (plane's normal)
            // Assuming a Plane - Set true if normal vector from contact surface is 1(on ground)
            // onGround |= normal.y >= minGroundDotProduct; // min Angle we accept as ground

            /// This way jump will be angled on the slope's Y Axis (slope's normal)
            if (normal.y >= minGroundDotProduct)
            {
                onGround = true;
                contactNormal += normal;
            }
        }
    }
    void Jump() 
    {
        if (onGround || jumpPhase < maxAirJumps)
        {
            jumpPhase += 1;
            
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            float alignedSpeed = Vector3.Dot(velocity, contactNormal);
            
            // We don't want limitless jumpspeed which means,
            // repeated jumping shouldn't add more speed to the movement
            if(alignedSpeed > 0f) // if already jumping(on air) 
            {
                // subtract this velocity from the mew jump action(stable jump velocity)
                // also if we're already going faster than the jump speed then we don't want a jump to slow us down,
                // either subtract from the jumpspeed or don't change anything ensuring that the modified jump speed never goes negative
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }

            velocity += contactNormal * jumpSpeed;
        }
    }

    Vector3 ProjectOnContactPlane (Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity()
    {
        // Vectors aligned with the ground, but they are only of unit length when the ground is perfectly flat. So normalize to get proper directions.
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        // Project the current velocity on both vectors to get the relative X and Z speeds.
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        // if onground use acceleration else air acceleration
        float acceleration = onGround ? maxAcceleration : maxAirAcceleration;
        
        float maxSpeedChange = acceleration * Time.deltaTime;

        // Calculate new X and Z speeds relative to the ground
        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        // Adjust the velocity by adding the differences between the new and old speeds along the relative axes.
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
    void UpdateState()
    {
        velocity = body.velocity;
        if (onGround)
        {
            jumpPhase = 0;
            contactNormal.Normalize();
        }
        else // if on air, use global Y
        {
            contactNormal = Vector3.up;
        }
    }
    void ClearState()
    {
        onGround = false;
        contactNormal = Vector3.zero;
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


}
