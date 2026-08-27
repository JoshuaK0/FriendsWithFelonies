using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Two-click local tight-rope tool. The first click selects the far anchor;
/// the second click selects the other anchor and asks the server to spawn it.
/// </summary>
public sealed class TightRopeItem : HotbarHeldItem
{
    [Header("Raycasts")]
    private Transform rayOrigin;
    [SerializeField] private LayerMask anchorMask = ~0;
    [SerializeField, Min(0f)] private float firstAnchorRange = 40f;
    [SerializeField, Min(0f)] private float secondAnchorRange = 8f;
    [SerializeField, Min(0f)] private float minimumAnchorSeparation = 1f;

    [Header("Presentation")]
    [SerializeField] private RopeVisual ropeVisual;
    [SerializeField] private AudioSource wireTravelAudio;
    [SerializeField] private AudioSource impactAudio;
    [SerializeField] private AudioSource anchorAudio;
    [SerializeField, Min(0.01f)] private float wireTravelSpeed = 30f;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private UnityEvent onFirstAnchorFired;
    [SerializeField] private UnityEvent onRopePlaced;
    [SerializeField] private UnityEvent onRopeCancelled;

    private bool awaitingSecondAnchor;
    private Vector3 firstAnchor;
    private Coroutine impactRoutine;

    private TightRopeItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
		if (CharacterServices != null)
			rayOrigin = CharacterServices.muzzle;

        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedTightRope() : null;
    }

    protected override void OnEquippedUpdate()
    {
        if (rayOrigin == null || networkedCounterpart == null)
            return;

        if (awaitingSecondAnchor && (Input.GetKeyDown(cancelKey) || Input.GetMouseButtonDown(1)))
        {
            CancelAnchoring();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!awaitingSecondAnchor)
            TrySelectFirstAnchor();
        else
            TryPlaceRope();
    }

    protected override void OnUnequipped()
    {
        CancelAnchoring(false);
    }

    private void TrySelectFirstAnchor()
    {
        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                firstAnchorRange,
                anchorMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        awaitingSecondAnchor = true;
        firstAnchor = hit.point;
        ropeVisual?.SetTargetPos(firstAnchor);
        ropeVisual?.ToggleRopeVisual(true);

        if (wireTravelAudio != null)
            wireTravelAudio.Play();

        if (impactRoutine != null)
            StopCoroutine(impactRoutine);

        impactRoutine = StartCoroutine(PlayImpactAfterTravel(
            Vector3.Distance(rayOrigin.position, firstAnchor) / wireTravelSpeed));

        onFirstAnchorFired?.Invoke();
    }

    private void TryPlaceRope()
    {
        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                secondAnchorRange,
                anchorMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 secondAnchor = hit.point;
        if (Vector3.Distance(firstAnchor, secondAnchor) < minimumAnchorSeparation)
            return;

        networkedCounterpart.RequestPlaceTightRope(firstAnchor, secondAnchor);

        awaitingSecondAnchor = false;
        ropeVisual?.ToggleRopeVisual(false);
        StopTravelAudio();

        if (anchorAudio != null)
            anchorAudio.Play();

        onRopePlaced?.Invoke();
        Inventory?.ConsumeOneConfirmed(ItemId);
    }

    private IEnumerator PlayImpactAfterTravel(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        impactRoutine = null;
        StopTravelAudio();

        if (awaitingSecondAnchor && impactAudio != null)
            impactAudio.Play();
    }

    private void CancelAnchoring(bool invokeEvent = true)
    {
        bool wasAnchoring = awaitingSecondAnchor;
        awaitingSecondAnchor = false;
        firstAnchor = Vector3.zero;
        ropeVisual?.ToggleRopeVisual(false);

        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }

        StopTravelAudio();

        if (invokeEvent && wasAnchoring)
            onRopeCancelled?.Invoke();
    }

    private void StopTravelAudio()
    {
        if (wireTravelAudio != null)
            wireTravelAudio.Stop();
    }
}
