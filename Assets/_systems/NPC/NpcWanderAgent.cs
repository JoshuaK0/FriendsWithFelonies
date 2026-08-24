using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class NpcWanderAgent : NetworkBehaviour
{
	[Header("References")]
	[SerializeField] private NavMeshAgent navMeshAgent;

	[Header("NPC Models")]
	[Tooltip("One of these objects will be randomly enabled for each NPC.")]
	[SerializeField] private List<GameObject> npcModels = new();

	[Header("Movement")]
	[Tooltip("Extra distance from stoppingDistance that counts as arriving.")]
	[SerializeField, Min(0f)] private float arrivalTolerance = 0.1f;

	private NpcTrafficSystem trafficSystem;

	private readonly List<Transform> route = new();
	private List<Transform> exitPoints;

	private float minWaitDuration;
	private float maxWaitDuration;
	private float navMeshSampleRadius;

	private Coroutine serverRoutine;

	private bool lastMoveSucceeded;

	/*
     * The server randomly chooses this value.
     *
     * FishNet then synchronizes it to every client so everyone
     * sees the same NPC model.
     *
     * -1 means no model selected yet.
     */
	private readonly SyncVar<int> selectedModelIndex =
		new(-1);

	private void Awake()
	{
		if (navMeshAgent == null)
			navMeshAgent = GetComponent<NavMeshAgent>();

		selectedModelIndex.OnChange += OnModelIndexChanged;

		DisableAllModels();
	}

	private void OnDestroy()
	{
		selectedModelIndex.OnChange -= OnModelIndexChanged;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		ChooseRandomModel();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		/*
         * Apply the currently synchronized model.
         *
         * This is especially useful for late joiners.
         */
		ApplyModel(selectedModelIndex.Value);

		/*
         * NavMesh movement is server authoritative.
         *
         * Remote clients should not run their own NavMeshAgent.
         */
		if (!IsServerInitialized && navMeshAgent != null)
			navMeshAgent.enabled = false;
	}

	private void ChooseRandomModel()
	{
		if (!IsServerInitialized)
			return;

		List<int> validIndices = new();

		for (int i = 0; i < npcModels.Count; i++)
		{
			if (npcModels[i] != null)
				validIndices.Add(i);
		}

		if (validIndices.Count == 0)
		{
			selectedModelIndex.Value = -1;

			Debug.LogWarning(
				$"[{nameof(NpcWanderAgent)}] No NPC models assigned.",
				this);

			return;
		}

		selectedModelIndex.Value =
			validIndices[Random.Range(0, validIndices.Count)];

		ApplyModel(selectedModelIndex.Value);
	}

	private void OnModelIndexChanged(
		int previous,
		int next,
		bool asServer)
	{
		ApplyModel(next);
	}

	private void ApplyModel(int modelIndex)
	{
		for (int i = 0; i < npcModels.Count; i++)
		{
			GameObject model = npcModels[i];

			if (model == null)
				continue;

			model.SetActive(i == modelIndex);
		}
	}

	private void DisableAllModels()
	{
		for (int i = 0; i < npcModels.Count; i++)
		{
			if (npcModels[i] != null)
				npcModels[i].SetActive(false);
		}
	}

	public void InitializeServer(
		NpcTrafficSystem trafficSystem,
		List<Transform> pointsOfInterest,
		List<Transform> exitPoints,
		int minPoints,
		int maxPoints,
		float minWaitDuration,
		float maxWaitDuration,
		float navMeshSampleRadius)
	{
		if (!IsServerInitialized)
			return;

		this.trafficSystem = trafficSystem;
		this.exitPoints = exitPoints;

		this.minWaitDuration =
			Mathf.Min(minWaitDuration, maxWaitDuration);

		this.maxWaitDuration =
			Mathf.Max(minWaitDuration, maxWaitDuration);

		this.navMeshSampleRadius = navMeshSampleRadius;

		BuildRoute(
			pointsOfInterest,
			minPoints,
			maxPoints);

		serverRoutine = StartCoroutine(ServerBehaviour());
	}

	private void BuildRoute(
		List<Transform> pointsOfInterest,
		int minPoints,
		int maxPoints)
	{
		route.Clear();

		List<Transform> validPoints = new();

		for (int i = 0; i < pointsOfInterest.Count; i++)
		{
			if (pointsOfInterest[i] != null)
				validPoints.Add(pointsOfInterest[i]);
		}

		if (validPoints.Count == 0)
			return;

		int minimum = Mathf.Min(minPoints, maxPoints);
		int maximum = Mathf.Max(minPoints, maxPoints);

		int pointCount =
			Random.Range(minimum, maximum + 1);

		/*
         * If there is only one POI, we cannot visit it multiple
         * times without creating consecutive duplicates.
         */
		if (validPoints.Count == 1)
		{
			if (pointCount > 0)
				route.Add(validPoints[0]);

			return;
		}

		Transform previousPoint = null;

		for (int i = 0; i < pointCount; i++)
		{
			Transform nextPoint;

			do
			{
				nextPoint =
					validPoints[
						Random.Range(0, validPoints.Count)];
			}
			while (nextPoint == previousPoint);

			route.Add(nextPoint);

			previousPoint = nextPoint;
		}
	}

	private IEnumerator ServerBehaviour()
	{
		/*
         * Visit each POI.
         */
		for (int i = 0; i < route.Count; i++)
		{
			Transform point = route[i];

			yield return MoveTo(point);

			if (!lastMoveSucceeded)
				continue;

			float waitDuration =
				Random.Range(
					minWaitDuration,
					maxWaitDuration);

			yield return new WaitForSeconds(waitDuration);
		}

		/*
         * Finished visiting POIs.
         * Pick a random exit.
         */
		Transform exit = GetRandomExit();

		if (exit != null)
		{
			yield return MoveTo(exit);

			if (!lastMoveSucceeded)
			{
				Debug.LogWarning(
					$"[{nameof(NpcWanderAgent)}] NPC could not reach " +
					$"exit '{exit.name}'. Despawning anyway.",
					this);
			}
		}

		Despawn();
	}

	private IEnumerator MoveTo(Transform target)
	{
		lastMoveSucceeded = false;

		if (target == null)
			yield break;

		if (navMeshAgent == null ||
			!navMeshAgent.enabled ||
			!navMeshAgent.isOnNavMesh)
		{
			Debug.LogWarning(
				$"[{nameof(NpcWanderAgent)}] NPC is not on the NavMesh.",
				this);

			yield break;
		}

		if (!NavMesh.SamplePosition(
				target.position,
				out NavMeshHit hit,
				navMeshSampleRadius,
				NavMesh.AllAreas))
		{
			Debug.LogWarning(
				$"[{nameof(NpcWanderAgent)}] Could not find NavMesh near " +
				$"'{target.name}'.",
				target);

			yield break;
		}

		bool destinationAccepted =
			navMeshAgent.SetDestination(hit.position);

		if (!destinationAccepted)
		{
			Debug.LogWarning(
				$"[{nameof(NpcWanderAgent)}] NavMeshAgent rejected " +
				$"destination '{target.name}'.",
				target);

			yield break;
		}

		while (navMeshAgent.pathPending)
			yield return null;

		if (!navMeshAgent.isOnNavMesh)
			yield break;

		if (navMeshAgent.pathStatus !=
			NavMeshPathStatus.PathComplete)
		{
			Debug.LogWarning(
				$"[{nameof(NpcWanderAgent)}] No complete path to " +
				$"'{target.name}'.",
				target);

			yield break;
		}

		while (true)
		{
			if (!navMeshAgent.enabled ||
				!navMeshAgent.isOnNavMesh)
			{
				yield break;
			}

			if (navMeshAgent.pathPending)
			{
				yield return null;
				continue;
			}

			if (navMeshAgent.pathStatus !=
				NavMeshPathStatus.PathComplete)
			{
				yield break;
			}

			float arrivalDistance =
				navMeshAgent.stoppingDistance +
				arrivalTolerance;

			if (navMeshAgent.remainingDistance <=
				arrivalDistance)
			{
				break;
			}

			yield return null;
		}

		navMeshAgent.ResetPath();

		lastMoveSucceeded = true;
	}

	private Transform GetRandomExit()
	{
		if (exitPoints == null ||
			exitPoints.Count == 0)
		{
			return null;
		}

		List<Transform> validExits = new();

		for (int i = 0; i < exitPoints.Count; i++)
		{
			if (exitPoints[i] != null)
				validExits.Add(exitPoints[i]);
		}

		if (validExits.Count == 0)
			return null;

		return validExits[
			Random.Range(0, validExits.Count)];
	}

	public override void OnStopServer()
	{
		if (serverRoutine != null)
		{
			StopCoroutine(serverRoutine);
			serverRoutine = null;
		}

		trafficSystem?.NotifyNpcDespawned(this);

		base.OnStopServer();
	}
}