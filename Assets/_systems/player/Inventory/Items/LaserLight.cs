using UnityEngine;

/// <summary>
/// Local laser-dot visual. Attach it to the owner viewmodel only.
/// </summary>
public sealed class LaserLight : MonoBehaviour
{
    [SerializeField, Min(0f)] private float range = 100f;
    [SerializeField, Min(0f)] private float radius = 0.01f;
    [SerializeField] private float surfaceOffset = 0.002f;
    [SerializeField] private Transform lightTransform;
    [SerializeField] private Transform muzzle;
    [SerializeField] private LayerMask layerMask = ~0;

    private void LateUpdate()
    {
        if (muzzle == null || lightTransform == null)
            return;

        if (Physics.SphereCast(
                muzzle.position,
                radius,
                muzzle.forward,
                out RaycastHit hit,
                range,
                layerMask,
                QueryTriggerInteraction.Ignore))
        {
            lightTransform.gameObject.SetActive(true);
            lightTransform.position = hit.point + hit.normal * surfaceOffset;
            lightTransform.rotation = Quaternion.LookRotation(hit.normal);
        }
        else
        {
            lightTransform.gameObject.SetActive(false);
        }
    }
}
