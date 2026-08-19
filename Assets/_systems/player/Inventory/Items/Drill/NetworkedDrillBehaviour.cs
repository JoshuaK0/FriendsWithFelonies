using EzySlice;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkedDrillBehaviour : NetworkBehaviour
{
	[Header("Drilling")]
	[SerializeField] private LayerMask wallLayers;
	[SerializeField] private string wallLayerName = "Default";
	[SerializeField, Min(0f)] private float timeToDrill = 5f;
	[SerializeField] private Rigidbody rb;

	[Tooltip("Area used to detect which walls should be sliced.")]
	[SerializeField] private Vector3 drillArea = Vector3.one;

	[Header("Polygon Hole")]
	[Tooltip(
		"Number of sides on the hole. " +
		"3 = triangle, 4 = square, 6 = hexagon, 7 = heptagon, etc.")]
	[SerializeField, Min(3)]
	private int polygonSides = 4;

	[Tooltip(
		"Distance from the centre of the hole " +
		"to each polygon vertex.")]
	[SerializeField, Min(0.01f)]
	private float polygonRadius = 1f;

	[Tooltip(
		"Rotation of the polygon around the drill's " +
		"local forward axis.")]
	[SerializeField, Range(0f, 360f)]
	private float polygonRotation = 45f;

	[Header("Cut Depth")]
	[Tooltip(
		"Total depth of the cut along the drill's forward axis. " +
		"The front and back planes are each placed at half " +
		"this distance from the drill transform.")]
	[SerializeField, Min(0.01f)]
	private float cutWidth = 0.5f;

	[Header("Generated Planes")]
	[SerializeField]
	private Transform generatedPlaneParent;

	[SerializeField]
	private List<Transform> generatedPlanes =
		new List<Transform>();

	[Header("Slicing")]
	[SerializeField]
	private Material crossSectionMaterial;

	[Header("Result")]
	[SerializeField]
	private GameObject platform;

	[SerializeField]
	private TextMeshProUGUI progressText;

	private float clientStartedAt;
	private bool completedLocally;

	private void Awake()
	{
		GenerateCutPlanes();
	}

	private void Reset()
	{
		rb = GetComponent<Rigidbody>();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		StartCoroutine(ServerDrillRoutine());
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		clientStartedAt = Time.time;
		completedLocally = false;
	}

	private void Update()
	{
		if (completedLocally)
			return;

		if (progressText == null)
			return;

		float elapsed =
			Time.time - clientStartedAt;

		float remaining =
			Mathf.Max(
				0f,
				timeToDrill - elapsed);

		progressText.text =
			remaining.ToString("0.0");
	}

	private IEnumerator ServerDrillRoutine()
	{
		if (timeToDrill > 0f)
		{
			yield return new WaitForSeconds(
				timeToDrill);
		}

		Vector3 drillingPosition =
			transform.position;

		Quaternion drillingRotation =
			transform.rotation;

		PerformDrillLocal(
			drillingPosition,
			drillingRotation);

		PerformDrillObserversRpc(
			drillingPosition,
			drillingRotation);
	}

	[ObserversRpc]
	private void PerformDrillObserversRpc(
		Vector3 drillingPosition,
		Quaternion drillingRotation)
	{
		// Host/server already performed the slice locally.
		if (IsServerInitialized)
			return;

		PerformDrillLocal(
			drillingPosition,
			drillingRotation);
	}

	private void PerformDrillLocal(
		Vector3 drillingPosition,
		Quaternion drillingRotation)
	{
		if (completedLocally)
			return;

		completedLocally = true;

		/*
		 * Rebuild the planes immediately before cutting
		 * so they match the current transform.
		 */
		GenerateCutPlanes();

		FindAndSliceWalls(
			drillingPosition,
			drillingRotation);

		if (rb != null)
		{
			rb.isKinematic = false;
			rb.useGravity = true;
		}

		if (platform != null)
		{
			Instantiate(
				platform,
				drillingPosition,
				drillingRotation);
		}

		if (progressText != null)
		{
			Destroy(
				progressText.gameObject);

			progressText = null;
		}
	}

	private void FindAndSliceWalls(
		Vector3 drillingPosition,
		Quaternion drillingRotation)
	{
		/*
		 * The overlap box is now centred directly on
		 * the drill transform.
		 *
		 * Previously this was offset by drillArea.z / 2.
		 */
		Vector3 boxCenter =
			drillingPosition;

		Vector3 overlapSize =
			drillArea;

		/*
		 * Make sure the overlap volume is at least as deep
		 * as the actual cutting volume.
		 */
		overlapSize.z =
			Mathf.Max(
				overlapSize.z,
				cutWidth);

		Collider[] wallColliders =
			Physics.OverlapBox(
				boxCenter,
				overlapSize * 0.5f,
				drillingRotation,
				wallLayers,
				QueryTriggerInteraction.Ignore);

		/*
		 * Prevent meshes with several colliders from
		 * being processed multiple times.
		 */
		Dictionary<GameObject, GameObject>
			wallsToSlice =
				new Dictionary<GameObject, GameObject>();

		foreach (Collider wallCollider in wallColliders)
		{
			if (wallCollider == null)
				continue;

			GameObject meshObject =
				FindMeshObject(
					wallCollider.gameObject);

			if (meshObject == null)
			{
				Debug.LogWarning(
					$"No MeshFilter found for " +
					$"'{wallCollider.name}'.",
					wallCollider);

				continue;
			}

			if (!wallsToSlice.ContainsKey(meshObject))
			{
				wallsToSlice.Add(
					meshObject,
					wallCollider.gameObject);
			}
		}

		foreach (
			KeyValuePair<GameObject, GameObject>
			wall in wallsToSlice)
		{
			SliceWall(
				wall.Key,
				wall.Value);
		}
	}

	private void SliceWall(
		GameObject originalMeshObject,
		GameObject colliderObject)
	{
		if (originalMeshObject == null)
			return;

		Transform originalParent =
			originalMeshObject.transform.parent;

		bool colliderIsPartOfMeshObject =
			colliderObject == originalMeshObject ||
			colliderObject.transform.IsChildOf(
				originalMeshObject.transform);

		/*
		 * Detach before manipulating the original
		 * hierarchy.
		 */
		originalMeshObject.transform.SetParent(
			null,
			true);

		GameObject pieceToContinueCutting =
			originalMeshObject;

		bool slicedAny = false;

		foreach (Transform plane in generatedPlanes)
		{
			if (plane == null)
				continue;

			if (pieceToContinueCutting == null)
				break;

			GameObject sourcePiece =
				pieceToContinueCutting;

			GameObject[] slicedObjects =
				Slice(
					sourcePiece,
					plane.position,
					plane.up,
					crossSectionMaterial);

			/*
			 * A failed cut should NOT stop the rest of
			 * the planes from being attempted.
			 */
			if (slicedObjects == null ||
				slicedObjects.Length < 2 ||
				slicedObjects[0] == null ||
				slicedObjects[1] == null)
			{
				Debug.LogWarning(
					$"Plane '{plane.name}' failed to slice " +
					$"'{sourcePiece.name}'. " +
					"Continuing to next plane.",
					plane);

				continue;
			}

			/*
			 * All plane normals point OUTWARD from
			 * the cutting volume.
			 *
			 * Upper hull:
			 *     Outside the cutting volume.
			 *     Keep it.
			 *
			 * Lower hull:
			 *     Inside the cutting volume.
			 *     Continue slicing it.
			 */
			GameObject upperHull =
				slicedObjects[0];

			GameObject lowerHull =
				slicedObjects[1];

			ConfigureWallPiece(
				upperHull);

			pieceToContinueCutting =
				lowerHull;

			/*
			 * EzySlice created new hull objects,
			 * so the source is no longer required.
			 */
			Destroy(
				sourcePiece);

			slicedAny = true;
		}

		if (!slicedAny)
		{
			/*
			 * No cuts succeeded.
			 * Restore the original object.
			 */
			if (originalMeshObject != null)
			{
				originalMeshObject.transform.SetParent(
					originalParent,
					true);
			}

			return;
		}

		/*
		 * The remaining lower hull is the portion
		 * inside the polygonal cutting prism.
		 *
		 * Delete it to create the hole.
		 */
		if (pieceToContinueCutting != null)
		{
			Destroy(
				pieceToContinueCutting);
		}

		if (!colliderIsPartOfMeshObject &&
			colliderObject != null)
		{
			Destroy(
				colliderObject);
		}
	}

	private GameObject FindMeshObject(
		GameObject root)
	{
		if (root == null)
			return null;

		MeshFilter meshFilter =
			root.GetComponent<MeshFilter>();

		if (meshFilter != null)
			return meshFilter.gameObject;

		meshFilter =
			root.GetComponentInChildren<MeshFilter>();

		if (meshFilter != null)
			return meshFilter.gameObject;

		meshFilter =
			root.GetComponentInParent<MeshFilter>();

		if (meshFilter != null)
			return meshFilter.gameObject;

		return null;
	}

	private void ConfigureWallPiece(
		GameObject piece)
	{
		if (piece == null)
			return;

		int wallLayer =
			LayerMask.NameToLayer(
				wallLayerName);

		if (wallLayer >= 0)
		{
			SetLayerRecursively(
				piece,
				wallLayer);
		}

		MeshFilter meshFilter =
			piece.GetComponent<MeshFilter>();

		MeshCollider meshCollider =
			piece.GetComponent<MeshCollider>();

		if (meshCollider == null)
		{
			meshCollider =
				piece.AddComponent<MeshCollider>();
		}

		if (meshFilter != null)
		{
			meshCollider.sharedMesh =
				meshFilter.sharedMesh;
		}
	}

	private void SetLayerRecursively(
		GameObject target,
		int layer)
	{
		target.layer = layer;

		foreach (Transform child in target.transform)
		{
			SetLayerRecursively(
				child.gameObject,
				layer);
		}
	}

	public GameObject[] Slice(
		GameObject objectToSlice,
		Vector3 planeWorldPosition,
		Vector3 planeWorldDirection,
		Material sectionMaterial)
	{
		if (objectToSlice == null)
			return null;

		return objectToSlice.SliceInstantiate(
			planeWorldPosition,
			planeWorldDirection,
			sectionMaterial);
	}

	// =========================================================
	// CUT VOLUME
	// =========================================================

	private Vector3 GetCutCenter()
	{
		/*
		 * The cut volume is centred EXACTLY on
		 * the drill transform.
		 */
		return transform.position;
	}

	[ContextMenu("Generate Cut Planes")]
	public void GenerateCutPlanes()
	{
		polygonSides =
			Mathf.Max(
				3,
				polygonSides);

		polygonRadius =
			Mathf.Max(
				0.01f,
				polygonRadius);

		cutWidth =
			Mathf.Max(
				0.01f,
				cutWidth);

		polygonRotation =
			Mathf.Repeat(
				polygonRotation,
				360f);

		CreatePlaneParentIfNeeded();

		ClearGeneratedPlanes();

		generatedPlanes.Clear();

		Vector3 cutCenter =
			GetCutCenter();

		/*
		 * Generate polygon sides first.
		 */
		GeneratePolygonSidePlanes(
			cutCenter);

		/*
		 * Then bound the polygon in depth.
		 */
		GenerateDepthPlanes(
			cutCenter);
	}

	private void GeneratePolygonSidePlanes(
		Vector3 cutCenter)
	{
		float angleStep =
			360f / polygonSides;

		for (int i = 0;
			i < polygonSides;
			i++)
		{
			float angleA =
				polygonRotation +
				angleStep * i;

			float angleB =
				polygonRotation +
				angleStep * (i + 1);

			Vector2 vertexA =
				GetPolygonPoint(
					angleA);

			Vector2 vertexB =
				GetPolygonPoint(
					angleB);

			Vector2 edgeCenter =
				(vertexA + vertexB) *
				0.5f;

			/*
			 * Because this is a regular polygon centred
			 * on zero, the edge midpoint direction
			 * points directly outward.
			 */
			Vector2 outward2D =
				edgeCenter.normalized;

			Vector3 worldPosition =
				cutCenter +
				transform.right *
					edgeCenter.x +
				transform.up *
					edgeCenter.y;

			Vector3 worldNormal =
				(
					transform.right *
						outward2D.x +
					transform.up *
						outward2D.y
				).normalized;

			CreateCutPlane(
				$"Side Cut Plane {i + 1}",
				worldPosition,
				worldNormal);
		}
	}

	private void GenerateDepthPlanes(
		Vector3 cutCenter)
	{
		float halfWidth =
			cutWidth * 0.5f;

		/*
		 * FRONT
		 *
		 * Exactly half cutWidth behind the centre.
		 */
		Vector3 frontPosition =
			cutCenter -
			transform.forward *
				halfWidth;

		Vector3 frontNormal =
			-transform.forward;

		CreateCutPlane(
			"Front Cut Plane",
			frontPosition,
			frontNormal);

		/*
		 * BACK
		 *
		 * Exactly half cutWidth in front of the centre.
		 */
		Vector3 backPosition =
			cutCenter +
			transform.forward *
				halfWidth;

		Vector3 backNormal =
			transform.forward;

		CreateCutPlane(
			"Back Cut Plane",
			backPosition,
			backNormal);
	}

	private void CreateCutPlane(
		string planeName,
		Vector3 worldPosition,
		Vector3 worldNormal)
	{
		GameObject planeObject =
			new GameObject(
				planeName);

		Transform plane =
			planeObject.transform;

		plane.SetParent(
			generatedPlaneParent,
			true);

		plane.position =
			worldPosition;

		/*
		 * EzySlice uses Transform.up as
		 * the plane normal.
		 */
		plane.rotation =
			Quaternion.FromToRotation(
				Vector3.up,
				worldNormal.normalized);

		generatedPlanes.Add(
			plane);
	}

	private Vector2 GetPolygonPoint(
		float angleDegrees)
	{
		float radians =
			angleDegrees *
			Mathf.Deg2Rad;

		return new Vector2(
			Mathf.Cos(radians) *
				polygonRadius,

			Mathf.Sin(radians) *
				polygonRadius);
	}

	private void CreatePlaneParentIfNeeded()
	{
		if (generatedPlaneParent != null)
			return;

		Transform existing =
			transform.Find(
				"Generated Cut Planes");

		if (existing != null)
		{
			generatedPlaneParent =
				existing;

			return;
		}

		GameObject parentObject =
			new GameObject(
				"Generated Cut Planes");

		generatedPlaneParent =
			parentObject.transform;

		generatedPlaneParent.SetParent(
			transform,
			false);

		generatedPlaneParent.localPosition =
			Vector3.zero;

		generatedPlaneParent.localRotation =
			Quaternion.identity;

		generatedPlaneParent.localScale =
			Vector3.one;
	}

	private void ClearGeneratedPlanes()
	{
		if (generatedPlaneParent == null)
			return;

		for (
			int i =
				generatedPlaneParent.childCount - 1;
			i >= 0;
			i--)
		{
			GameObject child =
				generatedPlaneParent
					.GetChild(i)
					.gameObject;

			if (Application.isPlaying)
			{
				Destroy(
					child);
			}
			else
			{
				DestroyImmediate(
					child);
			}
		}
	}

	// =========================================================
	// GIZMOS
	// =========================================================

	private void OnDrawGizmosSelected()
	{
		DrawDrillAreaGizmo();
		DrawCutVolumeGizmo();
	}

	private void DrawDrillAreaGizmo()
	{
		Matrix4x4 previousMatrix =
			Gizmos.matrix;

		/*
		 * Detection box is also centred on
		 * the drill transform.
		 */
		Gizmos.matrix =
			Matrix4x4.TRS(
				transform.position,
				transform.rotation,
				Vector3.one);

		Gizmos.DrawWireCube(
			Vector3.zero,
			drillArea);

		Gizmos.matrix =
			previousMatrix;
	}

	private void DrawCutVolumeGizmo()
	{
		int sides =
			Mathf.Max(
				3,
				polygonSides);

		float radius =
			Mathf.Max(
				0.01f,
				polygonRadius);

		float width =
			Mathf.Max(
				0.01f,
				cutWidth);

		Vector3 cutCenter =
			GetCutCenter();

		Vector3 frontCenter =
			cutCenter -
			transform.forward *
				(width * 0.5f);

		Vector3 backCenter =
			cutCenter +
			transform.forward *
				(width * 0.5f);

		float angleStep =
			360f / sides;

		for (int i = 0;
			i < sides;
			i++)
		{
			float angleA =
				polygonRotation +
				angleStep * i;

			float angleB =
				polygonRotation +
				angleStep * (i + 1);

			Vector2 a =
				GetGizmoPolygonPoint(
					angleA,
					radius);

			Vector2 b =
				GetGizmoPolygonPoint(
					angleB,
					radius);

			Vector3 frontA =
				PolygonPointToWorld(
					frontCenter,
					a);

			Vector3 frontB =
				PolygonPointToWorld(
					frontCenter,
					b);

			Vector3 backA =
				PolygonPointToWorld(
					backCenter,
					a);

			Vector3 backB =
				PolygonPointToWorld(
					backCenter,
					b);

			// Front polygon edge.
			Gizmos.DrawLine(
				frontA,
				frontB);

			// Back polygon edge.
			Gizmos.DrawLine(
				backA,
				backB);

			// Depth connection.
			Gizmos.DrawLine(
				frontA,
				backA);
		}

		/*
		 * Draw the centre so it is obvious exactly
		 * where cutWidth is being measured from.
		 */
		float markerSize = 0.1f;

		Gizmos.DrawLine(
			cutCenter -
				transform.right * markerSize,
			cutCenter +
				transform.right * markerSize);

		Gizmos.DrawLine(
			cutCenter -
				transform.up * markerSize,
			cutCenter +
				transform.up * markerSize);

		Gizmos.DrawLine(
			cutCenter -
				transform.forward * markerSize,
			cutCenter +
				transform.forward * markerSize);
	}

	private Vector2 GetGizmoPolygonPoint(
		float angleDegrees,
		float radius)
	{
		float radians =
			angleDegrees *
			Mathf.Deg2Rad;

		return new Vector2(
			Mathf.Cos(radians) *
				radius,

			Mathf.Sin(radians) *
				radius);
	}

	private Vector3 PolygonPointToWorld(
		Vector3 centre,
		Vector2 point)
	{
		return centre +
			transform.right *
				point.x +
			transform.up *
				point.y;
	}
}