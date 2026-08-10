using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecalFade : MonoBehaviour
{
	[SerializeField] float lifetime;
	[SerializeField] Renderer objectRenderer;
	MaterialPropertyBlock propertyBlock;

	float startTime;
	
	void Start()
	{
		startTime = Time.time;
		propertyBlock = new MaterialPropertyBlock();
	}

	void Update()
	{
		// Set the unique alpha value for this instance
		propertyBlock.SetFloat("_AlphaControl", 1 - Mathf.InverseLerp(startTime, startTime + lifetime, Time.time));
		objectRenderer.SetPropertyBlock(propertyBlock);
	}

	public void ResetFade()
	{
		startTime = Time.time;
	}
}
