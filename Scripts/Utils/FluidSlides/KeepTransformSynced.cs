using UnityEngine;

public class KeepTransformSynced : MonoBehaviour
{
    public Transform targetTransform; // The Transform to keep in sync with

    void LateUpdate()
    {
        // Set this GameObject's transform to match the targetTransform's transform
        transform.position = targetTransform.position;
        transform.rotation = targetTransform.rotation;
        transform.localScale = targetTransform.localScale;
    }
}