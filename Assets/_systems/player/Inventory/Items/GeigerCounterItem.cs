using System.Collections;
using UnityEngine;

public sealed class GeigerCounterItem : HotbarHeldItem
{
    [Header("Detection")]
    [SerializeField] private Transform forwardLooker;
    [SerializeField, Min(0f)] private float maximumDistance = 20f;
    [SerializeField, Min(0f)] private float minimumDistance = 1f;
    [SerializeField, Min(0.01f)] private float minimumClickInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float maximumClickInterval = 1f;
    [SerializeField, Min(0.01f)] private float outOfRangeClickInterval = 3f;
    [SerializeField, Range(0f, 180f)] private float maximumAngle = 100f;
    [SerializeField, Min(0f)] private float angleInfluence = 1f;
    [SerializeField, Min(0.01f)] private float targetRefreshRate = 0.25f;
    [SerializeField] private bool toggleable = true;

    [Header("Audio")]
    [SerializeField] private AudioSource localAudioSource;
    [SerializeField, Range(0.1f, 3f)] private float minimumPitch = 0.8f;
    [SerializeField, Range(0.1f, 3f)] private float maximumPitch = 1.5f;
    [SerializeField, Range(0.1f, 3f)] private float outOfRangePitch = 0.6f;
    [SerializeField, Min(0f)] private float volume = 1f;

    private GeigerTarget ownerTarget;
    private GeigerTarget trackedTarget;
    private float nextTargetRefreshTime;
    private float nextPingTime;
    private bool isOn;
    private Coroutine pingRoutine;
    private GeigerCounterItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedGeigerCounter() : null;
        ownerTarget = Inventory != null
            ? Inventory.GetComponentInParent<GeigerTarget>()
            : null;

        if (forwardLooker == null)
            forwardLooker = transform;
    }

    protected override void OnEquipped()
    {
        isOn = !toggleable;
        nextPingTime = Time.time;
    }

    protected override void OnEquippedUpdate()
    {
        if (toggleable && Input.GetMouseButtonDown(0))
        {
            isOn = !isOn;
            nextPingTime = Time.time;
        }

        if (isOn)
            UpdateSonar();
    }

    protected override void OnUnequipped()
    {
        isOn = false;
        trackedTarget = null;

        if (pingRoutine != null)
            StopCoroutine(pingRoutine);

        if (localAudioSource != null)
            localAudioSource.Stop();
    }

    private void UpdateSonar()
    {
        if (Time.time >= nextTargetRefreshTime)
        {
            trackedTarget = FindClosestEnemyTarget();
            nextTargetRefreshTime = Time.time + targetRefreshRate;
        }

        float pitch;
        float interval;

        if (trackedTarget == null)
        {
            pitch = outOfRangePitch;
            interval = outOfRangeClickInterval;
        }
        else
        {
            Transform targetPoint = trackedTarget.TargetPoint;
            float distance = Vector3.Distance(transform.position, targetPoint.position);
            float angle = Vector3.Angle(
                forwardLooker.forward,
                targetPoint.position - forwardLooker.position);

            if (distance > maximumDistance)
            {
                pitch = outOfRangePitch;
                interval = outOfRangeClickInterval;
            }
            else
            {
                float distanceValue = Mathf.Clamp01(Mathf.InverseLerp(maximumDistance, minimumDistance, distance));
                float angleValue = 1f - Mathf.Clamp01(Mathf.InverseLerp(0f, maximumAngle, angle));
                float combined = (distanceValue + angleInfluence * angleValue) / (1f + angleInfluence);

                pitch = Mathf.Lerp(minimumPitch, maximumPitch, combined);
                interval = Mathf.Lerp(maximumClickInterval, minimumClickInterval, distanceValue);
            }
        }

        if (Time.time < nextPingTime)
            return;

        nextPingTime = Time.time + interval;
        PlayPing(pitch);
    }

    private GeigerTarget FindClosestEnemyTarget()
    {
        GeigerTarget closest = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (GeigerTarget target in GeigerTarget.Instances)
        {
            if (target == null || target == ownerTarget)
                continue;

            if (ownerTarget != null && target.TeamId == ownerTarget.TeamId)
                continue;

            float sqrDistance = (target.TargetPoint.position - transform.position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closest = target;
        }

        return closest;
    }

    private void PlayPing(float pitch)
    {
        if (localAudioSource != null)
        {
            localAudioSource.pitch = pitch;
            localAudioSource.volume = volume;

            if (pingRoutine != null)
                StopCoroutine(pingRoutine);

            pingRoutine = StartCoroutine(RestartAudioNextFrame());
        }

        networkedCounterpart?.RequestGeigerPing(pitch);
    }

    private IEnumerator RestartAudioNextFrame()
    {
        localAudioSource.Stop();
        yield return null;
        localAudioSource.Play();
        pingRoutine = null;
    }
}
