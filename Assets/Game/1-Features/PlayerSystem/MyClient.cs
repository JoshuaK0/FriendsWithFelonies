using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game;
using Game.PopupSystem;
using Game.Utils;
using Steamworks;
using UnityEngine;
using UnityEngine.Serialization;

public struct PlayerInfoData
{
	public string Username;
	public ulong SteamID;

	public PlayerInfoData(string username, ulong steamID)
	{
		Username = username;
		SteamID = steamID;
	}
}

public class MyClient : BaseNetworkBehaviour
{
	public static MyClient Instance;

	public static Action<MyClient> OnStartClient;
	public static Action<bool> OnIsReady;
	public static Action<MyClient> C_OnSetPosition;

	public readonly SyncVar<PlayerInfoData> PlayerInfo =
		new SyncVar<PlayerInfoData>();

	public readonly SyncVar<bool> IsReady =
		new SyncVar<bool>();

	private readonly SyncVar<NetworkObject> playerManagerObject =
		new SyncVar<NetworkObject>();

	[Header("Team")]
	[SerializeField]
	private int teamId = PlayerTeams.NoTeamId;

	[SerializeField]
	private TeamType teamType = TeamType.Spectator;

	[Header("Player Manager")]
	[Tooltip(
		"A registered FishNet spawnable prefab containing " +
		"PlayerManager and NetworkObject.")]
	[SerializeField]
	private PlayerManager playerManagerPrefab;

	[FormerlySerializedAs("mesh")]
	[FormerlySerializedAs("contoller")]
	[Header("Controller")]
	[SerializeField]
	private GameObject controller;

	[SerializeField]
	private Behaviour[] componentsToEnable;

	private CharacterController characterController;

	public int TeamId => teamId;
	public TeamType CurrentTeamType => teamType;

	/// <summary>
	/// The synchronized PlayerManager NetworkObject belonging to this client.
	/// Every observing client can resolve this reference.
	/// </summary>
	public NetworkObject PlayerManagerNetworkObject =>
		playerManagerObject.Value;

	/// <summary>
	/// The synchronized PlayerManager belonging to this client.
	/// </summary>
	public PlayerManager PlayerManager
	{
		get
		{
			NetworkObject managerObject =
				playerManagerObject.Value;

			if (managerObject == null)
				return null;

			return managerObject.GetComponent<PlayerManager>();
		}
	}

	public event Action<PlayerManager> OnPlayerManagerChanged;

	public override void OnStartServer()
	{
		base.OnStartServer();

		SpawnPlayerManagerServer();
	}

	public override void OnStopServer()
	{
		DespawnPlayerManagerServer();

		base.OnStopServer();
	}

	protected override void RegisterEvents()
	{
		PlayerConnectionManager.Instance.AllClients.Add(this);

		PlayerInfo.OnChange += OnPlayerDataChange;
		IsReady.OnChange += OnIsReadyChange;
		playerManagerObject.OnChange += OnPlayerManagerObjectChange;

		PlayerTeams.OnTeamDataChanged += RefreshTeamInformation;

		characterController = GetComponent<CharacterController>();

		RefreshTeamInformation();

		if (IsOwner)
		{
			Instance = this;

			OnStartClient?.Invoke(this);
			PopupManager.Popup_Close();

			Cmd_UpdatePlayerInfo(
				SteamClient.SteamId,
				SteamClient.Name
			);
		}
	}

	protected override void UnregisterEvents()
	{
		PlayerConnectionManager.Instance.AllClients.Remove(this);

		PlayerInfo.OnChange -= OnPlayerDataChange;
		IsReady.OnChange -= OnIsReadyChange;
		playerManagerObject.OnChange -= OnPlayerManagerObjectChange;

		PlayerTeams.OnTeamDataChanged -= RefreshTeamInformation;

		if (Instance == this)
		{
			Instance = null;
			OnStartClient?.Invoke(null);
		}
	}

	#region Player Manager

