using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlarmIndicator : NetworkBehaviour
{
	[SerializeField] AudioSource source;
	[SerializeField] Light alarmLight;

	[SerializeField] float alarmDuration;

	[SerializeField] Vector2 flashingLightIntensity;
	[SerializeField] float flashingLightSmoothing;
	[SerializeField] float flashingLightInterval;

	float targetIntensity;

	float flashTimer;

	float runningTime;

	bool isAlarmActive;

	[SerializeField] ScreenIndicatorUI indicatorUI;

	[SerializeField] bool requireOwnershipForUIIndicator;

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (IsOwner || !requireOwnershipForUIIndicator)
		{
			PlayerManager.Instance.OnLocalPlayerSpawned += InitTripwireDelegate;

			if(MyClient.Instance.PlayerManager.LocalPlayerController != null)
			{
				InitTripwire();
			}
			
		}
	}
	void InitTripwireDelegate(GameObject controller)
	{
		InitTripwire();
	}
	void InitTripwire()
	{
		Debug.Log(indicatorUI);
		indicatorUI.InitialiseTargetIndicator(gameObject, MyClient.Instance.PlayerManager.LocalPlayerController.GetComponent<PlayerCharacter>().GetServiceLocator().PlayerCamera.GetComponent<Camera>());
	}

	void Update()
	{
		if (isAlarmActive)
		{
			FlashLight();
			runningTime += Time.deltaTime;
			if (runningTime >= alarmDuration)
			{
				if (alarmLight != null)
				{
					alarmLight.gameObject.SetActive(false);

				}
				source.Stop();
				isAlarmActive = false;
				indicatorUI.EnableUI(false);
			}
		}
	}

	public void StartAlarm()
	{
		isAlarmActive = true;
		runningTime = 0;
		flashTimer = 0;

		if (alarmLight != null)
		{
			alarmLight.gameObject.SetActive(true);

		}
		if (!source.isPlaying)
		{
			source.Play();
		}

		if(MyClient.Instance.CurrentTeamType == TeamType.Cop)
		{
			indicatorUI.EnableUI(true);
		}
	}

	void FlashLight()
	{
		if (flashTimer >= flashingLightInterval)
		{
			flashTimer = 0;
			if (targetIntensity == flashingLightIntensity.x)
			{
				targetIntensity = flashingLightIntensity.y;
			}
			else if (targetIntensity == flashingLightIntensity.y)
			{
				targetIntensity = flashingLightIntensity.x;
			}
		}
		else
		{
			flashTimer += Time.deltaTime;
		}


		if (alarmLight != null)
		{
			alarmLight.intensity = Mathf.Lerp(alarmLight.intensity, targetIntensity, flashingLightSmoothing * Time.deltaTime);

		}
	}

	void OnDestroy()
	{
		if (IsOwner || !requireOwnershipForUIIndicator)
		{
			if(PlayerManager.Instance != null)
			{
				PlayerManager.Instance.OnLocalPlayerSpawned -= InitTripwireDelegate;
			}
		}
	}

	void OnDisable()
	{
		if (IsOwner || !requireOwnershipForUIIndicator)
		{
			if (PlayerManager.Instance != null)
			{
				PlayerManager.Instance.OnLocalPlayerSpawned -= InitTripwireDelegate;
			}
		}
	}
}
