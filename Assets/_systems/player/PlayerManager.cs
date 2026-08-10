using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// A per-client networked manager.
/// This object is spawned and owned by the same connection as its MyClient.
/// </summary>
public class PlayerManager : NetworkBehaviour
{
	private static readonly Dictionary<int, PlayerManager>
		activeManagers = new();

	[Header("Player Controller")]
	[SerializeField]
	private GameObject playerAliveControllerPrefab;

	[SerializeField]
	private GameObject playerDeadSpectatePrefab;

	[Header("Reviving")]
	[SerializeField, Min(0f)]
	private float revivedHealth = 50f;

	private readonly SyncVar<PlayerState> playerState =
		new(PlayerState.Unspawned);

	private readonly SyncVar<NetworkObject> playerControllerObject =
		new();

	private GameObject playerDeadSpectateInstance;

	/// <summary>
	/// Convenience access to the local client's synchronized manager.
	/// </summary>
	public static PlayerManager Instance =>
		MyClient.Instance != null
			? MyClient.Instance.PlayerManager
			: null;

	public static bool TryGetPlayerManager(
		int playerId,
		out PlayerManager playerManager)
	{
		return activeManagers.TryGetValue(
			playerId,
			out playerManager);
	}

	/// <summary>
	/// The connection ID of the client that owns this manager.
	/// </summary>
	public int PlayerId => OwnerId;

	// Compatibility aliases for existing code.
	public int LocalPlayerId => PlayerId;
	public PlayerState LocalPlayerState => playerState.Value;

	public PlayerState State => playerState.Value;

	/// <summary>
	/// The synchronized spawned controller belonging to this manager's owner.
	/// Every observing client can resolve this reference.
	/// </summary>
	public NetworkObject PlayerControllerNetworkObject =>
		playerControllerObject.Value;

	// Compatibility alias for existing code.
	public NetworkObject LocalPlayerControllerNetworkObject =>
		PlayerControllerNetworkObject;

	public GameObject PlayerController
	{
		get
		{
			NetworkObject controllerObject =
				playerControllerObject.Value;

			return controllerObject != null
				? controllerObject.gameObject
				: null;
		}
	}

	// Compatibility alias for existing code.
	public GameObject LocalPlayerController =>
		PlayerController;

	public event Action<GameObject> OnPlayerControllerChanged;

	/// <summary>
	/// Fired on the owning client when its local controller changes.
	/// Can receive null when the controller is removed.
	/// </summary>
	public event Action<GameObject> OnLocalPlayerControllerChanged;

	/// <summary>
	/// Fired only on the owning client when its player character is spawned.
	/// Never fires with null.
	/// </summary>
	public event Action<GameObject> OnLocalPlayerSpawned;

	public event Action<PlayerState, PlayerState>
		OnPlayerStateChanged;

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		activeManagers[PlayerId] = this;

		playerControllerObject.OnChange +=
			HandlePlayerControllerChanged;

		playerState.OnChange +=
			HandlePlayerStateChanged;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (!IsOwner)
			return;

