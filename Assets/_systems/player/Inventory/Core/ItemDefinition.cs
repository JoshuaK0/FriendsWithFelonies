using FishNet.Object;
using UnityEngine;

[CreateAssetMenu(
	menuName = "Inventory/Items/Item Definition",
	fileName = "New Item")]
public class ItemDefinition : ScriptableObject
{
	[Header("Identity")]
	[SerializeField] private string lookupName;
	[SerializeField] private string displayName;

	[Header("Inventory")]
	[SerializeField, Min(1)] private int maxStack = 1;

	[Tooltip("If true, the item is removed from its hotbar slot when its count reaches zero.")]
	[SerializeField] private bool consumeOnEmpty = true;

	[Tooltip("If false, the player cannot drop this item or replace it through a full-hotbar pickup swap.")]
	[SerializeField] private bool isDroppable = true;

	[Header("Shop")]
	[SerializeField] private bool isPurchasable = true;

	[SerializeField, Min(0)]
	private int cost = 100;

	[Tooltip("How many of this item the player receives when purchasing it.")]
	[SerializeField, Min(1)]
	private int purchaseAmount = 1;

	[Header("Prefabs")]
	[SerializeField] private NetworkObject worldPrefab;
	[SerializeField] private GameObject heldPrefab;

	[Tooltip(
		"Presentation-only prefab shown on remote players. " +
		"Do not put input scripts on this prefab.")]
	[SerializeField] private GameObject remoteHeldPrefab;

	public string LookupName => lookupName;

	public string DisplayName =>
		string.IsNullOrWhiteSpace(displayName)
			? lookupName
			: displayName;

	public int MaxStack =>
		Mathf.Max(1, maxStack);

	public bool ConsumeOnEmpty =>
		consumeOnEmpty;

	public bool IsDroppable =>
		isDroppable;

	public bool IsPurchasable =>
		isPurchasable;

	public int Cost =>
		Mathf.Max(0, cost);

	public int PurchaseAmount =>
		Mathf.Max(1, purchaseAmount);

	public NetworkObject WorldPrefab =>
		worldPrefab;

	public GameObject HeldPrefab =>
		heldPrefab;

	public GameObject RemoteHeldPrefab =>
		remoteHeldPrefab;
}