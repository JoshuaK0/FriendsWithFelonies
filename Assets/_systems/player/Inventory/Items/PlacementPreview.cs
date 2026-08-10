using UnityEngine;

/// <summary>
/// Shared local-only placement preview. The overlap test ignores colliders that belong to the preview itself.
/// </summary>
public sealed class PlacementPreview : MonoBehaviour
{
    [SerializeField] private GameObject indicator;
    [SerializeField] private MeshRenderer[] renderers;
    [SerializeField] private Material clearMaterial;
    [SerializeField] private Material blockedMaterial;
    [SerializeField] private Vector3 collisionHalfExtents = Vector3.one * 0.25f;
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField, Min(4)] private int maxOverlaps = 32;

    private Collider[] overlapBuffer;

    public Transform IndicatorTransform => indicator != null ? indicator.transform : transform;

    private void Awake()
    {
        overlapBuffer = new Collider[Mathf.Max(4, maxOverlaps)];
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (indicator != null)
            indicator.SetActive(visible);
    }

    public void SetPose(Vector3 position, Quaternion rotation)
    {
        Transform target = IndicatorTransform;
        target.SetPositionAndRotation(position, rotation);
    }

    public bool EvaluateClear(bool additionalCondition = true)
    {
        Transform target = IndicatorTransform;
        int count = Physics.OverlapBoxNonAlloc(
            target.position,
            collisionHalfExtents,
            overlapBuffer,
            target.rotation,
            blockingMask,
            QueryTriggerInteraction.Ignore);

        bool clear = true;
        for (int i = 0; i < count; i++)
        {
            Collider overlap = overlapBuffer[i];
            if (overlap == null)
                continue;

            if (overlap.transform == target || overlap.transform.IsChildOf(target))
                continue;

            clear = false;
            break;
        }

        clear &= additionalCondition;
        SetMaterial(clear ? clearMaterial : blockedMaterial);
        return clear;
    }

    private void SetMaterial(Material material)
    {
        if (material == null || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].sharedMaterial = material;
        }
    }
}
