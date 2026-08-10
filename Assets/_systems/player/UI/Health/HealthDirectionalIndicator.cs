using System.Collections.Generic;
using UnityEngine;

public class HealthDirectionalIndicator : MonoBehaviour
{
	[SerializeField] HealthManager healthManager;
	[SerializeField] Camera targetCamera;
	[SerializeField] RectTransform canvasRect;
	[SerializeField] OffscreenIndicator indicatorPrefab;
	[SerializeField] int poolSize = 8;
	[SerializeField] float lifePerDamage = 0.05f;
	[SerializeField] float minLifetime = 0.25f;
	[SerializeField] float maxLifetime = 2f;

	[SerializeField] bool useCameraDirection = true;
	[SerializeField] Transform directionReference;
	[SerializeField] float indicatorLerpSpeed = 0f;

	class PooledIndicator
	{
		public OffscreenIndicator offscreen;
		public float timeRemaining;
		public Vector3 sourcePosition;
		public MonoBehaviour sourceComponent;
	}

	readonly List<PooledIndicator> pool = new List<PooledIndicator>();
	readonly Dictionary<MonoBehaviour, PooledIndicator> activeBySource = new Dictionary<MonoBehaviour, PooledIndicator>();

	void Awake()
	{
		if (healthManager == null) healthManager = GetComponent<HealthManager>();
		if (targetCamera == null) targetCamera = Camera.main;
		CreatePool();
	}

	void OnEnable()
	{
		if (healthManager != null) healthManager.OnHealthModify += HandleHealthModify;
	}

	void OnDisable()
	{
		if (healthManager != null) healthManager.OnHealthModify -= HandleHealthModify;
	}

	void CreatePool()
	{
		if (indicatorPrefab == null || canvasRect == null) return;

		pool.Clear();
		activeBySource.Clear();

		for (int i = 0; i < poolSize; i++)
		{
			OffscreenIndicator instance = Instantiate(indicatorPrefab, canvasRect);
			instance.Initialize(Vector3.zero, targetCamera, canvasRect);
			instance.forceAlwaysOn = true;
			instance.useCameraDirection = useCameraDirection;
			instance.directionReference = directionReference;
			instance.moveLerpSpeed = indicatorLerpSpeed;
			instance.SetIndicatorActive(false);

			PooledIndicator p = new PooledIndicator();
			p.offscreen = instance;
			p.timeRemaining = 0f;
			p.sourcePosition = Vector3.zero;
			p.sourceComponent = null;
			pool.Add(p);
		}
	}

	void HandleHealthModify(float changeAmount, Vector3 sourcePosition, Vector3 targetPosition, MonoBehaviour sourceComponent)
	{
		if (targetCamera == null) return;
		if (changeAmount >= 0f) return;

		float damage = -changeAmount;
		float addLifetime = damage * lifePerDamage;

		if (sourceComponent != null && activeBySource.TryGetValue(sourceComponent, out PooledIndicator existing))
		{
			existing.sourcePosition = sourcePosition;
			existing.timeRemaining = Mathf.Clamp(existing.timeRemaining + addLifetime, minLifetime, maxLifetime);
			existing.offscreen.targetCamera = targetCamera;
			existing.offscreen.canvasRect = canvasRect;
			existing.offscreen.forceAlwaysOn = true;
			existing.offscreen.useCameraDirection = useCameraDirection;
			existing.offscreen.directionReference = directionReference;
			existing.offscreen.moveLerpSpeed = indicatorLerpSpeed;
			existing.offscreen.SetTargetPosition(sourcePosition);
			existing.offscreen.SetIndicatorActive(true);
			return;
		}

		float lifetime = Mathf.Clamp(addLifetime, minLifetime, maxLifetime);

		PooledIndicator indicator = GetFreeIndicator();
		if (indicator == null && pool.Count > 0) indicator = pool[0];
		if (indicator == null) return;

		if (indicator.sourceComponent != null && activeBySource.TryGetValue(indicator.sourceComponent, out PooledIndicator mapped) && mapped == indicator)
		{
			activeBySource.Remove(indicator.sourceComponent);
		}

		indicator.sourcePosition = sourcePosition;
		indicator.timeRemaining = lifetime;
		indicator.sourceComponent = sourceComponent;

		if (sourceComponent != null)
		{
			activeBySource[sourceComponent] = indicator;
		}

		indicator.offscreen.targetCamera = targetCamera;
		indicator.offscreen.canvasRect = canvasRect;
		indicator.offscreen.forceAlwaysOn = true;
		indicator.offscreen.useCameraDirection = useCameraDirection;
		indicator.offscreen.directionReference = directionReference;
		indicator.offscreen.moveLerpSpeed = indicatorLerpSpeed;
		indicator.offscreen.SetTargetPosition(sourcePosition);
		indicator.offscreen.SetIndicatorActive(true);
	}

	PooledIndicator GetFreeIndicator()
	{
		for (int i = 0; i < pool.Count; i++)
		{
			if (pool[i].timeRemaining <= 0f && !pool[i].offscreen.IsIndicatorActive) return pool[i];
		}
		return null;
	}

	void Update()
	{
		for (int i = 0; i < pool.Count; i++)
		{
			PooledIndicator p = pool[i];
			if (p.timeRemaining > 0f)
			{
				p.timeRemaining -= Time.deltaTime;
				if (p.timeRemaining <= 0f)
				{
					p.timeRemaining = 0f;
					p.offscreen.SetIndicatorActive(false);
					if (p.sourceComponent != null && activeBySource.TryGetValue(p.sourceComponent, out PooledIndicator mapped) && mapped == p)
					{
						activeBySource.Remove(p.sourceComponent);
					}
					p.sourceComponent = null;
				}
				else
				{
					p.offscreen.useCameraDirection = useCameraDirection;
					p.offscreen.directionReference = directionReference;
					p.offscreen.moveLerpSpeed = indicatorLerpSpeed;
					p.offscreen.SetTargetPosition(p.sourcePosition);
				}
			}
		}
	}
}
