using UnityEngine;

public sealed class RiotShieldItem : HotbarHeldItem
{
    [SerializeField] private GameObject localShield;
    [SerializeField, Range(0f, 89f)] private float verticalAngle = 45f;
    [SerializeField] private Transform cameraForward;
    [SerializeField] private Transform shieldXAxisPivot;
    [SerializeField, Min(0f)] private float shieldRotateSpeed = 12f;

    private RiotShieldItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedRiotShield() : null;
        if (cameraForward == null && Camera.main != null)
            cameraForward = Camera.main.transform;
    }

    protected override void OnEquipped()
    {
        if (localShield != null)
            localShield.SetActive(true);

        networkedCounterpart?.RequestSetRiotShield(true);
    }

    protected override void OnEquippedUpdate()
    {
        if (localShield == null || cameraForward == null)
            return;

        float cameraPitch = NormalizeSignedAngle(cameraForward.eulerAngles.x);
        float clampedPitch = Mathf.Clamp(cameraPitch, -verticalAngle, verticalAngle);

        localShield.transform.position = cameraForward.position;
        Quaternion yawOnly = Quaternion.Euler(0f, cameraForward.eulerAngles.y, 0f);
        localShield.transform.rotation = Quaternion.Lerp(
            localShield.transform.rotation,
            yawOnly,
            shieldRotateSpeed * Time.deltaTime);

        if (shieldXAxisPivot != null)
        {
            Quaternion targetPitch = Quaternion.Euler(clampedPitch, 0f, 0f);
            shieldXAxisPivot.localRotation = Quaternion.Lerp(
                shieldXAxisPivot.localRotation,
                targetPitch,
                shieldRotateSpeed * Time.deltaTime);
        }
    }

    protected override void OnUnequipped()
    {
        if (localShield != null)
        {
            localShield.SetActive(false);
            localShield.transform.localPosition = Vector3.zero;
            localShield.transform.localRotation = Quaternion.identity;
        }

        if (shieldXAxisPivot != null)
            shieldXAxisPivot.localRotation = Quaternion.identity;

        networkedCounterpart?.RequestSetRiotShield(false);
    }

    private static float NormalizeSignedAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
