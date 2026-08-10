using FishNet.Object;
using UnityEngine;

/// <summary>
/// Marks one networked loot object that can be captured.
///
/// This simplified version assumes the LootCaptureTarget and its single
/// collider are on the same GameObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class LootCaptureTarget : NetworkBehaviour
{
	public bool IsCaptured { get; private set; }

	/// <summary>
	/// Despawns this loot from the network.
	/// May only be called by the server.
	/// </summary>
	[Server]
	public bool CaptureServer()
	{
		if (IsCaptured || !IsSpawned)
			return false;

		IsCaptured = true;
		base.Despawn();
		return true;
	}
}
