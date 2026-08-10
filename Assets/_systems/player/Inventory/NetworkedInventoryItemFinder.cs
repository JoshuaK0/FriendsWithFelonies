using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class NetworkedInventoryItemFinder : NetworkBehaviour
{
	public static NetworkedInventoryItemFinder Instance;

	[System.Serializable]
	public struct NetworkedItem
	{
		public string Name;
		public GameObject GameObject;
	}

	[SerializeField] List<NetworkedItem> networkedItems = new List<NetworkedItem>();

	public GameObject GetItemByName(string name)
	{
		foreach(NetworkedItem item in networkedItems)
		{
			if(item.Name == name)
			{
				return item.GameObject;
			}
		}
		return null;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if(IsOwner)
		{
			Instance = this;
		}
	}

	public override void OnStopClient()
	{
		if (Instance == this)
			Instance = null;

		base.OnStopClient();
	}
}
