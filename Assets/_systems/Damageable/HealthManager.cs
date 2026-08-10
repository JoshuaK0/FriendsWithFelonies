using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game;
using UnityEngine;

public sealed class HealthManager : NetworkBehaviour
{
	private const int NoHitboxIndex = 0;

	[Header("Health")]
	[SerializeField, Min(1f)]
	private float maxHealth = 100f;

	private readonly SyncVar<float> currentHealth = new();

	[Header("Team")]
	private readonly SyncVar<int> teamId =
		new(PlayerTeams.NoTeamId);

	[Header("Hitboxes")]
	[SerializeField]
	private List<Hitbox> hitboxes = new();

	[Header("Last Damage")]
	private readonly SyncVar<Vector3> lastDamageDirection = new();
	private readonly SyncVar<Vector3> lastHitPosition = new();
	private readonly SyncVar<float> lastDamageForce = new();

	private readonly SyncVar<int> lastDamagedHitboxIndex =
		new(NoHitboxIndex);

	public float MaxHealth =>
		maxHealth;

	public float CurrentHealth =>
		currentHealth.Value;

	public float NormalizedHealth =>
		maxHealth > 0f
			? currentHealth.Value / maxHealth
			: 0f;

	public bool IsDead =>
		currentHealth.Value <= 0f;

	public int TeamId =>
		teamId.Value;

	public Vector3 LastDamageDirection =>
		lastDamageDirection.Value;

	public Vector3 LastHitPosition =>
		lastHitPosition.Value;

	public float LastDamageForce =>
		lastDamageForce.Value;

	public int LastDamagedHitboxIndex =>
		lastDamagedHitboxIndex.Value;

	public Hitbox LastDamagedHitbox =>
		GetHitbox(lastDamagedHitboxIndex.Value);

	public event Action<float, float> OnHealthChanged;

	/// <summary>
	/// Negative changeAmount represents damage.
	/// Positive changeAmount represents healing.
	/// </summary>
	public event Action<
		float,
		Vector3,
		Vector3,
		MonoBehaviour> OnHealthModify;

	public event Action<
		float,
		Vector3,
		Vector3,
		float> OnDamaged;

	public event Action<int, int> OnTeamChanged;
	public event Action OnDied;

	private void Awake()
	{
		InitializeHitboxes();
	}

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		currentHealth.OnChange += HandleHealthChanged;
		teamId.OnChange += HandleTeamChanged;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		currentHealth.Value = maxHealth;

