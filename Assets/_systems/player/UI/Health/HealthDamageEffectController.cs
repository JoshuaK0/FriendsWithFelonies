using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.DebugUI;

public class HealthDamageEffectController : MonoBehaviour
{
	[SerializeField] HealthManager healthManager;
	[SerializeField] float fadeSpeed = 1f;
	[SerializeField] float damageScale = 0.05f;
	[SerializeField, Range(0f, 1f)] float healthDamageIntensity;

	[SerializeField] private UnityEngine.Rendering.Volume volume;

	public float HealthDamageIntensity => healthDamageIntensity;

	void OnEnable()
	{
		if (healthManager != null)
			healthManager.OnHealthModify += OnHealthModified;
	}

	void OnDisable()
	{
		if (healthManager != null)
			healthManager.OnHealthModify -= OnHealthModified;
	}

	void Update()
	{
		if (healthDamageIntensity > 0f)
		{
			healthDamageIntensity = Mathf.MoveTowards(healthDamageIntensity, 0f, fadeSpeed * Time.deltaTime);

			volume.weight = healthDamageIntensity;

		}
	}

	void OnHealthModified(float changeAmount, Vector3 sourcePos, Vector3 targetPos, MonoBehaviour sourceComponent)
	{
		if (changeAmount >= 0f)
			return;

		float damage = -changeAmount;
		float delta = damage * damageScale;
		healthDamageIntensity = Mathf.Clamp01(healthDamageIntensity + delta);

	}
}
