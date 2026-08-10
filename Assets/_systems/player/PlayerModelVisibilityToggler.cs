using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class PlayerModelVisibilityToggler : NetworkBehaviour
{
	[SerializeField] List<Renderer> meshRenderers;

	public override void OnStartClient()
	{
		base.OnStartClient();
		if(IsOwner)
		{
			foreach(Renderer renderer in meshRenderers)
			{
				renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
			}
		}
	}
}
