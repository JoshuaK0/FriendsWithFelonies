using FishNet.Object;
using UnityEngine;

public class UVFootstepsSpawner : NetworkBehaviour
{
    [SerializeField] GameObject footstepPrefab;
    [SerializeField] float rate;
	[SerializeField] float footstepLifetime;
	[SerializeField] float minDistance;
	Vector3 lastPos;

	float lastFootstepTime;

	public override void OnStartClient()
	{
		base.OnStartClient(); 
		if (!IsOwner)
		{
			enabled = false;
		}
	}

	void Update()
    {

		if (!IsSpawned || !IsClientInitialized)
		{
			return;
		}

		if (PlayerTeams.Instance.GetTeamType(MyClient.Instance.TeamId) != TeamType.Robber)
		{
			return;
		}



		if (Vector3.Distance(lastPos, transform.position) > minDistance)
		{
			if (lastFootstepTime <= 0)
			{
				lastFootstepTime = rate;
				CreateFootstepServer(transform.position, transform.rotation);
				lastPos = transform.position;
			}
			else
			{
				lastFootstepTime -= Time.deltaTime;
			}
		}
		else
		{
			lastFootstepTime -= Time.deltaTime;

		}

	}

    [ServerRpc]
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
}
