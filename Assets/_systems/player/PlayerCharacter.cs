using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCharacter : NetworkBehaviour
{
	[Header("Owner Objects")]
	[SerializeField]
	private List<GameObject> ownerEnable = new();

	[SerializeField]
	private List<GameObject> ownerDisable = new();

	[SerializeField]
	private List<GameObject> notOwnerDisable = new();
	
	[SerializeField]
	private List<GameObject> deadDisable = new();

	[Header("Components")]
	[SerializeField]
	private PlayerMovement playerMovement;

	[SerializeField]
	private CharacterAnimator characterAnimator;

	[SerializeField]
	private MouseLook mouseLook;

	[SerializeField]
	private HealthManager healthManager;

	[SerializeField]
	private CharControllerServiceLocator charControllerServiceLocator;

	[SerializeField] NetworkRagdollManager ragdollManager;

	[SerializeField] NetHotbarInventory inventory;
	[SerializeField] GameObject inventoryObject;

	[Header("PostProcessing")]
	[SerializeField] Slider NVGSlider;
	[SerializeField] GameObject NVGFX;

	[SerializeField] Slider ThermalGogglesSlider;
	[SerializeField] GameObject ThermalGogglesFX;

	private readonly SyncVar<int> teamId =
		new(PlayerTeams.NoTeamId);

	private readonly SyncVar<TeamType> teamType =
		new(TeamType.Spectator);

	public int TeamId => teamId.Value;

	public TeamType CurrentTeamType =>
		teamType.Value;

	private MyClient owningClient;

	private void Awake()
	{
		if (healthManager == null)
			healthManager = GetComponent<HealthManager>();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		PlayerTeams.OnTeamDataChanged +=
			HandleTeamDataChanged;

		owningClient = FindOwningClient();

		if (owningClient == null)
		{
			Debug.LogError(
				$"Could not find MyClient for connection " +
				$"{Owner.ClientId}.",
				this);

			return;
		}

		SyncTeam();
	}

	public override void OnStopServer()
	{
		PlayerTeams.OnTeamDataChanged -=
			HandleTeamDataChanged;

		owningClient = null;

		base.OnStopServer();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (healthManager != null)
			healthManager.OnDied += HandlePlayerDied;

		if (IsOwner)
		{
			SetObjectsActive(ownerEnable, true);
			SetObjectsActive(ownerDisable, false);
		}
		else
		{
			SetObjectsActive(notOwnerDisable, false);

			if (playerMovement != null)
				playerMovement.enabled = false;

			if (mouseLook != null)
				mouseLook.enabled = false;

			if (characterAnimator != null)
				characterAnimator.enabled = false;
		}
	}

	public override void OnStopClient()
	{
		if (healthManager != null)
			healthManager.OnDied -= HandlePlayerDied;

		base.OnStopClient();
	}

	/// <summary>
	/// Called once on every observing client when this player dies.
	/// </summary>
	private void HandlePlayerDied()
	{
		if(IsOwner)
		{
			playerMovement.enabled = false;
			mouseLook.enabled = false;
			MyClient.Instance.PlayerManager.KillPlayer();
		}
		if (IsServerInitialized)
		{
			ragdollManager.EnableRagdoll();
			healthManager.LastDamagedHitbox.GetComponent<Rigidbody>().AddForceAtPosition(healthManager.LastDamageDirection * healthManager.LastDamageForce, healthManager.LastHitPosition, ForceMode.VelocityChange);
		}
		else
		{
			
		}

		foreach(GameObject obj in deadDisable)
		{
			obj.SetActive(false);
		}

		inventory?.UnequipCurrentItem();

		inventoryObject.SetActive(false);
	}

	private static void SetObjectsActive(
		IEnumerable<GameObject> objects,
		bool active)
	{
		foreach (GameObject obj in objects)
		{
			if (obj != null)
				obj.SetActive(active);
		}
	}

	[Server]
	private void HandleTeamDataChanged()
	{
		SyncTeam();
	}

	public void TogglePause(bool paused)
	{
		if(paused)
		{
			inventory.SetPaused(true);
		}
		else
		{
			inventory.SetPaused(false);
		}
	}

	[Server]
	private void SyncTeam()
	{
		if (owningClient == null)
			return;

		owningClient.RefreshTeamInformation();

		teamId.Value = owningClient.TeamId;
		teamType.Value = owningClient.CurrentTeamType;

		if (healthManager != null)
			healthManager.SetTeam(teamId.Value);
	}

	private MyClient FindOwningClient()
	{
		MyClient[] clients =
			FindObjectsByType<MyClient>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None);

		foreach (MyClient client in clients)
		{
			if (client == null || !client.IsSpawned)
				continue;

			if (client.Owner.ClientId == Owner.ClientId)
				return client;
		}

		return null;
	}

	public CharControllerServiceLocator GetServiceLocator()
	{
		return charControllerServiceLocator;
	}

	public Slider GetNVGSlider()
	{
		return NVGSlider;
	}

	public GameObject GetNVGFX()
	{
		return NVGFX;
	}

	public Slider GetThermalSlider()
	{
		return ThermalGogglesSlider;
	}

	public GameObject GetThermalFX()
	{
		return ThermalGogglesFX;
	}
}