using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SecuritySabotageManager : NetworkBehaviour
{
	public static SecuritySabotageManager Instance;

	bool isSecurityOn = true;
	void Awake()
	{
		Instance = this;
	}

	[ServerRpc(RequireOwnership = false)]
	public void ToggleSecurityServer(bool isSecurityOn)
	{
		ToggleSecurityClient(isSecurityOn);
	}

	[ObserversRpc]
	void ToggleSecurityClient(bool isSecurityOn)
	{
		var securitySabotageables = FindObjectsOfType<MonoBehaviour>().OfType<ISecuritySabotageable>();
		foreach (ISecuritySabotageable securitySabotageable in securitySabotageables)
		{
			securitySabotageable.ToggleSecurity(isSecurityOn);
		}

		this.isSecurityOn = isSecurityOn;
	}

	public bool IsSecurityOn()
	{
		return isSecurityOn;
	}
}
