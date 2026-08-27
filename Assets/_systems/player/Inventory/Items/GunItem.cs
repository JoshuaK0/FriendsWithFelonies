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
	private Transform fireOrigin;

	[Tooltip("Transform used for firing recoil.")]
	[SerializeField]
	private Transform viewmodelHolder;

	[SerializeField]
	private GameObject[] viewmodelObjects;


	[Header("Fire Effects")]
	[SerializeField]
	private ParticleSystem[] fireParticleEffects;

	[SerializeField]
	private AudioSource firingAudioSource;

	[Tooltip("A random sound from this list is played each time the gun fires.")]
	[SerializeField]
	private AudioClip[] firingSounds;

	[SerializeField]
	private float minFirePitch = 0.9f;

	[SerializeField]
	private float maxFirePitch = 1.1f;


	[Header("Reload Effects")]
	[Tooltip("Audio source used for the reload sound.")]
	[SerializeField]
	private AudioSource reloadAudioSource;

	[Tooltip("Sound played when reloading begins.")]
	[SerializeField]
	private AudioClip reloadSound;

	[Tooltip(
		"Transform that spins around its own local X axis " +
		"during the reload.")]
	[SerializeField]
	private Transform reloadSpinTransform;

	[Tooltip(
		"Total rotation of the reload spin. " +
		"360 = one full spin.")]
	[SerializeField]
	private float reloadSpinDegrees = 360f;

	[Tooltip(
		"Controls rotation progress over the reload. " +
		"X = normalized reload time. " +
		"Y = normalized spin progress.")]
	[SerializeField]
	private AnimationCurve reloadSpinCurve =
		AnimationCurve.EaseInOut(
			0f,
			0f,
			1f,
			1f);


	[Header("Impact Effects")]
	[Tooltip("Spawned when a bullet hits an IDamageable.")]
	[SerializeField]
	private GameObject damageableHitPrefab;

	[Tooltip("Spawned when a bullet hits something that is not an IDamageable.")]
	[SerializeField]
	private GameObject surfaceHitPrefab;

	[Tooltip("Pushes the impact slightly away from the surface.")]
	[SerializeField, Min(0f)]
	private float impactSurfaceOffset = 0.001f;

	[Tooltip(
		"0 = inverse surface normal. " +
		"1 = bullet travel direction.")]
	[SerializeField, Range(0f, 1f)]
	private float impactRotationLerp = 0.5f;

	[Tooltip(
		"Automatically destroys impact objects after this many seconds. " +
		"0 means never destroy automatically.")]
	[SerializeField, Min(0f)]
	private float impactLifetime = 5f;


	[Header("Viewmodel Recoil")]
	[SerializeField]
	private Vector3 firePositionOffset =
		new(0f, 0f, -0.05f);

	[SerializeField]
	private Vector3 fireRotationOffset =
		new(-4f, 0f, 0f);

	[Tooltip(
		"Furthest negative local Z displacement " +
		"the viewmodel can reach.")]
	[SerializeField]
	private float maxZDisplacement = -0.15f;

	[Tooltip(
		"Furthest negative local X rotation " +
		"the viewmodel can reach.")]
	[SerializeField]
	private float maxXRotation = -15f;

	[SerializeField, Min(0f)]
	private float viewmodelReturnSpeed = 12f;


	[Header("UI")]
	[SerializeField]
	private GameObject crosshair;

	[SerializeField]
	private TextMeshProUGUI ammoCounter;


	private CrosshairController crosshairController;

	private const string CurrentRoundsStateKey =
		"current-rounds";

	private int currentRounds;
	private float currentBloom;
	private float nextFireTime;

	private Coroutine reloadRoutine;

	private Vector3 originalViewmodelPosition;
	private Quaternion originalViewmodelRotation;

	private Vector3 viewmodelPositionOffset;
	private Vector3 viewmodelRotationOffset;

	private Quaternion originalReloadSpinRotation;

	private bool viewmodelInitialized;
	private bool reloadSpinInitialized;
	private bool warnedAboutMissingReference;

	private GunItemNetworked networkedCounterpart;


	public int CurrentRounds =>
		currentRounds;

	public int MagazineSize =>
		Mathf.Max(
			1,
			magazineSize);

	public bool IsReloading =>
		reloadRoutine != null;

	public float CurrentSpread =>
		Mathf.Max(
			0f,
			baseSpread + currentBloom);


	protected override void OnContextInitialized()
	{
		if (CharacterServices != null)
		{
			crosshairController = CharacterServices.CrosshairController;
			fireOrigin = CharacterServices.muzzle;
		}

		currentRounds =
			Mathf.Clamp(
				RuntimeState != null
					? RuntimeState.GetInt(
						ItemId,
						CurrentRoundsStateKey,
						MagazineSize)
					: MagazineSize,
				0,
				MagazineSize);

		SaveCurrentRounds();

		ResolveRuntimeReferences();

		InitializeViewmodel();
		InitializeReloadSpin();

		UpdateCrosshair();
	}


	protected override void OnEquipped()
	{
		ResolveRuntimeReferences();

		InitializeViewmodel();
		InitializeReloadSpin();

		currentRounds =
			Mathf.Clamp(
				currentRounds,
				0,
				MagazineSize);

		SaveCurrentRounds();

		ResetViewmodel();
		ResetReloadSpin();

		SetViewmodelActive(true);

		if (crosshair != null)
		{
			crosshair.SetActive(true);
		}

		if (ammoCounter != null)
		{
			ammoCounter.enabled = true;
		}

		UpdateAmmoCounter();
		UpdateCrosshair();
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
		{
			TryFire();
		}

		if (Input.GetKeyDown(KeyCode.R))
		{
			StartReload();
		}

		UpdateCrosshair();
	}


	protected override void OnUnequipped()
	{
		CancelReload();
		SaveCurrentRounds();

		currentBloom = 0f;

		ResetViewmodel();
		ResetReloadSpin();

		SetViewmodelActive(false);

		if (crosshairController != null)
		{
			crosshairController
				.SetGapMultiplier(1f);
		}

		if (crosshair != null)
		{
			crosshair.SetActive(false);
		}

		if (ammoCounter != null)
		{
			ammoCounter.enabled = false;
		}
	}


	private void TryFire()
	{
		if (IsReloading)
		{
			return;
		}

		if (currentRounds <= 0)
		{
			return;
		}

		if (Time.time < nextFireTime)
		{
			return;
		}

		ResolveRuntimeReferences();

		MyClient client =
			MyClient.Instance;

		if (!ValidateFireReferences(client))
		{
			return;
		}

		nextFireTime =
			Time.time +
			60f /
			Mathf.Max(
				1f,
				roundsPerMinute);

		Vector3[] pelletDirections =
			CalculatePelletDirections();

		currentRounds--;
		SaveCurrentRounds();

		currentBloom =
			Mathf.Min(
				currentBloom + bloomPerShot,
				maximumBloom);

		/*
		 * OWNER-LOCAL PREDICTION
		 *
		 * These happen immediately for the shooting player.
		 *
		 * The server does NOT send these effects back to the owner.
		 */
		PlayLocalFireEffects();

		SpawnImpactEffects(
			pelletDirections);

		UpdateAmmoCounter();
		UpdateCrosshair();

		/*
		 * The server independently raycasts using the same
		 * origin/directions and decides whether damage occurs.
		 */
		networkedCounterpart.RequestFire(
			fireOrigin.position,
			pelletDirections,
			Mathf.Max(
				1f,
				roundsPerMinute),
			Mathf.Max(
				0f,
				damage),
			Mathf.Max(
				0.1f,
				range),
			hitMask.value,
			Mathf.Max(
				0f,
				ragdollForce),
			client.TeamId);
	}


	private void PlayLocalFireEffects()
	{
		viewmodelPositionOffset +=
			firePositionOffset;

		viewmodelRotationOffset +=
			fireRotationOffset;


		// Both recoil limits are negative.
		viewmodelPositionOffset.z =
			Mathf.Clamp(
				viewmodelPositionOffset.z,
				maxZDisplacement,
				0f);

		viewmodelRotationOffset.x =
			Mathf.Clamp(
				viewmodelRotationOffset.x,
				maxXRotation,
				0f);


		if (fireParticleEffects != null)
		{
			foreach (
				ParticleSystem particleEffect
				in fireParticleEffects)
			{
				if (particleEffect != null)
				{
					particleEffect.Play();
				}
			}
		}

		PlayRandomFireSound();
	}


	private void PlayRandomFireSound()
	{
		if (firingAudioSource == null)
		{
			return;
		}

		if (firingSounds == null ||
			firingSounds.Length == 0)
		{
			return;
		}


		AudioClip clip = null;

		int startIndex =
			Random.Range(
				0,
				firingSounds.Length);


		for (int i = 0;
			i < firingSounds.Length;
			i++)
		{
			int index =
				(startIndex + i) %
				firingSounds.Length;

			if (firingSounds[index] == null)
			{
				continue;
			}

			clip =
				firingSounds[index];

			break;
		}


		if (clip == null)
		{
			return;
		}


		float minimumPitch =
			Mathf.Min(
				minFirePitch,
				maxFirePitch);

		float maximumPitch =
			Mathf.Max(
				minFirePitch,
				maxFirePitch);


		firingAudioSource.pitch =
			Random.Range(
				minimumPitch,
				maximumPitch);

		firingAudioSource.PlayOneShot(
			clip);
	}


	private void PlayReloadSound()
	{
		if (reloadAudioSource == null ||
			reloadSound == null)
		{
			return;
		}

		reloadAudioSource.PlayOneShot(
			reloadSound);
	}


	private void SpawnImpactEffects(
		Vector3[] pelletDirections)
	{
		if (fireOrigin == null ||
			pelletDirections == null)
		{
			return;
		}


		for (int i = 0;
			i < pelletDirections.Length;
			i++)
		{
			Vector3 bulletDirection =
				pelletDirections[i]
					.normalized;


			if (!Physics.Raycast(
					fireOrigin.position,
					bulletDirection,
					out RaycastHit hit,
					Mathf.Max(
						0.1f,
						range),
					hitMask,
					QueryTriggerInteraction.Collide))
			{
				continue;
			}


			SpawnImpactEffect(
				hit,
				bulletDirection);
		}
	}


	private void SpawnImpactEffect(
		RaycastHit hit,
		Vector3 bulletDirection)
	{
		bool hitDamageable =
			GetDamageable(
				hit.collider) != null;


		GameObject prefab =
			hitDamageable
				? damageableHitPrefab
				: surfaceHitPrefab;


		if (prefab == null)
		{
			return;
		}


		Vector3 inverseSurfaceNormal =
			hit.normal.normalized;

		Vector3 travelDirection =
			bulletDirection.normalized;


		Vector3 impactDirection =
			Vector3.Slerp(
				inverseSurfaceNormal,
				travelDirection,
				Mathf.Clamp01(
					impactRotationLerp));


		if (impactDirection.sqrMagnitude <
			0.0001f)
		{
			impactDirection =
				inverseSurfaceNormal;
		}


		impactDirection.Normalize();


		Quaternion impactRotation =
			Quaternion.FromToRotation(
				Vector3.forward,
				impactDirection);


		Vector3 impactPosition =
			hit.point +
			hit.normal *
			Mathf.Max(
				0f,
				impactSurfaceOffset);


		GameObject impact =
			Instantiate(
				prefab,
				impactPosition,
				impactRotation);


		if (hit.collider != null)
		{
			impact.transform.SetParent(
				hit.collider.transform,
				true);
		}


		if (impactLifetime > 0f)
		{
			Destroy(
				impact,
				impactLifetime);
		}
	}


	private IDamageable GetDamageable(
		Collider hitCollider)
	{
		if (hitCollider == null)
		{
			return null;
		}


		IDamageable damageable =
			hitCollider
				.GetComponent<IDamageable>();


		if (damageable != null)
		{
			return damageable;
		}


		damageable =
			hitCollider
				.GetComponentInParent<IDamageable>();


		if (damageable != null)
		{
			return damageable;
		}


		if (hitCollider.attachedRigidbody != null)
		{
			damageable =
				hitCollider.attachedRigidbody
					.GetComponent<IDamageable>();

			if (damageable != null)
			{
				return damageable;
			}


			damageable =
				hitCollider.attachedRigidbody
					.GetComponentInParent<IDamageable>();
		}


		return damageable;
	}


	private void UpdateViewmodelAnimation()
	{
		if (!viewmodelInitialized ||
			viewmodelHolder == null)
		{
			return;
		}


		float lerpAmount =
			Mathf.Max(
				0f,
				viewmodelReturnSpeed) *
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
		viewmodelPositionOffset =
			Vector3.zero;

		viewmodelRotationOffset =
			Vector3.zero;


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


	private void InitializeReloadSpin()
	{
		if (reloadSpinInitialized ||
			reloadSpinTransform == null)
		{
			return;
		}


		originalReloadSpinRotation =
			reloadSpinTransform.localRotation;

		reloadSpinInitialized = true;
	}


	private void ResetReloadSpin()
	{
		if (!reloadSpinInitialized ||
			reloadSpinTransform == null)
		{
			return;
		}


		reloadSpinTransform.localRotation =
			originalReloadSpinRotation;
	}


	private Vector3[] CalculatePelletDirections()
	{
		int pelletCount =
			Mathf.Max(
				1,
				pelletsPerShot);


		Vector3[] directions =
			new Vector3[pelletCount];


		Vector3 forward =
			fireOrigin.forward.normalized;

		Vector3 right =
			fireOrigin.right.normalized;

		Vector3 up =
			fireOrigin.up.normalized;


		float spread =
			CurrentSpread;


		for (int i = 0;
			i < pelletCount;
			i++)
		{
			if (spread <= 0f)
			{
				directions[i] =
					forward;

				continue;
			}


			Vector2 randomSpread =
				Random.insideUnitCircle *
				spread;


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
		currentBloom =
			Mathf.MoveTowards(
				currentBloom,
				0f,
				Mathf.Max(
					0f,
					bloomRecoverySpeed) *
				Time.deltaTime);
	}


	private void UpdateCrosshair()
	{
		if (crosshairController == null)
		{
			return;
		}


		float multiplier = 1f;


		if (baseSpread > 0f)
		{
			multiplier =
				CurrentSpread /
				baseSpread;
		}
		else if (currentBloom > 0f)
		{
			multiplier =
				1f +
				currentBloom;
		}


		crosshairController
			.SetGapMultiplier(
				multiplier);
	}


	private void StartReload()
	{
		if (IsReloading)
		{
			return;
		}

		if (currentRounds >= MagazineSize)
		{
			return;
		}

		if (!isActiveAndEnabled)
		{
			return;
		}


		InitializeReloadSpin();
		ResetReloadSpin();

		PlayReloadSound();


		reloadRoutine =
			StartCoroutine(
				ReloadRoutine());
	}


	private IEnumerator ReloadRoutine()
	{
		float duration =
			Mathf.Max(
				0f,
				reloadTime);


		if (duration <= 0f)
		{
			ResetReloadSpin();

			currentRounds =
				MagazineSize;

			SaveCurrentRounds();

			reloadRoutine = null;

			UpdateAmmoCounter();

			yield break;
		}


		float elapsed = 0f;


		while (elapsed < duration)
		{
			elapsed +=
				Time.deltaTime;


			float normalizedTime =
				Mathf.Clamp01(
					elapsed /
					duration);


			float spinProgress;

			if (reloadSpinCurve != null)
			{
				spinProgress =
					reloadSpinCurve.Evaluate(
						normalizedTime);
			}
			else
			{
				spinProgress =
					Mathf.SmoothStep(
						0f,
						1f,
						normalizedTime);
			}


			float spinAngle =
				spinProgress *
				reloadSpinDegrees;


			if (reloadSpinTransform != null &&
				reloadSpinInitialized)
			{
				reloadSpinTransform.localRotation =
					originalReloadSpinRotation *
					Quaternion.AngleAxis(
						spinAngle,
						Vector3.right);
			}


			yield return null;
		}


		if (reloadSpinTransform != null &&
			reloadSpinInitialized)
		{
			reloadSpinTransform.localRotation =
				originalReloadSpinRotation *
				Quaternion.AngleAxis(
					reloadSpinDegrees,
					Vector3.right);
		}


		ResetReloadSpin();


		currentRounds =
			MagazineSize;

		SaveCurrentRounds();

		reloadRoutine = null;

		UpdateAmmoCounter();
	}


	private void CancelReload()
	{
		if (reloadRoutine != null)
		{
			StopCoroutine(
				reloadRoutine);

			reloadRoutine = null;
		}


		ResetReloadSpin();


		if (reloadAudioSource != null)
		{
			reloadAudioSource.Stop();
		}
	}


	private void SaveCurrentRounds()
	{
		RuntimeState?.SetInt(
			ItemId,
			CurrentRoundsStateKey,
			currentRounds);
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
		{
			return;
		}


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
		{
			return;
		}


		var serviceLocator =
			playerCharacter
				.GetServiceLocator();


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
			warnedAboutMissingReference =
				false;

			return true;
		}


		if (!warnedAboutMissingReference)
		{
			warnedAboutMissingReference =
				true;


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


	private void SetViewmodelActive(
		bool active)
	{
		if (viewmodelObjects == null)
		{
			return;
		}


		foreach (
			GameObject viewmodelObject
			in viewmodelObjects)
		{
			if (viewmodelObject != null)
			{
				viewmodelObject.SetActive(
					active);
			}
		}
	}
}
