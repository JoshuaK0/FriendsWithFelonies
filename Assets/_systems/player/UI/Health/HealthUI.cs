using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HealthUI : MonoBehaviour
{
	[Header("Source")]
	[SerializeField]
	private HealthManager healthManager;

	[Header("UI")]
	[SerializeField]
	private Slider healthSlider;

	[SerializeField]
	private TextMeshProUGUI healthText;

	[SerializeField]
	private GameObject deadIndicator;

	[Header("Display")]
	[SerializeField]
	private bool showMaximumHealth = true;

	private bool isSubscribed;

	private void Awake()
	{
		if (healthManager == null)
			healthManager = GetComponentInParent<HealthManager>();
	}

	private void OnEnable()
	{
		Subscribe();
		Refresh();
	}

	private void OnDisable()
	{
		Unsubscribe();
	}

	private void Subscribe()
	{
		if (healthManager == null || isSubscribed)
			return;

		healthManager.OnHealthChanged += HandleHealthChanged;
		healthManager.OnDied += HandleDied;

		isSubscribed = true;
	}

	private void Unsubscribe()
	{
		if (healthManager == null || !isSubscribed)
			return;

		healthManager.OnHealthChanged -= HandleHealthChanged;
		healthManager.OnDied -= HandleDied;

		isSubscribed = false;
	}

	private void Refresh()
	{
		if (healthManager == null)
		{
			Debug.LogWarning(
				$"{nameof(HealthUI)} on {name} has no " +
				$"{nameof(HealthManager)} assigned.",
				this);

			return;
		}

		UpdateDisplay(
			healthManager.CurrentHealth,
			healthManager.MaxHealth);
	}

	private void HandleHealthChanged(
		float currentHealth,
		float maximumHealth)
	{
		UpdateDisplay(
			currentHealth,
			maximumHealth);
	}

	private void HandleDied()
	{
		if (deadIndicator != null)
			deadIndicator.SetActive(true);
	}

	private void UpdateDisplay(
		float currentHealth,
		float maximumHealth)
	{
		currentHealth = Mathf.Max(0f, currentHealth);
		maximumHealth = Mathf.Max(1f, maximumHealth);

		if (healthSlider != null)
		{
			healthSlider.minValue = 0f;
			healthSlider.maxValue = maximumHealth;
			healthSlider.value = currentHealth;
		}

		if (healthText != null)
		{
			int displayedHealth =
				Mathf.CeilToInt(currentHealth);

			if (showMaximumHealth)
			{
				healthText.text =
					$"{displayedHealth} / " +
					$"{Mathf.CeilToInt(maximumHealth)}";
			}
			else
			{
				healthText.text =
					displayedHealth.ToString();
			}
		}

		if (deadIndicator != null)
		{
			deadIndicator.SetActive(
				currentHealth <= 0f);
		}
	}
}