	[Server]
	private void SpawnPlayerManagerServer()
	{
		if (playerManagerObject.Value != null)
			return;

		if (playerManagerPrefab == null)
		{
			Debug.LogError(
				$"{nameof(MyClient)} on '{name}' has no " +
				$"{nameof(PlayerManager)} prefab assigned.",
				this);

			return;
		}

		PlayerManager manager = Instantiate(playerManagerPrefab);

		if (!manager.TryGetComponent(
				out NetworkObject managerNetworkObject))
		{
			Debug.LogError(
				$"The {nameof(PlayerManager)} prefab requires a " +
				$"{nameof(NetworkObject)} component.",
				manager);

			Destroy(manager.gameObject);
			return;
		}

		InstanceFinder.ServerManager.Spawn(
			manager.gameObject,
			Owner);

		// Assign after spawning so observers can resolve the reference.
		playerManagerObject.Value = managerNetworkObject;
	}

	[Server]
	private void DespawnPlayerManagerServer()
	{
		NetworkObject managerObject =
			playerManagerObject.Value;

		playerManagerObject.Value = null;

		if (managerObject == null)
			return;

		if (managerObject.IsSpawned)
		{
			InstanceFinder.ServerManager.Despawn(
				managerObject.gameObject);
		}
		else
		{
			Destroy(managerObject.gameObject);
		}
	}

	private void OnPlayerManagerObjectChange(
		NetworkObject previous,
		NetworkObject current,
		bool asServer)
	{
		PlayerManager manager =
			current != null
				? current.GetComponent<PlayerManager>()
				: null;

		OnPlayerManagerChanged?.Invoke(manager);
	}

	#endregion

	#region Team

	public void RefreshTeamInformation()
	{
		if (PlayerTeams.Instance == null)
		{
			teamId = PlayerTeams.NoTeamId;
			teamType = TeamType.Spectator;
			return;
		}

		int connectionId = Owner.ClientId;

		teamId =
			PlayerTeams.Instance.GetPlayerTeamId(connectionId);

		teamType =
			PlayerTeams.Instance.GetPlayerTeamType(connectionId);
	}

	public bool IsInTeam(int targetTeamId)
	{
		return teamId == targetTeamId;
	}

	#endregion

	#region Controller

	[ObserversRpc]
	public void Rpc_ToggleController(bool value)
	{
		if (!IsOwner)
			return;

		Cursor.lockState = CursorLockMode.Locked;

		if (controller != null)
			controller.SetActive(value);

		foreach (Behaviour component in componentsToEnable)
		{
			if (component != null)
				component.enabled = value;
		}
	}

	[Server]
	public void S_SetPosition(
		Vector3 position,
		Quaternion rotation,
		bool toggleController)
	{
		TRpc_SetPosition(Owner, position, rotation);

		if (toggleController)
			Rpc_ToggleController(true);
	}

	[TargetRpc]
	private void TRpc_SetPosition(
		NetworkConnection connection,
		Vector3 position,
		Quaternion rotation)
	{
		if (characterController != null)
			characterController.enabled = false;

		transform.position = position;
		transform.rotation = rotation;

		if (characterController != null)
			characterController.enabled = true;

		C_OnSetPosition?.Invoke(this);
	}

	#endregion

	#region SyncVar Hooks

	private void OnPlayerDataChange(
		PlayerInfoData previous,
		PlayerInfoData current,
		bool asServer)
	{
	}

	[ServerRpc]
	private void Cmd_UpdatePlayerInfo(
		ulong steamId,
		string username)
	{
		PlayerInfo.Value = new PlayerInfoData(
			username,
			steamId
		);
	}

	private void OnIsReadyChange(
		bool previous,
		bool value,
		bool asServer)
	{
		if (IsOwner)
			OnIsReady?.Invoke(value);
	}

	[ServerRpc(RequireOwnership = false)]
	public void Cmd_ReadyUp()
	{
		IsReady.Value = !IsReady.Value;
	}

	#endregion
}
