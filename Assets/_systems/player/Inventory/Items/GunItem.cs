using System.Collections;
using TMPro;
using UnityEngine;

public sealed class GunItem : HotbarHeldItem
{
	[Header("Gun Stats")]
	[SerializeField, Min(1)]
	private int magazineSize = 30;

	[SerializeField, Min(1f)]
	private float roundsPerMinute = 600f;

	[SerializeField, Min(0f)]
	private float reloadTime = 2f;

	[SerializeField]
	private bool automatic = true;

	[Header("Damage")]
	[SerializeField, Min(0f)]
	private float damage = 25f;

	[SerializeField, Min(0.1f)]
	private float range = 100f;

	[SerializeField]
	private LayerMask hitMask = ~0;

	[SerializeField, Min(1)]
	private int pelletsPerShot = 1;

	[SerializeField, Min(0f)]
	private float ragdollForce = 20f;

	[Header("Accuracy")]
	[SerializeField, Min(0f)]
	private float baseSpread = 0.25f;

	[SerializeField, Min(0f)]
	private float bloomPerShot = 0.3f;

	[SerializeField, Min(0f)]
	private float maximumBloom = 4f;

	[SerializeField, Min(0f)]
	private float bloomRecoverySpeed = 5f;

	[Header("References")]
	[Tooltip("Uses the local player's camera when left unassigned.")]
	[SerializeField]
	private Transform fireOrigin;

	[SerializeField]
	private Transform viewmodelHolder;

	[SerializeField]
	private GameObject[] viewmodelObjects;

	[SerializeField]
	private ParticleSystem muzzleFlash;

	[SerializeField]
	private AudioSource firingAudioSource;

	[Header("Viewmodel Animation")]
	[SerializeField]
	private Vector3 firePositionOffset =
		new(0f, 0f, -0.05f);

	[SerializeField]
	private Vector3 fireRotationOffset =
		new(-4f, 0f, 0f);

	[SerializeField, Min(0f)]
	private float viewmodelReturnSpeed = 12f;

	[Header("UI")]
	[SerializeField]
	private GameObject crosshair;

	[SerializeField]
	private TextMeshProUGUI ammoCounter;

	private int currentRounds;
	private float currentBloom;
	private float nextFireTime;

	private Coroutine reloadRoutine;

	private Vector3 originalViewmodelPosition;
	private Quaternion originalViewmodelRotation;

	private Vector3 viewmodelPositionOffset;
	private Vector3 viewmodelRotationOffset;

	private bool viewmodelInitialized;
	private bool warnedAboutMissingReference;

	private GunItemNetworked networkedCounterpart;

	public int CurrentRounds => currentRounds;
	public int MagazineSize => Mathf.Max(1, magazineSize);
	public bool IsReloading => reloadRoutine != null;

	public float CurrentSpread =>
		Mathf.Max(0f, baseSpread + currentBloom);

	protected override void OnContextInitialized()
	{
		currentRounds = MagazineSize;

		ResolveRuntimeReferences();
		InitializeViewmodel();
	}

	protected override void OnEquipped()
	{
		ResolveRuntimeReferences();
		InitializeViewmodel();

		currentRounds = Mathf.Clamp(
			currentRounds,
			0,
			MagazineSize);

		ResetViewmodel();
		SetViewmodelActive(true);

		if (crosshair != null)
			crosshair.SetActive(true);

		if (ammoCounter != null)
			ammoCounter.enabled = true;

		UpdateAmmoCounter();
	}

	protected override void OnEquippedUpdate()
	{
		RecoverBloom();
		UpdateViewmodelAnimation();

		bool firePressed =
			automatic
				? Input.GetMouseButton(0)
				: Input.GetMouseButtonDown(0);

		if (firePressed)
			TryFire();

		if (Input.GetKeyDown(KeyCode.R))
			StartReload();
	}

	protected override void OnUnequipped()
	{
		CancelReload();

		currentBloom = 0f;

		ResetViewmodel();
		SetViewmodelActive(false);

		if (crosshair != null)
			crosshair.SetActive(false);

		if (ammoCounter != null)
			ammoCounter.enabled = false;
	}

	private void TryFire()
	{
		if (IsReloading)
			return;

		if (currentRounds <= 0)
			return;

		if (Time.time < nextFireTime)
			return;

		ResolveRuntimeReferences();

		MyClient client = MyClient.Instance;

		if (!ValidateFireReferences(client))
			return;

		nextFireTime =
			Time.time +
			60f / Mathf.Max(1f, roundsPerMinute);

		Vector3[] pelletDirections =
			CalculatePelletDirections();

		currentRounds--;

		currentBloom = Mathf.Min(
			currentBloom + bloomPerShot,
			maximumBloom);

		PlayLocalFireEffects();
		UpdateAmmoCounter();

		networkedCounterpart.RequestFire(
			fireOrigin.position,
			pelletDirections,
			Mathf.Max(1f, roundsPerMinute),
			Mathf.Max(0f, damage),
			Mathf.Max(0.1f, range),
			hitMask.value,
			Mathf.Max(0f, ragdollForce),
			client.TeamId);
	}

	private void PlayLocalFireEffects()
	{
		viewmodelPositionOffset =
			firePositionOffset;

		viewmodelRotationOffset =
			fireRotationOffset;

		if (muzzleFlash != null)
			muzzleFlash.Play();

		if (firingAudioSource != null &&
			firingAudioSource.clip != null)
		{
			firingAudioSource.PlayOneShot(
				firingAudioSource.clip);
		}
	}

