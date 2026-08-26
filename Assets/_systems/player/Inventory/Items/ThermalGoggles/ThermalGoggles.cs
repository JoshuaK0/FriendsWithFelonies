using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Owner-local thermal vision input and configuration.
///
/// The persistent runtime controller is attached to the inventory, so thermal
/// vision and its battery continue to work when the held prefab is rebuilt.
/// Renderer features are local to the running client and do not require
/// FishNet replication.
/// </summary>
public sealed class ThermalGoggles : HotbarHeldItem
{
	[Header("Thermal Vision")]
	[SerializeField]
	private UniversalRendererData rendererData;

	[SerializeField]
	private string featureName = "Outline1";

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	[SerializeField]
	private AudioClip toggleClip;

	[Header("Battery")]
	[SerializeField, Min(0.01f)]
	private float duration = 10f;

	[SerializeField, Min(0f)]
	private float consumeRate = 1f;

	private ThermalVisionRuntimeController runtimeController;
	private GameObject screenFx;
	private Slider slider;

	private void Start()
	{
		ResolvePlayerOutputs();
	}

	protected override void OnContextInitialized()
	{
		if (Inventory == null)
			return;

		runtimeController =
			Inventory.GetComponent
				<ThermalVisionRuntimeController>();

		if (runtimeController == null)
		{
			runtimeController =
				Inventory.gameObject.AddComponent
					<ThermalVisionRuntimeController>();
		}

		ResolvePlayerOutputs();

		runtimeController.Configure(
			ItemId,
			RuntimeState,
			rendererData,
			featureName,
			duration,
			consumeRate,
			screenFx,
			slider
		);
	}

	protected override void OnEquipped()
	{
		ResolvePlayerOutputs();

		// Equipping does not change the current thermal-vision state.
		runtimeController?.SetOutputReferences(
			screenFx,
			slider
		);
	}

	protected override void OnEquippedUpdate()
	{
		if (!Input.GetMouseButtonDown(0))
			return;

		if (runtimeController == null)
			return;

		bool stateChanged =
			runtimeController.TryToggle();

		if (!stateChanged)
			return;

		if (audioSource != null &&
			toggleClip != null)
		{
			audioSource.PlayOneShot(toggleClip);
		}
	}

	protected override void OnUnequipped()
	{
		// Keep thermal vision and its HUD output active after unequipping.
		runtimeController?.SaveState();
	}

	private bool ResolvePlayerOutputs()
	{
		if (MyClient.Instance == null ||
			MyClient.Instance.PlayerManager == null ||
			MyClient.Instance.PlayerManager
				.LocalPlayerController == null)
		{
			return false;
		}

		PlayerCharacter playerCharacter =
			MyClient.Instance.PlayerManager
				.LocalPlayerController
				.GetComponent<PlayerCharacter>();

		if (playerCharacter == null)
			return false;

		slider =
			playerCharacter.GetThermalSlider();

		screenFx =
			playerCharacter.GetThermalFX();

		runtimeController?.SetOutputReferences(
			screenFx,
			slider
		);

		return slider != null ||
			screenFx != null;
	}
}
