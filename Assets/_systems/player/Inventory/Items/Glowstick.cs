using UnityEngine;

public sealed class Glowstick : MonoBehaviour
{
    [SerializeField] private Transform lightTransform;
    [SerializeField] private float heightBoost;

    private void LateUpdate()
    {
        if (lightTransform != null)
            lightTransform.position = transform.position + Vector3.up * heightBoost;
    }
}
