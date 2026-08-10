using System;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public sealed class LockdownManager : NetworkBehaviour
{
	public static LockdownManager Instance { get; private set; }

	[Header("Lockdown")]
	[SerializeField, Min(0f)]
	private float lockdownStartDelay = 3f;

	[SerializeField, Min(0f)]
	private float lockdownDuration = 30f;

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	[SerializeField]
	private AudioClip lockdownStartClip;

	private readonly SyncVar<bool> isLockedDown =
		new(false);

	public bool IsLockedDown =>
		isLockedDown.Value;

	public bool HasTriggeredThisRound =>
		lockdownTriggeredThisRound;

	/// <summary>
	/// Raised immediately when the first loot pickup
	/// triggers the lockdown sequence.
	///
	/// The lockdown may not actually be active yet.
	/// </summary>
	public event Action OnLockdownTriggered;

	/// <summary>
	/// Raised when the lockdown actually becomes active.
	/// </summary>
	public event Action OnLockdownStarted;

	/// <summary>
	/// Raised when an active lockdown ends.
	/// </summary>
	public event Action OnLockdownEnded;

	/// <summary>
	/// Raised when lockdown is reset for a new round.
	/// </summary>
	public event Action OnLockdownReset;

	private bool lockdownTriggeredThisRound;

	private Coroutine lockdownRoutine;


	private void Awake()
	{
		if (Instance != null &&
			Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}


	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}


	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		isLockedDown.OnChange +=
			HandleLockdownChanged;

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted +=
				HandleRoundSetupStarted;
		}
	}


	public override void OnStopNetwork()
	{
		isLockedDown.OnChange -=
			HandleLockdownChanged;

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted -=
				HandleRoundSetupStarted;
		}

		if (lockdownRoutine != null)
		{
			StopCoroutine(
				lockdownRoutine);

			lockdownRoutine = null;
		}

		base.OnStopNetwork();
	}


	/// <summary>
	/// Called when loot has successfully been picked up.
	/// Only the first call each round triggers lockdown.
	/// </summary>
	[ServerRpc(RequireOwnership = false)]
	public void RequestLockdownServerRpc()
	{
		TriggerLockdown();
	}


	/// <summary>
	/// Reserves and starts the lockdown sequence.
	/// </summary>
	[Server]
	public void TriggerLockdown()
	{
		if (lockdownTriggeredThisRound)
			return;

		lockdownTriggeredThisRound = true;

		LockdownTriggeredObserversRpc();

		if (lockdownRoutine != null)
		{
			StopCoroutine(
				lockdownRoutine);
		}

		lockdownRoutine =
			StartCoroutine(
				LockdownSequence());
	}


	[Server]
	private IEnumerator LockdownSequence()
	{
		if (lockdownStartDelay > 0f)
		{
			yield return new WaitForSeconds(
				lockdownStartDelay);
		}

		SetLockdownStateServer(
			true);

		if (lockdownDuration > 0f)
		{
			yield return new WaitForSeconds(
				lockdownDuration);
		}

		SetLockdownStateServer(
			false);

		lockdownRoutine = null;
	}


	[Server]
	private void SetLockdownStateServer(
		bool lockedDown)
	{
		if (isLockedDown.Value == lockedDown)
			return;

		isLockedDown.Value =
			lockedDown;

		if (lockedDown)
		{
			LockdownStartedObserversRpc();
		}
		else
		{
			LockdownEndedObserversRpc();
		}
	}


	/// <summary>
	/// Immediately ends an active lockdown.
	/// This does not allow another lockdown this round.
	/// </summary>
	[Server]
	public void StopLockdown()
	{
		if (lockdownRoutine != null)
		{
			StopCoroutine(
				lockdownRoutine);

			lockdownRoutine = null;
		}

		SetLockdownStateServer(
			false);
	}


	/// <summary>
	/// Fully resets lockdown so the next loot pickup
	/// may trigger it again.
	/// </summary>
	[Server]
	public void ResetLockdown()
	{
		if (lockdownRoutine != null)
		{
			StopCoroutine(
				lockdownRoutine);

			lockdownRoutine = null;
		}

		SetLockdownStateServer(
			false);

		lockdownTriggeredThisRound =
			false;

		LockdownResetObserversRpc();
	}


	private void HandleRoundSetupStarted(
		int round)
	{
		if (!IsServerInitialized)
			return;

		ResetLockdown();
	}


	private void HandleLockdownChanged(
		bool previous,
		bool current,
		bool asServer)
	{
		/*
		 * The actual events are sent explicitly using
		 * ObserversRpc, so this callback only exists to
		 * keep the SyncVar state available to clients.
		 */
	}


	[ObserversRpc(RunLocally = true)]
	private void LockdownTriggeredObserversRpc()
	{
		OnLockdownTriggered?.Invoke();
	}


	[ObserversRpc(RunLocally = true)]
	private void LockdownStartedObserversRpc()
	{
		if (IsClientInitialized &&
			audioSource != null &&
			lockdownStartClip != null)
		{
			audioSource.PlayOneShot(
				lockdownStartClip);
		}

		OnLockdownStarted?.Invoke();
	}


	[ObserversRpc(RunLocally = true)]
	private void LockdownEndedObserversRpc()
	{
		OnLockdownEnded?.Invoke();
	}


	[ObserversRpc(RunLocally = true)]
	private void LockdownResetObserversRpc()
	{
		OnLockdownReset?.Invoke();
	}
}