	private void UpdateViewmodelAnimation()
	{
		if (!viewmodelInitialized ||
			viewmodelHolder == null)
		{
			return;
		}

		float lerpAmount =
			Mathf.Max(0f, viewmodelReturnSpeed) *
			Time.deltaTime;

		viewmodelPositionOffset =
			Vector3.Lerp(
				viewmodelPositionOffset,
				Vector3.zero,
				lerpAmount);

		viewmodelRotationOffset =
			Vector3.Lerp(
				viewmodelRotationOffset,
				Vector3.zero,
				lerpAmount);

		viewmodelHolder.localPosition =
			originalViewmodelPosition +
			viewmodelPositionOffset;

		viewmodelHolder.localRotation =
			originalViewmodelRotation *
			Quaternion.Euler(
				viewmodelRotationOffset);
	}

	private void InitializeViewmodel()
	{
		if (viewmodelInitialized ||
			viewmodelHolder == null)
		{
			return;
		}

		originalViewmodelPosition =
			viewmodelHolder.localPosition;

		originalViewmodelRotation =
			viewmodelHolder.localRotation;

		viewmodelInitialized = true;
	}

	private void ResetViewmodel()
	{
		viewmodelPositionOffset = Vector3.zero;
		viewmodelRotationOffset = Vector3.zero;

		if (!viewmodelInitialized ||
			viewmodelHolder == null)
		{
			return;
		}

		viewmodelHolder.localPosition =
			originalViewmodelPosition;

		viewmodelHolder.localRotation =
			originalViewmodelRotation;
	}

	private Vector3[] CalculatePelletDirections()
	{
		int pelletCount =
			Mathf.Max(1, pelletsPerShot);

		Vector3[] directions =
			new Vector3[pelletCount];

		Vector3 forward =
			fireOrigin.forward.normalized;

		Vector3 right =
			fireOrigin.right.normalized;

		Vector3 up =
			fireOrigin.up.normalized;

		float spread = CurrentSpread;

		for (int i = 0; i < pelletCount; i++)
		{
			if (spread <= 0f)
			{
				directions[i] = forward;
				continue;
			}

			Vector2 randomSpread =
				Random.insideUnitCircle * spread;

			Quaternion horizontalRotation =
				Quaternion.AngleAxis(
					randomSpread.x,
					up);

			Quaternion verticalRotation =
				Quaternion.AngleAxis(
					-randomSpread.y,
					right);

			directions[i] =
				(
					horizontalRotation *
					verticalRotation *
					forward
				).normalized;
		}

		return directions;
	}

	private void RecoverBloom()
	{
		currentBloom = Mathf.MoveTowards(
			currentBloom,
			0f,
			Mathf.Max(0f, bloomRecoverySpeed) *
			Time.deltaTime);
	}

	private void StartReload()
	{
		if (IsReloading)
			return;

		if (currentRounds >= MagazineSize)
			return;

		if (!isActiveAndEnabled)
			return;

		reloadRoutine =
			StartCoroutine(ReloadRoutine());
	}

	private IEnumerator ReloadRoutine()
	{
		yield return new WaitForSeconds(
			Mathf.Max(0f, reloadTime));

		currentRounds = MagazineSize;
		reloadRoutine = null;

		UpdateAmmoCounter();
	}

	private void CancelReload()
	{
		if (reloadRoutine == null)
			return;

		StopCoroutine(reloadRoutine);
		reloadRoutine = null;
	}

	private void ResolveRuntimeReferences()
	{
		ResolveNetworkedCounterpart();
		ResolveFireOrigin();
	}

	private void ResolveNetworkedCounterpart()
	{
		if (networkedCounterpart != null ||
			ItemServices == null)
		{
			return;
		}

		networkedCounterpart =
			ItemServices.GetNetworkedGun();
	}

	private void ResolveFireOrigin()
	{
		if (fireOrigin != null)
			return;

		PlayerManager playerManager =
			PlayerManager.Instance;

		if (playerManager == null ||
			playerManager.LocalPlayerController == null)
		{
			return;
		}

		PlayerCharacter playerCharacter =
			playerManager.LocalPlayerController
				.GetComponent<PlayerCharacter>();

		if (playerCharacter == null)
			return;

		var serviceLocator =
			playerCharacter.GetServiceLocator();

		if (serviceLocator != null)
		{
			fireOrigin =
				serviceLocator.PlayerCamera;
		}
	}

	private bool ValidateFireReferences(
		MyClient client)
	{
		string missingReference = null;

		if (fireOrigin == null)
		{
			missingReference =
				"the fire origin";
		}
		else if (networkedCounterpart == null)
		{
			missingReference =
				"the networked gun counterpart";
		}
		else if (client == null)
		{
			missingReference =
				nameof(MyClient);
		}

		if (missingReference == null)
		{
			warnedAboutMissingReference = false;
			return true;
		}

		if (!warnedAboutMissingReference)
		{
			warnedAboutMissingReference = true;

			Debug.LogWarning(
				$"{nameof(GunItem)} on '{name}' cannot fire " +
				$"because {missingReference} was not found.",
				this);
		}

		return false;
	}

	private void UpdateAmmoCounter()
	{
		if (ammoCounter != null)
		{
			ammoCounter.text =
				$"{currentRounds} | {MagazineSize}";
		}
	}

	private void SetViewmodelActive(bool active)
	{
		if (viewmodelObjects == null)
			return;

		foreach (GameObject viewmodelObject
			in viewmodelObjects)
		{
			if (viewmodelObject != null)
				viewmodelObject.SetActive(active);
		}
	}
}