		UpdateDeadSpectate(playerState.Value);
	}

	public override void OnStopClient()
	{
		if (IsOwner &&
			playerDeadSpectateInstance != null)
		{
			Destroy(playerDeadSpectateInstance);
			playerDeadSpectateInstance = null;
		}

		base.OnStopClient();
	}

	public override void OnStopNetwork()
	{
		if (activeManagers.TryGetValue(
				PlayerId,
				out PlayerManager registeredManager) &&
			registeredManager == this)
		{
			activeManagers.Remove(PlayerId);
		}

		playerControllerObject.OnChange -=
			HandlePlayerControllerChanged;

		playerState.OnChange -=
			HandlePlayerStateChanged;

		base.OnStopNetwork();
	}

	public override void OnStopServer()
	{
		DespawnPlayerControllerServer();

		base.OnStopServer();
	}

	public PlayerState GetPlayerState(int playerId)
	{
		if (TryGetPlayerManager(
				playerId,
				out PlayerManager playerManager))
		{
			return playerManager.State;
		}

		return PlayerState.Unspawned;
	}

	#region Spawn

	[Client]
	public void SpawnPlayer()
	{
		if (!IsOwner)
			return;

		SpawnPlayerServerRpc();
	}

	[ServerRpc]
	private void SpawnPlayerServerRpc()
	{
		SpawnPlayerServer();
	}

	[Server]
	public void SpawnPlayerServer()
	{
		if (!TryGetSpawnRequirements(
				out NetworkConnection playerConnection))
		{
			return;
		}

		if (PlayerTeams.Instance == null)
		{
			Debug.LogError(
				$"{nameof(PlayerTeams)} instance was not found.",
				this);

			return;
		}

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(PlayerId);

		Transform spawnPoint =
			MapManager.Instance.GetRandomSpawnPoint(teamType);

		if (spawnPoint == null)
		{
			Debug.LogWarning(
				$"No spawn point found for player {PlayerId}.",
				this);

			return;
		}

		if (!SpawnControllerServer(
				playerConnection,
				spawnPoint))
		{
			return;
		}

		Debug.Log(
			$"Spawned player controller for client {PlayerId}.",
			this);
	}

	/// <summary>
	/// Routes the call to the PlayerManager owned by the supplied connection.
	/// </summary>
	[Server]
	public void SpawnPlayerServer(
		NetworkConnection playerConnection)
	{
		PlayerManager playerManager =
			ResolvePlayerManager(playerConnection);

		if (playerManager != null)
			playerManager.SpawnPlayerServer();
	}

	#endregion

	#region Revive

	/// <summary>
	/// Requests that the local player be revived at the jail spawn point.
	/// </summary>
	[Client]
	public void RevivePlayer()
	{
		if (!IsOwner)
			return;

		RevivePlayerServerRpc();
	}

	[ServerRpc]
	private void RevivePlayerServerRpc()
	{
		RevivePlayerServer();
	}

	/// <summary>
	/// Revives this manager's player at the jail spawn point.
	/// </summary>
	[Server]
	public void RevivePlayerServer()
	{
		if (!TryGetSpawnRequirements(
				out NetworkConnection playerConnection))
		{
			return;
		}

		Transform jailSpawnPoint =
			MapManager.Instance.GetJailSpawnPoint();

		if (jailSpawnPoint == null)
		{
			Debug.LogWarning(
				$"No jail spawn point found for player {PlayerId}.",
				this);

			return;
		}

		if (!SpawnControllerServer(
				playerConnection,
				jailSpawnPoint,
				revivedHealth))
		{
			return;
		}

		Debug.Log(
			$"Revived player {PlayerId} with " +
			$"{revivedHealth} health.",
			this);
	}

	/// <summary>
	/// Routes the revive call to the PlayerManager owned by the
	/// supplied connection.
	/// </summary>
	[Server]
	public void RevivePlayerServer(
		NetworkConnection playerConnection)
	{
		PlayerManager playerManager =
			ResolvePlayerManager(playerConnection);

		if (playerManager != null)
			playerManager.RevivePlayerServer();
	}

	#endregion

	#region Kill

	[Client]
	public void KillPlayer()
	{
		if (!IsOwner)
			return;

		KillPlayerServerRpc();
	}

	[ServerRpc]
	private void KillPlayerServerRpc()
	{
		KillPlayerServer();
	}
	[Server]
	public void KillPlayerServer()
	{
		if (playerState.Value == PlayerState.Dead)
			return;

		playerState.Value = PlayerState.Dead;
		playerControllerObject.Value = null;

		RobberCaptureManager.Instance?.ReportPlayerKilled(this);

		// DespawnPlayerControllerServer();
	}

	[Server]
	public void KillPlayerServer(
		NetworkConnection playerConnection)
	{
		PlayerManager playerManager =
			ResolvePlayerManager(playerConnection);

		if (playerManager != null)
			playerManager.KillPlayerServer();
	}

	#endregion

	#region Set unspawned

	[Client]
	public void SetUnspawned()
	{
		if (!IsOwner)
			return;

		SetUnspawnedServerRpc();
	}

	[ServerRpc]
	private void SetUnspawnedServerRpc()
	{
		SetUnspawnedServer();
	}

	[Server]
	public void SetUnspawnedServer()
	{
		playerState.Value = PlayerState.Unspawned;

		DespawnPlayerControllerServer();
	}

	[Server]
	public void SetUnspawnedServer(
		NetworkConnection playerConnection)
	{
		PlayerManager playerManager =
			ResolvePlayerManager(playerConnection);

		if (playerManager != null)
			playerManager.SetUnspawnedServer();
	}

	#endregion

	#region Controller spawning

	[Server]
	private bool TryGetSpawnRequirements(
		out NetworkConnection playerConnection)
	{
		playerConnection = Owner;

		if (playerConnection == null)
		{
			Debug.LogWarning(
				$"{nameof(PlayerManager)} has no owning connection.",
				this);

			return false;
		}

		if (playerAliveControllerPrefab == null)
		{
			Debug.LogError(
				$"{nameof(PlayerManager)} has no player " +
				"controller prefab.",
				this);

			return false;
		}

		if (MapManager.Instance == null)
		{
			Debug.LogError(
				$"{nameof(MapManager)} instance was not found.",
				this);

			return false;
		}

		return true;
	}

	/// <summary>
	/// Spawns the player's controller.
	/// When startingHealth is supplied, it is applied after FishNet has
	/// initialized the spawned object's HealthManager.
	/// </summary>
	[Server]
	private bool SpawnControllerServer(
		NetworkConnection playerConnection,
		Transform spawnPoint,
		float? startingHealth = null)
	{
		if (playerConnection == null || spawnPoint == null)
			return false;

		// Despawn the existing player before spawning a replacement.
		if (playerControllerObject.Value != null)
			DespawnPlayerControllerServer();

		GameObject controller = Instantiate(
			playerAliveControllerPrefab,
			spawnPoint.position,
			spawnPoint.rotation);

		if (!controller.TryGetComponent(
				out NetworkObject controllerNetworkObject))
		{
			Debug.LogError(
				"The player controller prefab requires a " +
				$"{nameof(NetworkObject)} component.",
				controller);

			Destroy(controller);
			return false;
		}

		InstanceFinder.ServerManager.Spawn(
			controller,
			playerConnection);

		playerControllerObject.Value =
			controllerNetworkObject;

		playerState.Value = PlayerState.Alive;

		return true;
	}

	#endregion

	#region Synchronized controller

	[Server]
	private void DespawnPlayerControllerServer()
	{
		NetworkObject controllerObject =
			playerControllerObject.Value;

		// Clear the synchronized reference before despawning.
		playerControllerObject.Value = null;

		if (controllerObject == null)
			return;

		if (controllerObject.IsSpawned)
		{
			InstanceFinder.ServerManager.Despawn(
				controllerObject.gameObject);
		}
		else
		{
			Destroy(controllerObject.gameObject);
		}
	}

	private void HandlePlayerControllerChanged(
		NetworkObject previous,
		NetworkObject current,
		bool asServer)
	{
		GameObject controller =
			current != null
				? current.gameObject
				: null;

		// All observers may listen to this.
		OnPlayerControllerChanged?.Invoke(controller);

		// Local events only fire on the owning client's side.
		if (asServer || !IsOwner)
			return;

		OnLocalPlayerControllerChanged?.Invoke(controller);

		if (current != null)
			OnLocalPlayerSpawned?.Invoke(controller);
	}

	private void HandlePlayerStateChanged(
		PlayerState previous,
		PlayerState current,
		bool asServer)
	{
		OnPlayerStateChanged?.Invoke(previous, current);

		if (asServer || !IsOwner)
			return;

		UpdateDeadSpectate(current);
	}

	#endregion

	#region Dead spectate

	private void UpdateDeadSpectate(PlayerState state)
	{
		bool shouldBeActive =
			state == PlayerState.Dead;

		if (shouldBeActive)
		{
			if (playerDeadSpectateInstance == null)
			{
				if (playerDeadSpectatePrefab == null)
				{
					Debug.LogWarning(
						$"{nameof(PlayerManager)} has no dead " +
						"spectate prefab.",
						this);

					return;
				}

				playerDeadSpectateInstance =
					Instantiate(playerDeadSpectatePrefab);
			}

			playerDeadSpectateInstance.SetActive(true);
		}
		else
		{
			if (playerDeadSpectateInstance != null)
				playerDeadSpectateInstance.SetActive(false);
		}
	}

	#endregion

	#region Player manager resolution

	private PlayerManager ResolvePlayerManager(
		NetworkConnection connection)
	{
		if (connection == null)
			return null;

		if (TryGetPlayerManager(
				connection.ClientId,
				out PlayerManager playerManager))
		{
			return playerManager;
		}

		Debug.LogWarning(
			$"No PlayerManager was found for connection " +
			$"{connection.ClientId}.",
			this);

		return null;
	}

	#endregion
}