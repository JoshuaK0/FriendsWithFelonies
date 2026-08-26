using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

public sealed class NpcTrafficSystem : NetworkBehaviour
{
    [Header("NPC")]
    [SerializeField] private NetworkObject npcPrefab;

    [Header("Population")]
    [SerializeField, Min(0)] private int maxNpcCount = 10;

    [Header("Spawn Timing")]
    [SerializeField, Min(0f)] private float minSpawnInterval = 2f;
    [SerializeField, Min(0f)] private float maxSpawnInterval = 5f;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Points Of Interest")]
    [SerializeField] private List<Transform> pointsOfInterest = new();

    [Header("Exit Points")]
    [SerializeField] private List<Transform> exitPoints = new();

    [Header("NPC Route")]
    [SerializeField, Min(0)] private int minPointsPerNpc = 2;
    [SerializeField, Min(0)] private int maxPointsPerNpc = 5;

    [Header("Waiting At Points")]
    [SerializeField, Min(0f)] private float minWaitDuration = 1f;
    [SerializeField, Min(0f)] private float maxWaitDuration = 5f;

    [Header("Navigation")]
    [Tooltip("How far from a Transform the system may search for valid NavMesh.")]
    [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 2f;

    private readonly HashSet<NpcWanderAgent> activeNpcs = new();

    private Coroutine spawnRoutine;

    public int ActiveNpcCount => activeNpcs.Count;

    public override void OnStartServer()
    {
        base.OnStartServer();

        GameFlowManager.Instance.OnRoundStarted += StartSpawnRoutine;
    }

    void StartSpawnRoutine(int round)
    {
		spawnRoutine = StartCoroutine(SpawnRoutine());
	}

	public override void OnStopServer()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        activeNpcs.Clear();

        base.OnStopServer();
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (activeNpcs.Count < maxNpcCount)
                TrySpawnNpc();

            float delay = Random.Range(
                Mathf.Min(minSpawnInterval, maxSpawnInterval),
                Mathf.Max(minSpawnInterval, maxSpawnInterval));

            yield return new WaitForSeconds(delay);
        }
    }

    private void TrySpawnNpc()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning(
                $"[{nameof(NpcTrafficSystem)}] No NPC prefab assigned.",
                this);

            return;
        }

        Transform spawnPoint = GetRandomValidTransform(spawnPoints);

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                $"[{nameof(NpcTrafficSystem)}] No valid spawn points assigned.",
                this);

            return;
        }

        if (pointsOfInterest.Count == 0)
        {
            Debug.LogWarning(
                $"[{nameof(NpcTrafficSystem)}] No points of interest assigned.",
                this);

            return;
        }

        if (exitPoints.Count == 0)
        {
            Debug.LogWarning(
                $"[{nameof(NpcTrafficSystem)}] No exit points assigned.",
                this);

            return;
        }

        if (!NavMesh.SamplePosition(
                spawnPoint.position,
                out NavMeshHit navHit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"[{nameof(NpcTrafficSystem)}] Could not find NavMesh near " +
                $"spawn point '{spawnPoint.name}'.",
                spawnPoint);

            return;
        }

        NetworkObject npc = Instantiate(
            npcPrefab,
            navHit.position,
            spawnPoint.rotation);

        Spawn(npc);

        NpcWanderAgent wanderAgent =
            npc.GetComponent<NpcWanderAgent>();

        if (wanderAgent == null)
        {
            Debug.LogError(
                $"[{nameof(NpcTrafficSystem)}] NPC prefab '{npc.name}' " +
                $"does not have {nameof(NpcWanderAgent)}.",
                npc);

            npc.Despawn();
            return;
        }

        activeNpcs.Add(wanderAgent);

        wanderAgent.InitializeServer(
            this,
            pointsOfInterest,
            exitPoints,
            minPointsPerNpc,
            maxPointsPerNpc,
            minWaitDuration,
            maxWaitDuration,
            navMeshSampleRadius);
    }

    internal void NotifyNpcDespawned(NpcWanderAgent npc)
    {
        if (npc == null)
            return;

        activeNpcs.Remove(npc);
    }

    private Transform GetRandomValidTransform(List<Transform> transforms)
    {
        if (transforms == null || transforms.Count == 0)
            return null;

        for (int i = 0; i < transforms.Count; i++)
        {
            Transform candidate =
                transforms[Random.Range(0, transforms.Count)];

            if (candidate != null)
                return candidate;
        }

        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i] != null)
                return transforms[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxSpawnInterval =
            Mathf.Max(minSpawnInterval, maxSpawnInterval);

        maxPointsPerNpc =
            Mathf.Max(minPointsPerNpc, maxPointsPerNpc);

        maxWaitDuration =
            Mathf.Max(minWaitDuration, maxWaitDuration);
    }
#endif
}
