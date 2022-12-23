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

    Rigidbody body;

    // To see which jump are we at
    int jumpPhase;

    Vector3 velocity, desiredVelocity;
    
    bool desiredJump, onGround;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
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
        UpdateState();

        // if onground use acceleration else air acceleration
        float acceleration = onGround ? maxAcceleration : maxAirAcceleration;
        
        // acceleration of the sphere
        float maxSpeedChange = acceleration * Time.deltaTime;

        velocity.x = Mathf.MoveTowards(velocity.x, desiredVelocity.x, maxSpeedChange);
        velocity.z = Mathf.MoveTowards(velocity.z, desiredVelocity.z, maxSpeedChange);

        // Jump
        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }

        body.velocity = velocity;

        onGround = false;
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
            // Assuming a Plane - Set true if normal vector from contact surface is 1(on ground)
            onGround |= normal.y >= 0.9f; // To be sure we set it >= 0.9f
        }
    }
    void Jump() 
    {
        if (onGround || jumpPhase < maxAirJumps)
        {
            jumpPhase += 1;
            
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            // We don't want limitless jumpspeed which means,
            // repeated jumping shouldn't add more speed to the movement
            if(velocity.y > 0f) // if already jumping(on air) with velocity upwards
            {
                // subtract this velocity from the mew jump action(stable jump velocity)
                // also if we're already going faster than the jump speed then we don't want a jump to slow us down,
                // either subtract from the jumpspeed or don't change anything ensuring that the modified jump speed never goes negative
                jumpSpeed = Mathf.Max(jumpSpeed - velocity.y, 0f);
            }

            velocity.y += jumpSpeed;
        }
    }

    void UpdateState()
    {
        velocity = body.velocity;
        if (onGround)
        {
            jumpPhase = 0;
        }
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
