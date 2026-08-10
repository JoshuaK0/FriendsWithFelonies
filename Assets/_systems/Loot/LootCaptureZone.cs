using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// Server-authoritative zone that tracks loot inside a BoxCollider.
///
/// Each LootCaptureTarget is assumed to have exactly one collider, with the
/// collider and LootCaptureTarget component on the same GameObject.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class LootCaptureZone : NetworkBehaviour
{
	[SerializeField]
	private BoxCollider zoneCollider;

	[SerializeField]
	private LayerMask lootLayers = ~0;

	private readonly HashSet<LootCaptureTarget> lootInside = new();
	private readonly List<LootCaptureTarget> captureBuffer = new();

	/// <summary>
	/// Raised only on the server when the tracked loot count changes.
	/// </summary>
	public event Action<int> OnServerLootCountChanged;

	public int ServerLootCount => lootInside.Count;

	private void Awake()
	{
		if (zoneCollider == null)
			zoneCollider = GetComponent<BoxCollider>();

		zoneCollider.isTrigger = true;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		// Detect loot that already exists inside the zone when it starts.
		RefreshContentsServer();
	}

	public override void OnStopServer()
	{
		lootInside.Clear();
		captureBuffer.Clear();

		base.OnStopServer();
	}

	private void OnTriggerEnter(Collider other)
	{
		TryAddLootServer(other);
	}

	private void OnTriggerStay(Collider other)
	{
		// Handles loot which is spawned or enabled while already inside the
		// trigger volume.
		TryAddLootServer(other);
	}

	private void OnTriggerExit(Collider other)
	{
		if (!IsServerStarted)
			return;

		if (!other.TryGetComponent(out LootCaptureTarget loot))
			return;

		if (lootInside.Remove(loot))
			OnServerLootCountChanged?.Invoke(lootInside.Count);
	}

	/// <summary>
	/// Rebuilds the tracked set from the server's physics world.
	/// This is called again immediately before capture, so the client never
	/// supplies or controls the captured loot count.
	/// </summary>
	[Server]
	public void RefreshContentsServer()
	{
		int previousCount = lootInside.Count;
		lootInside.Clear();

		if (zoneCollider == null || !zoneCollider.enabled)
		{
			NotifyCountChanged(previousCount);
			return;
		}

		Vector3 center =
			zoneCollider.transform.TransformPoint(zoneCollider.center);

		Vector3 scale = zoneCollider.transform.lossyScale;
		Vector3 absoluteScale = new(
			Mathf.Abs(scale.x),
			Mathf.Abs(scale.y),
			Mathf.Abs(scale.z));

		Vector3 halfExtents = Vector3.Scale(
			zoneCollider.size * 0.5f,
			absoluteScale);

		Collider[] overlaps = Physics.OverlapBox(
			center,
			halfExtents,
			zoneCollider.transform.rotation,
			lootLayers,
			QueryTriggerInteraction.Collide);

		foreach (Collider overlap in overlaps)
		{
			if (overlap == null || overlap == zoneCollider)
				continue;

			if (!overlap.TryGetComponent(out LootCaptureTarget loot))
				continue;

			if (IsValidLoot(loot))
				lootInside.Add(loot);
		}

		NotifyCountChanged(previousCount);
	}

	/// <summary>
	/// Claims the LootStolen result using the authoritative number of loot
	/// objects currently inside this zone. The loot is only despawned if the
	/// round-end request succeeds.
	/// </summary>
	[Server]
	public bool TryCaptureAllLootServer(out int capturedLootCount)
	{
		capturedLootCount = 0;
		RefreshContentsServer();

		captureBuffer.Clear();

		foreach (LootCaptureTarget loot in lootInside)
		{
			if (IsValidLoot(loot))
				captureBuffer.Add(loot);
		}

		if (captureBuffer.Count == 0)
			return false;

		if (GameFlowManager.Instance == null)
		{
			Debug.LogError(
				"LootCaptureZone requires GameFlowManager.Instance.");
			return false;
		}

		int authoritativeLootCount = captureBuffer.Count;

		// This count becomes the score multiplier. The round flow rejects this
		// call if setup is active, the round is over, or another condition won.
		if (!GameFlowManager.Instance.ReportLootStolen(
				authoritativeLootCount))
		{
			return false;
		}

		foreach (LootCaptureTarget loot in captureBuffer)
		{
			if (IsValidLoot(loot))
				loot.CaptureServer();
		}

		capturedLootCount = authoritativeLootCount;
		captureBuffer.Clear();
		lootInside.Clear();
		OnServerLootCountChanged?.Invoke(0);

		return true;
	}

	[Server]
	public bool HasLootServer()
	{
		RefreshContentsServer();
		return lootInside.Count > 0;
	}

	private void TryAddLootServer(Collider other)
	{
		if (!IsServerStarted || other == null)
			return;

		if (!other.TryGetComponent(out LootCaptureTarget loot))
			return;

		if (!IsValidLoot(loot))
			return;

		if (lootInside.Add(loot))
			OnServerLootCountChanged?.Invoke(lootInside.Count);
	}

	private void NotifyCountChanged(int previousCount)
	{
		if (previousCount != lootInside.Count)
			OnServerLootCountChanged?.Invoke(lootInside.Count);
	}

	private static bool IsValidLoot(LootCaptureTarget loot)
	{
		return loot != null &&
		       loot.IsSpawned &&
		       !loot.IsCaptured &&
		       loot.gameObject.activeInHierarchy;
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (zoneCollider == null)
			zoneCollider = GetComponent<BoxCollider>();

		if (zoneCollider != null)
			zoneCollider.isTrigger = true;
	}
#endif
}
