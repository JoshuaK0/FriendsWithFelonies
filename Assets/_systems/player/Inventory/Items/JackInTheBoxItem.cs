using FishNet.Object;
using UnityEngine;

public sealed class JackInTheBoxItem : HotbarHeldItem
{
    [SerializeField] private JackInTheBoxDefinition definition;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private GameObject localBoxVisual;
    [SerializeField] private MonoBehaviour motorSource;
    [SerializeField] private Rigidbody fallbackRigidbody;

    [Header("Local audio")]
    [SerializeField] private AudioSource ambientAudio;
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip boxOpenClip;
    [SerializeField] private AudioClip scareClip;

    [Header("Fallback controls")]
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode leapKey = KeyCode.Space;

    private IJackInTheBoxMotor motor;
    private float crouchTime;
    private bool boxEnabled;
    private JackInTheBoxItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedJackInTheBox() : null;
        motor = motorSource as IJackInTheBoxMotor;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (attackOrigin == null && playerCamera != null)
            attackOrigin = playerCamera.transform;
    }

    protected override void OnEquippedUpdate()
    {
        if (definition == null)
            return;

        bool crouching = motor != null ? motor.IsCrouching : Input.GetKey(crouchKey);
        bool moving = motor != null
            ? motor.HasMovementInput
            : Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
              Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;

        if (!crouching || moving)
            crouchTime = 0f;
        else
            crouchTime += Time.deltaTime;

        if (!boxEnabled && crouchTime >= definition.ChargeTime)
            EnableBox();

        if (!boxEnabled)
            return;

        if (Input.GetKeyDown(crouchKey))
        {
            DisableBox();
            crouchTime = 0f;
            return;
        }

        if (Input.GetKeyDown(leapKey))
        {
            Vector3 velocity = attackOrigin.forward * definition.LeapForwardVelocity +
                               Vector3.up * definition.LeapUpVelocity;

            if (motor != null)
                motor.ApplyJackLeap(velocity);
            else if (fallbackRigidbody != null)
                fallbackRigidbody.AddForce(velocity, ForceMode.VelocityChange);

            DisableBox();
            crouchTime = 0f;
            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryAttack();
    }

    protected override void OnUnequipped()
    {
        DisableBox();
        crouchTime = 0f;
    }

    private void TryAttack()
    {
        if (attackOrigin == null || networkedCounterpart == null)
            return;

        if (!Physics.SphereCast(
                attackOrigin.position,
                definition.AttackRadius,
                attackOrigin.forward,
                out RaycastHit hit,
                definition.AttackRange,
                definition.AttackMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        NetworkObject targetObject = hit.collider.GetComponentInParent<NetworkObject>();
        if (targetObject == null || targetObject == Inventory.NetworkObject)
            return;

        networkedCounterpart.RequestAttack(targetObject, hit.point);
        networkedCounterpart.RequestPlayScare();

        if (oneShotAudio != null && scareClip != null)
            oneShotAudio.PlayOneShot(scareClip);

        DisableBox();
        crouchTime = 0f;
        Inventory?.ConsumeOneConfirmed(ItemId);
    }

    private void EnableBox()
    {
        if (boxEnabled)
            return;

        boxEnabled = true;
        localBoxVisual?.SetActive(true);
        motor?.SetBoxMovementLocked(true);
        networkedCounterpart?.RequestSetBoxState(true);

        if (oneShotAudio != null && boxOpenClip != null)
            oneShotAudio.PlayOneShot(boxOpenClip);

        if (ambientAudio != null)
            ambientAudio.Play();
    }

    private void DisableBox()
    {
        if (!boxEnabled)
            return;

        boxEnabled = false;
        localBoxVisual?.SetActive(false);
        motor?.SetBoxMovementLocked(false);
        networkedCounterpart?.RequestSetBoxState(false);

        if (ambientAudio != null)
            ambientAudio.Stop();
    }
}
