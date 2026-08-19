using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Server-triggered network tripwire. The server decides whether a collider is
/// a valid target and synchronizes the alarm state to every observer.
/// </summary>
public sealed class TripWireProp : NetworkBehaviour
{
	[Header("Trigger Validation")]
	[SerializeField]
	private LayerMask triggerMask = ~0;

	[SerializeField]
	private bool ignoreOwner = true;

	[SerializeField]
	private bool triggerOnlyDifferentTeam = true;

	[SerializeField]
	private bool requireTeamIdProvider;

	[Header("Alarm Timing")]
	[SerializeField, Min(0f)]
	private float warningDelay;

	[SerializeField, Min(0f)]
	private float alarmDuration = 5f;

	[Header("Alarm Presentation")]
	[SerializeField]
	private AudioSource alarmAudio;

	[SerializeField]
	private Light alarmLight;

	[SerializeField]
	private Vector2 flashingLightIntensity =
		new(0f, 4f);

	[SerializeField, Min(0f)]
	private float flashingLightSmoothing = 12f;

	[SerializeField, Min(0.01f)]
	private float flashingLightInterval = 0.2f;

	[Header("Alarm Indicator")]
	[SerializeField]
	private AlarmIndicator alarmIndicator;

	[Header("Events")]
	[SerializeField]
	private UnityEvent onAlarmStarted;

	[SerializeField]
	private UnityEvent onAlarmStopped;

	private readonly SyncVar<int> ownerTeamId = new(-1);
	private readonly SyncVar<bool> alarmActive = new(false);

	private Coroutine serverAlarmRoutine;

	private float flashTimer;
	private float targetIntensity;

	public int OwnerTeamId =>
		ownerTeamId.Value;

	public bool IsAlarmActive =>
		alarmActive.Value;

	public override void OnStartClient()
	{
		base.OnStartClient();

		alarmActive.OnChange += OnAlarmActiveChanged;

		ApplyAlarmState(
			alarmActive.Value,
			false);
	}

	public override void OnStopClient()
	{
		alarmActive.OnChange -= OnAlarmActiveChanged;

		ApplyAlarmState(
			false,
			false);

		base.OnStopClient();
	}

	[Server]
	public void InitializeServer(int teamId)
	{
		ownerTeamId.Value = teamId;
		alarmActive.Value = false;
	}

	private void Update()
	{
		if (!alarmActive.Value)
			return;

		UpdateAlarmLight();
	}

	private void UpdateAlarmLight()
	{
		if (alarmLight == null)
			return;

		flashTimer += Time.deltaTime;

		if (flashTimer >= flashingLightInterval)
		{
			flashTimer = 0f;

			targetIntensity =
				Mathf.Approximately(
					targetIntensity,
					flashingLightIntensity.x)
					? flashingLightIntensity.y
					: flashingLightIntensity.x;
		}

		alarmLight.intensity =
			Mathf.Lerp(
				alarmLight.intensity,
				targetIntensity,
				flashingLightSmoothing *
				Time.deltaTime);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!IsServerInitialized)
			return;

		if (alarmActive.Value)
			return;

		if (serverAlarmRoutine != null)
			return;

		if (other == null)
			return;

		if ((triggerMask.value &
			 (1 << other.gameObject.layer)) == 0)
		{
			return;
		}

		NetworkObject targetNetworkObject =
			other.GetComponentInParent<NetworkObject>();

		if (ignoreOwner &&
			targetNetworkObject != null &&
			targetNetworkObject.Owner == Owner)
		{
			return;
		}

		ITeamIdProvider targetTeam =
			ComponentInterfaceUtility
				.FindInParents<ITeamIdProvider>(other);

		if (requireTeamIdProvider &&
			targetTeam == null)
		{
			return;
		}

		if (triggerOnlyDifferentTeam &&
			ownerTeamId.Value >= 0 &&
			targetTeam != null &&
			targetTeam.TeamId == ownerTeamId.Value)
		{
			return;
		}

		// Don't trigger security alarms while
		// the security system is sabotaged.
		if (SecuritySabotageManager.Instance != null &&
			!SecuritySabotageManager.Instance.IsSecurityOn())
		{
			return;
		}

		serverAlarmRoutine =
			StartCoroutine(ServerAlarmRoutine());
	}

	[Server]
	private IEnumerator ServerAlarmRoutine()
	{
		if (warningDelay > 0f)
		{
			yield return
				new WaitForSeconds(warningDelay);
		}

		// Security may have been disabled during
		// the warning delay.
		if (SecuritySabotageManager.Instance != null &&
			!SecuritySabotageManager.Instance.IsSecurityOn())
		{
			serverAlarmRoutine = null;
			yield break;
		}

		alarmActive.Value = true;

		if (alarmDuration > 0f)
		{
			yield return
				new WaitForSeconds(alarmDuration);
		}

		alarmActive.Value = false;
		serverAlarmRoutine = null;
	}

	private void OnAlarmActiveChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		ApplyAlarmState(
			next,
			true);
	}

	private void ApplyAlarmState(
		bool enabled,
		bool invokeEvents)
	{
		flashTimer = 0f;

		targetIntensity =
			flashingLightIntensity.y;

		if (alarmLight != null)
		{
			alarmLight.gameObject
				.SetActive(enabled);

			if (!enabled)
			{
				alarmLight.intensity =
					flashingLightIntensity.x;
			}
		}

		if (alarmAudio != null)
		{
			if (enabled)
			{
				if (!alarmAudio.isPlaying)
					alarmAudio.Play();
			}
			else
			{
				alarmAudio.Stop();
			}
		}

		// Reuse the same AlarmIndicator system
		// used by DoorInteractable.
		if (enabled && alarmIndicator != null)
		{
			alarmIndicator.StartAlarm();
		}

		if (!invokeEvents)
			return;

		if (enabled)
		{
			onAlarmStarted?.Invoke();
		}
		else
		{
			onAlarmStopped?.Invoke();
		}
	}
}