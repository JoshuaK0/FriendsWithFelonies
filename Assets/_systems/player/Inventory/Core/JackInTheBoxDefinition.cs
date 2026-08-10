using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Items/Jack In The Box Definition")]
public sealed class JackInTheBoxDefinition : ItemDefinition
{
    [Header("Jack In The Box")]
    [SerializeField, Min(0f)] private float chargeTime = 2f;
    [SerializeField, Min(0f)] private float attackRange = 4f;
    [SerializeField, Min(0f)] private float attackRadius = 0.5f;
    [SerializeField, Min(0f)] private float damage = 200f;
    [SerializeField, Min(0f)] private float damageDelay = 0.5f;
    [SerializeField, Min(0f)] private float ragdollForce = 100f;
    [SerializeField, Min(0f)] private float leapForwardVelocity = 8f;
    [SerializeField, Min(0f)] private float leapUpVelocity = 4f;
    [SerializeField] private LayerMask attackMask = ~0;

    public float ChargeTime => chargeTime;
    public float AttackRange => attackRange;
    public float AttackRadius => attackRadius;
    public float Damage => damage;
    public float DamageDelay => damageDelay;
    public float RagdollForce => ragdollForce;
    public float LeapForwardVelocity => leapForwardVelocity;
    public float LeapUpVelocity => leapUpVelocity;
    public LayerMask AttackMask => attackMask;
}
