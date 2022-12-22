using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] float maxAcceleration = 10f;
    [SerializeField, Range(0f, 100f)] float maxSpeed = 10f;

    [SerializeField] Rect allowedArea = new Rect(-5f, -5f, 10f, 10f);
    [SerializeField, Range(0f, 1f)] float bounciness = 0.5f;
    Vector3 velocity;

    // Update is called once per frame
    void Update()
    {
        // Easy access to the inputs
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");

        /// Choose one of them for your sphere
        // JoystickBehavior(playerInput);
        BasicBouncySphereWithinArea(playerInput);
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
        { // We need to give it a new Vector2 because contains only checks x,y in Vector3 we would sent
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
