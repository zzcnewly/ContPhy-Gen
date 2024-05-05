
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using ClothCollisionConfig;
public class ExhibitionCameraController : MonoBehaviour
{
    public Vector3 target = new Vector3(cfg.look_at_x, cfg.look_at_y, cfg.look_at_z); // the target that the camera should orbit
    public Vector3 offset; // offset form the target position
    public float rotationSpeed = cfg.rotationSpeed; // speed of the rotation
    public float jitterAmount = cfg.jitterAmount; // amount of the jitter
    public float rotationDuration = cfg.rotationDuration; // rotation duration in seconds
    private float angleLimit = cfg.angleLimit;
    private float rotationY = cfg.rotationY;
    private float originalX;

    void Start()
    {
        Debug.Log("CameraController Start " + transform.position);
        offset = transform.position - target;
        Quaternion rotation = Quaternion.LookRotation(offset); // Compute the rotation
        Vector3 eulerAngles = rotation.eulerAngles;
        originalX = 0;
        StartCoroutine(RotateCamera());
    }

    IEnumerator RotateCamera()
    {
        float timer = 0.0f;

        while(timer < rotationDuration)
        {
            timer += Time.deltaTime;

            // oscillate rotationY between -30 and 30 degrees
            rotationY = angleLimit * Mathf.Sin((2.0f * Mathf.PI * timer) / rotationDuration);

            // Calculate the rotation
            Quaternion rotation = Quaternion.Euler(originalX, rotationY, 0);
            // Quaternion rotation = Quaternion.identity;

            // Update the position
            transform.position = target + rotation * offset;

            // Look at the target
            transform.LookAt(target);

            // Add random jitter
            Vector3 jitter = new Vector3(0, Random.Range(-jitterAmount, jitterAmount), 0);
            transform.position += jitter;

            yield return null;
        }

        // Once the rotation is done, smoothly return the camera back to the original pose
        while(rotationY != 0.0f)
        {
            rotationY = Mathf.Lerp(rotationY, 0, Time.deltaTime * rotationSpeed);
            Quaternion rotation = Quaternion.Euler(originalX, rotationY, 0);
            transform.position = target + rotation * offset;
            transform.LookAt(target);
            yield return null;
        }
    }
}
