using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public sealed class DisguiseItemNetworked : NetworkBehaviour
{
	[Header("Default Character")]
	[Tooltip("Only these renderers are hidden while disguised.")]
	[SerializeField] private List<Renderer> defaultCharacterRenderers = new();

	[Header("Disguise Models")]
	[Tooltip("Possible disguise model GameObjects.")]
	[SerializeField] private List<GameObject> disguiseModels = new();

	[Header("Disguise Effects")]
	[Tooltip("Particle systems played for other clients whenever the disguise changes.")]
	[SerializeField] private List<ParticleSystem> disguiseParticles = new();

	[SerializeField] HealthManager healthManager;

	// Server-only state.
	// -1 means not disguised.
	private int currentDisguiseIndex = -1;

	private void Awake()
	{
		SetDefaultAppearance();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		SetDefaultAppearance();

		if(IsOwner)
		{
			healthManager.OnDied += RequestRevertDisguise;
		}
	}

	// =========================================================
	// Public requests
	// =========================================================

	public void RequestRandomDisguise()
	{
		if (!IsOwner)
			return;

		RequestRandomDisguiseServerRpc();
	}

	public void RequestRevertDisguise()
	{
		if (!IsOwner)
			return;

		RequestRevertDisguiseServerRpc();
	}

	// =========================================================
	// Server
	// =========================================================

	[ServerRpc]
	private void RequestRandomDisguiseServerRpc()
	{
		int newIndex = PickRandomDisguiseIndex();

		if (newIndex < 0)
			return;

		currentDisguiseIndex = newIndex;

		ApplyDisguiseObserversRpc(currentDisguiseIndex);
		PlayDisguiseParticlesObserversRpc();
	}

	[ServerRpc]
	private void RequestRevertDisguiseServerRpc()
	{
		currentDisguiseIndex = -1;

		ApplyDisguiseObserversRpc(-1);
	}

	// =========================================================
	// Client appearance
	// =========================================================

	[ObserversRpc(
		ExcludeOwner = true,
		BufferLast = true)]
	private void ApplyDisguiseObserversRpc(int disguiseIndex)
	{
		ApplyAppearance(disguiseIndex);
	}

	[ObserversRpc(ExcludeOwner = true)]
	private void PlayDisguiseParticlesObserversRpc()
	{
		PlayDisguiseParticles();
	}

	// =========================================================
	// Model selection
	// =========================================================

	private int PickRandomDisguiseIndex()
	{
		if (disguiseModels == null ||
			disguiseModels.Count == 0)
		{
			Debug.LogWarning(
				$"[{nameof(DisguiseItemNetworked)}] No disguise models assigned.",
				this);

			return -1;
		}

		List<int> validIndices = new();

		for (int i = 0; i < disguiseModels.Count; i++)
		{
			if (disguiseModels[i] != null)
				validIndices.Add(i);
		}

		if (validIndices.Count == 0)
			return -1;

		if (validIndices.Count == 1)
			return validIndices[0];

		int selectedIndex;

		do
		{
			selectedIndex =
				validIndices[
					Random.Range(0, validIndices.Count)];
		}
		while (selectedIndex == currentDisguiseIndex);

		return selectedIndex;
	}

	// =========================================================
	// Appearance
	// =========================================================

	private void ApplyAppearance(int disguiseIndex)
	{
		bool validDisguise =
			disguiseModels != null &&
			disguiseIndex >= 0 &&
			disguiseIndex < disguiseModels.Count &&
			disguiseModels[disguiseIndex] != null;

		// Hide only the specified renderers.
		SetDefaultRenderersEnabled(!validDisguise);

		if (disguiseModels == null)
			return;

		for (int i = 0; i < disguiseModels.Count; i++)
		{
			GameObject model = disguiseModels[i];

			if (model == null)
				continue;

			model.SetActive(
				validDisguise &&
				i == disguiseIndex);
		}
	}

	private void SetDefaultAppearance()
	{
		SetDefaultRenderersEnabled(true);

		if (disguiseModels == null)
			return;

		for (int i = 0; i < disguiseModels.Count; i++)
		{
			if (disguiseModels[i] != null)
				disguiseModels[i].SetActive(false);
		}
	}

	private void SetDefaultRenderersEnabled(bool enabled)
	{
		if (defaultCharacterRenderers == null)
			return;

		for (int i = 0; i < defaultCharacterRenderers.Count; i++)
		{
			Renderer renderer = defaultCharacterRenderers[i];

			if (renderer != null)
				renderer.enabled = enabled;
		}
	}

	// =========================================================
	// Effects
	// =========================================================

	private void PlayDisguiseParticles()
	{
		if (disguiseParticles == null)
			return;

		for (int i = 0; i < disguiseParticles.Count; i++)
		{
			ParticleSystem particles = disguiseParticles[i];

			if (particles == null)
				continue;

			particles.Stop(
				true,
				ParticleSystemStopBehavior.StopEmittingAndClear);

			particles.Play(true);
		}
	}
}