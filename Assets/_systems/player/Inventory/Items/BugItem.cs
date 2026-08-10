using System.Collections.Generic;
using UnityEngine;

public sealed class BugItem : HotbarHeldItem
{
    [Header("Placement")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    [Header("Owned bug indicators")]
    [SerializeField] private Camera indicatorCamera;
    [SerializeField] private Transform uiCanvas;
    [SerializeField] private GameObject bugIndicatorPrefab;
    [SerializeField, Range(1f, 180f)] private float viewAngleThreshold = 45f;
    [SerializeField, Min(0f)] private float uiScale = 100f;
    [SerializeField, Min(0f)] private float minimumUiScale = 0.5f;
    [SerializeField] private KeyCode activationKey = KeyCode.Mouse2;

    private readonly Dictionary<BugProp, GameObject> trackedIndicators = new();
    private BugItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedBug() : null;
        if (indicatorCamera == null)
            indicatorCamera = Camera.main;
    }

    protected override void OnEquipped()
    {
        preview?.SetVisible(true);
    }

    protected override void OnEquippedUpdate()
    {
        UpdatePlacement();
        UpdateBugIndicators();

        if (Input.GetKeyDown(activationKey))
            ActivateVisibleOwnedBugs();
    }

    protected override void OnUnequipped()
    {
        preview?.SetVisible(false);
        ClearAllIndicators();
    }

    private void UpdatePlacement()
    {
        if (rayOrigin == null || preview == null || networkedCounterpart == null)
            return;

        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                range,
                surfaceMask,
                QueryTriggerInteraction.Ignore))
        {
            preview.SetVisible(false);
            return;
        }

        Vector3 position = hit.point + hit.normal * surfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
        preview.SetVisible(true);
        preview.SetPose(position, rotation);

        if (preview.EvaluateClear() && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceBug(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    private void UpdateBugIndicators()
    {
        if (indicatorCamera == null || uiCanvas == null || bugIndicatorPrefab == null)
            return;

        List<BugProp> remove = null;
        foreach (KeyValuePair<BugProp, GameObject> entry in trackedIndicators)
        {
            if (entry.Key != null)
                continue;

            remove ??= new List<BugProp>();
            if (entry.Value != null)
                Destroy(entry.Value);
            remove.Add(entry.Key);
        }

        if (remove != null)
        {
            for (int i = 0; i < remove.Count; i++)
                trackedIndicators.Remove(remove[i]);
        }

        foreach (BugProp bug in BugProp.Instances)
        {
            if (bug == null || !bug.IsOwner)
                continue;

            Vector3 toBug = bug.transform.position - indicatorCamera.transform.position;
            float angle = Vector3.Angle(indicatorCamera.transform.forward, toBug);
            bool visible = angle <= viewAngleThreshold && Vector3.Dot(indicatorCamera.transform.forward, toBug) > 0f;

            if (!visible)
            {
                RemoveIndicator(bug);
                continue;
            }

            if (!trackedIndicators.TryGetValue(bug, out GameObject indicator) || indicator == null)
            {
                indicator = Instantiate(bugIndicatorPrefab, uiCanvas);
                trackedIndicators[bug] = indicator;
            }

            indicator.transform.position = indicatorCamera.WorldToScreenPoint(bug.transform.position);
            float distance = Mathf.Max(0.01f, toBug.magnitude);
            float scale = Mathf.Max(minimumUiScale, uiScale / distance);
            indicator.transform.localScale = Vector3.one * scale;
        }
    }

    private void ActivateVisibleOwnedBugs()
    {
        if (indicatorCamera == null || networkedCounterpart == null)
            return;

        foreach (BugProp bug in BugProp.Instances)
        {
            if (bug == null || !bug.IsOwner)
                continue;

            Vector3 toBug = bug.transform.position - indicatorCamera.transform.position;
            if (Vector3.Angle(indicatorCamera.transform.forward, toBug) <= viewAngleThreshold)
                networkedCounterpart.RequestActivateBug(bug.NetworkObject);
        }
    }

    private void RemoveIndicator(BugProp bug)
    {
        if (!trackedIndicators.TryGetValue(bug, out GameObject indicator))
            return;

        if (indicator != null)
            Destroy(indicator);
        trackedIndicators.Remove(bug);
    }

    private void ClearAllIndicators()
    {
        foreach (GameObject indicator in trackedIndicators.Values)
        {
            if (indicator != null)
                Destroy(indicator);
        }

        trackedIndicators.Clear();
    }
}
