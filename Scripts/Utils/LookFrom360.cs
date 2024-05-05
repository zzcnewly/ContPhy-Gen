using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform lookAtPoint; // the point the camera should look at
    public float moveSpeed = 2f; // the speed at which the camera should move
    public float moveDistance = 1f; // the maximum distance the camera should move from its starting position
    public float moveDuration = 1f; // the duration of each movement

    private Vector3 startPosition; // the starting position of the camera
    private float moveTimer = 0f; // the timer for each movement
    private Vector3 targetPosition; // the target position for each movement

    private void Start()
    {
        startPosition = transform.position;
        targetPosition = GetRandomTargetPosition();
    }

    private void Update()
    {
        // move the camera towards the target position
        transform.position = Vector3.Lerp(startPosition, targetPosition, moveTimer / moveDuration);
        moveTimer += Time.deltaTime * moveSpeed;

        // if the camera has reached the target position, choose a new target position
        if (moveTimer >= moveDuration)
        {
            startPosition = targetPosition;
            targetPosition = GetRandomTargetPosition();
            moveTimer = 0f;
        }

        // look at the fixed point
        transform.LookAt(lookAtPoint);
    }

    private Vector3 GetRandomTargetPosition()
    {
        // choose a random position within the move distance from the starting position
        return startPosition + Random.insideUnitSphere * moveDistance;
    }
}