		lastDamageDirection.Value = Vector3.zero;
		lastHitPosition.Value = Vector3.zero;
		lastDamageForce.Value = 0f;
		lastDamagedHitboxIndex.Value = NoHitboxIndex;
	}

	public override void OnStopNetwork()
	{
		currentHealth.OnChange -= HandleHealthChanged;
		teamId.OnChange -= HandleTeamChanged;

		base.OnStopNetwork();
	}

	private void InitializeHitboxes()
	{
		for (int i = 0; i < hitboxes.Count; i++)
		{
			Hitbox hitbox = hitboxes[i];

			if (hitbox == null)
			{
				Debug.LogWarning(
					$"Hitbox list entry {i} is empty on {name}.",
					this);

				continue;
			}

			hitbox.Initialize(this, i);
		}
	}

	public Hitbox GetHitbox(int index)
	{
		if (!IsValidHitboxIndex(index))
			return null;

		return hitboxes[index];
	}

	private bool IsValidHitboxIndex(int index)
	{
		return index >= 0 &&
			   index < hitboxes.Count &&
			   hitboxes[index] != null;
	}

	[Server]
	public void SetTeam(int newTeamId)
	{
		if (teamId.Value == newTeamId)
			return;

		teamId.Value = newTeamId;
	}

	/// <summary>
	/// Deals damage without updating any stored damage information.
	///
	/// Preserves:
	/// - LastDamageDirection
	/// - LastHitPosition
	/// - LastDamageForce
	/// - LastDamagedHitbox
	/// </summary>
	public void TakeDamage(
		float amount,
		MonoBehaviour sourceComponent = null)
	{
		RequestDamage(
			amount,
			Vector3.zero,
			Vector3.zero,
			Vector3.zero,
			0f,
			NoHitboxIndex,
			sourceComponent,
			updateDamageInformation: false,
			updateDamagedHitbox: false);
	}

	/// <summary>
	/// Deals damage and updates direction, position, and force.
	///
	/// Preserves the previously stored damaged hitbox.
	/// </summary>
	public void TakeDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		MonoBehaviour sourceComponent)
	{
		RequestDamage(
			amount,
			damageDirection,
			sourcePos,
			targetPos,
			force,
			NoHitboxIndex,
			sourceComponent,
			updateDamageInformation: true,
			updateDamagedHitbox: false);
	}

	/// <summary>
	/// Deals damage and updates all stored damage information,
	/// including the last damaged hitbox.
	/// </summary>
	public void TakeDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		int hitboxIndex,
		MonoBehaviour sourceComponent)
	{
		RequestDamage(
			amount,
			damageDirection,
			sourcePos,
			targetPos,
			force,
			hitboxIndex,
			sourceComponent,
			updateDamageInformation: true,
			updateDamagedHitbox: true);
	}

	private void RequestDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		int hitboxIndex,
		MonoBehaviour sourceComponent,
		bool updateDamageInformation,
		bool updateDamagedHitbox)
	{
		if (amount <= 0f || IsDead || !IsSpawned)
			return;

		if (updateDamageInformation)
		{
			damageDirection =
				damageDirection.sqrMagnitude > 0f
					? damageDirection.normalized
					: Vector3.zero;

			force = Mathf.Max(0f, force);
		}
		else
		{
			damageDirection = Vector3.zero;
			sourcePos = Vector3.zero;
			targetPos = Vector3.zero;
			force = 0f;
		}

		if (!updateDamagedHitbox)
			hitboxIndex = NoHitboxIndex;

		NetworkBehaviour networkSource =
			sourceComponent as NetworkBehaviour;

		if (IsServerInitialized)
		{
			ApplyDamage(
				amount,
				damageDirection,
				sourcePos,
				targetPos,
				force,
				hitboxIndex,
				networkSource,
				updateDamageInformation,
				updateDamagedHitbox);

			return;
		}

		TakeDamageServerRpc(
			amount,
			damageDirection,
			sourcePos,
			targetPos,
			force,
			hitboxIndex,
			networkSource,
			updateDamageInformation,
			updateDamagedHitbox);
	}

	[ServerRpc(RequireOwnership = false)]
	private void TakeDamageServerRpc(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		int hitboxIndex,
		NetworkBehaviour sourceComponent,
		bool updateDamageInformation,
		bool updateDamagedHitbox)
	{
		ApplyDamage(
			amount,
			damageDirection,
			sourcePos,
			targetPos,
			force,
			hitboxIndex,
			sourceComponent,
			updateDamageInformation,
			updateDamagedHitbox);
	}

	[Server]
	private void ApplyDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		int hitboxIndex,
		NetworkBehaviour sourceComponent,
		bool updateDamageInformation,
		bool updateDamagedHitbox)
	{
		if (amount <= 0f || IsDead)
			return;

		float previousHealth =
			currentHealth.Value;

		if (updateDamageInformation)
		{
			lastDamageDirection.Value =
				damageDirection.sqrMagnitude > 0f
					? damageDirection.normalized
					: Vector3.zero;

			lastHitPosition.Value =
				targetPos;

			lastDamageForce.Value =
				Mathf.Max(0f, force);
		}

		if (updateDamagedHitbox)
		{
			lastDamagedHitboxIndex.Value =
				IsValidHitboxIndex(hitboxIndex)
					? hitboxIndex
					: NoHitboxIndex;
		}

		currentHealth.Value = Mathf.Clamp(
			previousHealth - amount,
			0f,
			maxHealth);

		float appliedDamage =
			previousHealth - currentHealth.Value;

		if (appliedDamage <= 0f)
			return;

		Vector3 eventSourcePosition =
			updateDamageInformation
				? sourcePos
				: transform.position;

		Vector3 eventTargetPosition =
			updateDamageInformation
				? targetPos
				: transform.position;

		Vector3 eventDamageDirection =
			updateDamageInformation
				? lastDamageDirection.Value
				: Vector3.zero;

		float eventDamageForce =
			updateDamageInformation
				? lastDamageForce.Value
				: 0f;

		NotifyHealthModifiedObserversRpc(
			-appliedDamage,
			eventSourcePosition,
			eventTargetPosition,
			sourceComponent,
			eventDamageDirection,
			eventDamageForce);
	}

	public void Heal(float amount)
	{
		Heal(
			amount,
			transform.position,
			transform.position,
			this);
	}

	public void Heal(
		float amount,
		Vector3 sourcePos,
		Vector3 targetPos,
		MonoBehaviour sourceComponent)
	{
		if (amount <= 0f || IsDead || !IsSpawned)
			return;

		NetworkBehaviour networkSource =
			sourceComponent as NetworkBehaviour;

		if (IsServerInitialized)
		{
			ApplyHealing(
				amount,
				sourcePos,
				targetPos,
				networkSource);

			return;
		}

		HealServerRpc(
			amount,
			sourcePos,
			targetPos,
			networkSource);
	}

	[ServerRpc(RequireOwnership = false)]
	private void HealServerRpc(
		float amount,
		Vector3 sourcePos,
		Vector3 targetPos,
		NetworkBehaviour sourceComponent)
	{
		ApplyHealing(
			amount,
			sourcePos,
			targetPos,
			sourceComponent);
	}

	[Server]
	private void ApplyHealing(
		float amount,
		Vector3 sourcePos,
		Vector3 targetPos,
		NetworkBehaviour sourceComponent)
	{
		if (amount <= 0f || IsDead)
			return;

		float previousHealth =
			currentHealth.Value;

		currentHealth.Value = Mathf.Clamp(
			previousHealth + amount,
			0f,
			maxHealth);

		float appliedHealing =
			currentHealth.Value - previousHealth;

		if (appliedHealing <= 0f)
			return;

		NotifyHealthModifiedObserversRpc(
			appliedHealing,
			sourcePos,
			targetPos,
			sourceComponent,
			Vector3.zero,
			0f);
	}

	[ObserversRpc(RunLocally = true)]
	private void NotifyHealthModifiedObserversRpc(
		float changeAmount,
		Vector3 sourcePos,
		Vector3 targetPos,
		NetworkBehaviour sourceComponent,
		Vector3 damageDirection,
		float force)
	{
		OnHealthModify?.Invoke(
			changeAmount,
			sourcePos,
			targetPos,
			sourceComponent);

		if (changeAmount >= 0f)
			return;

		OnDamaged?.Invoke(
			-changeAmount,
			damageDirection,
			targetPos,
			force);
	}

	private void HandleTeamChanged(
		int previousTeamId,
		int newTeamId,
		bool asServer)
	{
		// Prevent duplicate host events.
		if (asServer && IsClientInitialized)
			return;

		OnTeamChanged?.Invoke(
			previousTeamId,
			newTeamId);
	}

	private void HandleHealthChanged(
		float previousHealth,
		float newHealth,
		bool asServer)
	{
		// Prevent duplicate host events.
		if (asServer && IsClientInitialized)
			return;

		OnHealthChanged?.Invoke(
			newHealth,
			maxHealth);

		if (previousHealth > 0f &&
			newHealth <= 0f)
		{
			OnDied?.Invoke();
		}
	}

	[Server]
	public void SetHealth(float newHealth)
	{
		currentHealth.Value = Mathf.Clamp(
			newHealth,
			0f,
			maxHealth);
	}
}