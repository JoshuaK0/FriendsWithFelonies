using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Networked rope geometry. Anchor points are synchronized so every client
/// derives the same midpoint, rotation, length and collider state.
/// </summary>
public sealed class TightRopeProp : NetworkBehaviour
{
    [SerializeField] private Transform scaledRoot;
    [SerializeField] private GameObject colliders;
    [SerializeField] private Vector3 baseScale = Vector3.one;
    [SerializeField, Min(0.0001f)] private float modelLengthAlongZ = 1f;

    private readonly SyncVar<Vector3> firstAnchor = new(Vector3.zero);
    private readonly SyncVar<Vector3> secondAnchor = new(Vector3.zero);
    private readonly SyncVar<bool> initialized = new(false);

    public Vector3 FirstAnchor => firstAnchor.Value;
    public Vector3 SecondAnchor => secondAnchor.Value;

    private void Reset()
    {
        scaledRoot = transform;
        baseScale = transform.localScale;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        firstAnchor.OnChange += OnAnchorChanged;
        secondAnchor.OnChange += OnAnchorChanged;
        initialized.OnChange += OnInitializedChanged;
        ApplyGeometry();
    }

    public override void OnStopClient()
    {
        firstAnchor.OnChange -= OnAnchorChanged;
        secondAnchor.OnChange -= OnAnchorChanged;
        initialized.OnChange -= OnInitializedChanged;
        base.OnStopClient();
    }

    [Server]
    public void InitializeServer(Vector3 anchorA, Vector3 anchorB)
    {
        firstAnchor.Value = anchorA;
        secondAnchor.Value = anchorB;
        initialized.Value = true;
        ApplyGeometry();
    }

    private void OnAnchorChanged(Vector3 previous, Vector3 next, bool asServer)
    {
        ApplyGeometry();
    }

    private void OnInitializedChanged(bool previous, bool next, bool asServer)
    {
        ApplyGeometry();
    }

    private void ApplyGeometry()
    {
        bool isReady = initialized.Value;
        if (colliders != null)
            colliders.SetActive(isReady);

        if (!isReady)
            return;

        Vector3 delta = secondAnchor.Value - firstAnchor.Value;
        float length = delta.magnitude;
        if (length <= 0.0001f)
            return;

        Quaternion rotation = Quaternion.LookRotation(delta / length, Vector3.up);
        transform.SetPositionAndRotation(
            (firstAnchor.Value + secondAnchor.Value) * 0.5f,
            rotation);

        Transform target = scaledRoot != null ? scaledRoot : transform;
        if (target != transform)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
        }

        Vector3 scale = baseScale;
        scale.z = baseScale.z * (length / Mathf.Max(0.0001f, modelLengthAlongZ));
        target.localScale = scale;
    }

    // Compatibility method for old UnityEvent references.
    public void EnableZipLineServer()
    {
        if (colliders != null)
            colliders.SetActive(initialized.Value);
    }
}
