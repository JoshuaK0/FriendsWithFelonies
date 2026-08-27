using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class RopeVisual :
    MonoBehaviour,
    IHotbarItemContextReceiver
{
    [SerializeField, Min(1)] private int quality = 20;
    [SerializeField] private float damper = 14f;
    [SerializeField] private float strength = 800f;
    [SerializeField] private float velocity = 15f;
    [SerializeField] private float waveCount = 3f;
    [SerializeField] private float waveHeight = 1f;
    [SerializeField] private AnimationCurve affectCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0f)] private float travelSpeed = 40f;

    private readonly Spring spring = new();
    private LineRenderer lineRenderer;
    private Vector3 currentGrapplePosition;
    private Vector3 targetPosition;
    private Vector3 currentPosition;
    private bool drawRope;
    private bool isInitialized;

    public void InitializeHotbarItem(
        NetHotbarInventory inventory,
        int itemId)
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
            return;

        isInitialized = true;
        lineRenderer = GetComponent<LineRenderer>();
        spring.SetTarget(0f);
    }

    private void LateUpdate()
    {
        EnsureInitialized();
        DrawRope();
    }

    public void ToggleRopeVisual(bool enabled)
    {
        drawRope = enabled;
    }

    public void SetTargetPos(Vector3 newTargetPosition)
    {
        targetPosition = newTargetPosition;
        currentPosition = muzzle != null ? muzzle.position : transform.position;
    }

    private void DrawRope()
    {
        if (lineRenderer == null || muzzle == null)
            return;

        currentPosition = Vector3.MoveTowards(currentPosition, targetPosition, travelSpeed * Time.deltaTime);

        if (!drawRope)
        {
            currentGrapplePosition = muzzle.position;
            spring.Reset();
            lineRenderer.positionCount = 0;
            return;
        }

        int segmentCount = Mathf.Max(1, quality);
        if (lineRenderer.positionCount == 0)
        {
            spring.SetVelocity(velocity);
            lineRenderer.positionCount = segmentCount + 1;
        }

        spring.SetDamper(damper);
        spring.SetStrength(strength);
        spring.Update(Time.deltaTime);

        Vector3 direction = currentPosition - muzzle.position;
        if (direction.sqrMagnitude < 0.0001f)
            direction = muzzle.forward;

        Quaternion basis = Quaternion.LookRotation(direction.normalized);
        Vector3 up = basis * Vector3.up;
        Vector3 right = basis * Vector3.right;

        currentGrapplePosition = Vector3.Lerp(
            currentGrapplePosition,
            currentPosition,
            Time.deltaTime * 12f);

        for (int i = 0; i <= segmentCount; i++)
        {
            float delta = i / (float)segmentCount;
            float curve = affectCurve != null ? affectCurve.Evaluate(delta) : delta;
            Vector3 offset =
                up * waveHeight * Mathf.Sin(delta * waveCount * Mathf.PI) * spring.Value * curve +
                right * waveHeight * Mathf.Cos(delta * waveCount * Mathf.PI) * spring.Value * curve;

            lineRenderer.SetPosition(
                i,
                Vector3.Lerp(muzzle.position, currentGrapplePosition, delta) + offset);
        }
    }
}
