using System.Collections.Generic;
using UnityEngine;

public class StarSpawner : MonoBehaviour
{
	[Header("Star Prefab")]
	[SerializeField] private GameObject starPrefab;

	[Header("Spawn Settings")]
	[SerializeField] private int numberOfStars = 50;

	[SerializeField] private float minRadius = 50f;
	[SerializeField] private float maxRadius = 100f;

	[Range(0f, 180f)]
	[SerializeField] private float topAngle = 90f;

	[Header("Spacing")]
	[Tooltip("Minimum spacing when two stars both have a size of 1.")]
	[SerializeField] private float minimumStarDistance = 5f;

	[Tooltip("Maximum attempts to find a valid position for each star.")]
	[SerializeField] private int maxSpawnAttempts = 100;

	[Header("Star Size")]
	[SerializeField] private float minSize = 0.5f;
	[SerializeField] private float maxSize = 1.5f;

	[Tooltip(
		"X = normalized size (0 = minSize, 1 = maxSize)\n" +
		"Y = probability / rarity"
	)]
	[SerializeField]
	private AnimationCurve sizeRarityCurve =
		AnimationCurve.Linear(0f, 1f, 1f, 1f);

	[Header("Star Rotation")]
	[Range(0f, 180f)]
	[SerializeField] private float maxRotationAngle = 15f;

	[Header("Generation")]
	[SerializeField] private bool generateOnStart = true;

	private Transform generatedStarsRoot;

	private readonly List<SpawnedStar> spawnedStars = new();

	private struct SpawnedStar
	{
		public Vector3 position;
		public float size;

		public SpawnedStar(Vector3 position, float size)
		{
			this.position = position;
			this.size = size;
		}
	}

	private void Start()
	{
		if (generateOnStart)
			GenerateStars();
	}

	[ContextMenu("Generate Stars")]
	public void GenerateStars()
	{
		if (starPrefab == null)
		{
			Debug.LogError("StarSpawner: No starPrefab assigned!");
			return;
		}

		ClearStars();

		GameObject root = new GameObject("Generated Stars");
		root.transform.SetParent(transform);
		root.transform.localPosition = Vector3.zero;
		root.transform.localRotation = Quaternion.identity;

		generatedStarsRoot = root.transform;

		spawnedStars.Clear();

		for (int i = 0; i < numberOfStars; i++)
		{
			// Size is chosen BEFORE position because spacing depends on it.
			float normalizedSize = GetRandomSizeFromCurve();

			float size = Mathf.Lerp(
				minSize,
				maxSize,
				normalizedSize
			);

			bool foundPosition = false;
			Vector3 spawnPosition = Vector3.zero;

			for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
			{
				Vector3 candidate = GetRandomSpawnPosition();

				if (IsPositionValid(candidate, size))
				{
					spawnPosition = candidate;
					foundPosition = true;
					break;
				}
			}

			if (!foundPosition)
			{
				Debug.LogWarning(
					$"StarSpawner: Could not find space for star {i + 1}."
				);

				continue;
			}

			SpawnStar(spawnPosition, size);

			spawnedStars.Add(
				new SpawnedStar(spawnPosition, size)
			);
		}
	}

	private Vector3 GetRandomSpawnPosition()
	{
		float theta = Random.Range(0f, topAngle);
		float phi = Random.Range(0f, 360f);
		float radius = Random.Range(minRadius, maxRadius);

		float thetaRad = theta * Mathf.Deg2Rad;
		float phiRad = phi * Mathf.Deg2Rad;

		float x =
			radius *
			Mathf.Sin(thetaRad) *
			Mathf.Cos(phiRad);

		float z =
			radius *
			Mathf.Sin(thetaRad) *
			Mathf.Sin(phiRad);

		float y =
			radius *
			Mathf.Cos(thetaRad);

		return transform.position + new Vector3(x, y, z);
	}

	private bool IsPositionValid(
		Vector3 candidatePosition,
		float candidateSize)
	{
		foreach (SpawnedStar existingStar in spawnedStars)
		{
			// Average the two star sizes.
			// Two size-1 stars use exactly minimumStarDistance.
			float sizeMultiplier =
				(candidateSize + existingStar.size) * 0.5f;

			float requiredDistance =
				minimumStarDistance * sizeMultiplier;

			float requiredDistanceSquared =
				requiredDistance * requiredDistance;

			float actualDistanceSquared =
				(candidatePosition - existingStar.position)
				.sqrMagnitude;

			if (actualDistanceSquared < requiredDistanceSquared)
				return false;
		}

		return true;
	}

	private void SpawnStar(Vector3 position, float size)
	{
		GameObject star = Instantiate(
			starPrefab,
			position,
			Quaternion.identity,
			generatedStarsRoot
		);

		// Face the center.
		star.transform.LookAt(transform.position);

		// Random tilt.
		float randomX = Random.Range(
			-maxRotationAngle,
			maxRotationAngle
		);

		float randomY = Random.Range(
			-maxRotationAngle,
			maxRotationAngle
		);

		// Full random roll.
		float randomZ = Random.Range(0f, 360f);

		star.transform.Rotate(
			randomX,
			randomY,
			randomZ,
			Space.Self
		);

		// Preserve prefab proportions.
		star.transform.localScale *= size;
	}

	private float GetRandomSizeFromCurve()
	{
		const int maxAttempts = 100;

		for (int i = 0; i < maxAttempts; i++)
		{
			float x = Random.value;

			float probability = Mathf.Clamp01(
				sizeRarityCurve.Evaluate(x)
			);

			if (Random.value <= probability)
				return x;
		}

		return Random.value;
	}

	[ContextMenu("Clear Stars")]
	public void ClearStars()
	{
		Transform existingRoot = transform.Find("Generated Stars");

		if (existingRoot != null)
		{
			if (Application.isPlaying)
				Destroy(existingRoot.gameObject);
			else
				DestroyImmediate(existingRoot.gameObject);
		}

		generatedStarsRoot = null;
		spawnedStars.Clear();
	}
}