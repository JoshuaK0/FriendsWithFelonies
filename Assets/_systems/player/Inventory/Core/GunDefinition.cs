using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Items/Gun Definition")]
public sealed class GunDefinition : ItemDefinition
{
    [Header("Firing")]
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(1f)] private float roundsPerMinute = 350f;
    [SerializeField, Min(0f)] private float range = 200f;
    [SerializeField, Min(1)] private int pelletsPerShot = 1;
    [SerializeField, Min(0f)] private float spreadDegrees = 0.5f;
    [SerializeField] private bool automatic;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Magazine")]
    [SerializeField, Min(1)] private int magazineSize = 30;
    [SerializeField, Min(0f)] private float reloadTime = 1.5f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float ragdollForce = 100f;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private GameObject environmentHitFx;
    [SerializeField] private GameObject bodyHitFx;

    public float Damage => damage;
    public float RoundsPerMinute => roundsPerMinute;
    public float Range => range;
    public int PelletsPerShot => pelletsPerShot;
    public float SpreadDegrees => spreadDegrees;
    public bool Automatic => automatic;
    public LayerMask HitMask => hitMask;
    public int MagazineSize => magazineSize;
    public float ReloadTime => reloadTime;
    public float RagdollForce => ragdollForce;
    public AudioClip FireSound => fireSound;
    public GameObject EnvironmentHitFx => environmentHitFx;
    public GameObject BodyHitFx => bodyHitFx;
}
