using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecurityDisableObject : MonoBehaviour, ISecuritySabotageable
{
	[SerializeField] List<GameObject> disableObjects = new List<GameObject>();
	public void ToggleSecurity(bool isSecurityOn)
	{
		foreach(GameObject obj in disableObjects)
		{
			if (obj != null)
			{
				obj.SetActive(isSecurityOn);
			}
		}
	}
}
