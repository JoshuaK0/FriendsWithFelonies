using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class FootstepsSpawner : NetworkBehaviour
{
	[SerializeField] GameObject footstepPrefab;
	[SerializeField] float rate;
	[SerializeField] float delay;
	[SerializeField] float minDistance;
	[SerializeField] float footstepLifetime;

	[SerializeField] Camera footstepsCamera;

	float lastFootstepTime;

	List<FootstepData> footsteps = new List<FootstepData>();

	Vector3 lastPos;

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!IsOwner)
		{
			Debug.Log("Is Owner of footstep spawner");
			enabled = false;
		}

		if (PlayerTeams.Instance.GetTeamType(MyClient.Instance.TeamId) != TeamType.Cop)
		{
			if (footstepsCamera != null)
			{
				footstepsCamera.enabled = true;

			}
		}
		else
		{
			if (footstepsCamera != null)
			{
				footstepsCamera.enabled = false;

			}
		}

		lastPos = transform.position;
	}

	void Update()
	{
		if (!IsSpawned || !IsClientInitialized)
		{
			return;
		}

		if (PlayerTeams.Instance.GetTeamType(MyClient.Instance.TeamId) != TeamType.Cop)
		{
			return;
		}

		if (lastFootstepTime <= 0 && Vector3.Distance(lastPos, transform.position) >= minDistance)
		{
			lastFootstepTime = rate;
			FootstepData newFootstepData = new FootstepData();
			newFootstepData.time = Time.time;
			newFootstepData.position = transform.position;
			newFootstepData.rotation = transform.rotation;

			footsteps.Add(newFootstepData);

			lastPos = transform.position;
		}
		else
		{
			lastFootstepTime -= Time.deltaTime;
		}

		if(footsteps.Count > 0)
		{
			if (footsteps[0].time < Time.time - delay)
			{
				CreateFootstepServer(footsteps[0].position, footsteps[0].rotation);
				footsteps.RemoveAt(0);

			}
		}

	}

	[ServerRpc(RequireOwnership = false)]
	void CreateFootstepServer(Vector3 position, Quaternion rot)
	{
		CreateFootstpesClient(position, rot);
	}
	[ObserversRpc]
	void CreateFootstpesClient(Vector3 position, Quaternion rot)
	{
		GameObject newFootstep = Instantiate(footstepPrefab, position, rot);
		Destroy(newFootstep, footstepLifetime);

	}

	struct FootstepData
	{
		public float time;
		public Vector3 position;
		public Quaternion rotation;
	}
}
