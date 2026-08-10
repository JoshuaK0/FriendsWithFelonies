using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EzySlice;
using FishNet.Object;
using TMPro;
using UnityEngine;

/// <summary>
/// Server-timed network drill. The server starts the operation once and every
/// observer repeats the deterministic EzySlice operation against its local copy
/// of the scene geometry.
/// </summary>
public sealed class NetworkedDrillBehaviour : NetworkBehaviour
{
    [Header("Drilling")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private string wallLayerName = "Default";
    [SerializeField, Min(0f)] private float timeToDrill = 5f;
    [SerializeField] private Rigidbody body;
    [SerializeField] private Vector3 drillArea = Vector3.one;

    [Header("Slicing")]
    [Tooltip("Applied in order. Each transform's position and up vector define an EzySlice plane.")]
    [SerializeField] private List<Transform> planeTransforms = new();
    [Tooltip("Optional per-plane hull choice. True keeps EzySlice's upper hull; false keeps its lower hull.")]
    [SerializeField] private List<bool> keepUpperHull = new();
    [SerializeField] private Material crossSectionMaterial;

    [Header("Result")]
    [SerializeField] private GameObject platform;
    [SerializeField] private TextMeshProUGUI progressText;

    private float clientStartedAt;
    private bool completedLocally;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
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
    }

    private void Update()
    {
        if (progressText == null || completedLocally)
            return;

        float remaining = Mathf.Max(0f, timeToDrill - (Time.time - clientStartedAt));
        progressText.text = remaining.ToString("0.0");
    }

    private IEnumerator ServerDrillRoutine()
    {
        if (timeToDrill > 0f)
            yield return new WaitForSeconds(timeToDrill);

        PerformDrillLocal(transform.position, transform.rotation);
        PerformDrillObserversRpc(transform.position, transform.rotation);
    }

    [ObserversRpc]
    private void PerformDrillObserversRpc(Vector3 drillingPosition, Quaternion drillingRotation)
    {
        // A host already performed the authoritative local operation above.
        if (IsServerInitialized)
            return;

        PerformDrillLocal(drillingPosition, drillingRotation);
    }

    private void PerformDrillLocal(Vector3 drillingPosition, Quaternion drillingRotation)
    {
        if (completedLocally)
            return;

        completedLocally = true;

        Collider[] wallColliders = Physics.OverlapBox(
            drillingPosition + transform.forward * drillArea.z * 0.5f,
            drillArea * 0.5f,
            drillingRotation,
            wallLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < wallColliders.Length; i++)
        {
            Collider wall = wallColliders[i];
            if (wall != null)
                SliceWall(wall.gameObject);
        }

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }

        if (platform != null)
            Instantiate(platform, drillingPosition, drillingRotation);

        if (progressText != null)
            Destroy(progressText.gameObject);
    }

    private void SliceWall(GameObject wallRoot)
    {
        GameObject originalMeshObject = FindMeshObject(wallRoot);
        if (originalMeshObject == null)
        {
            Debug.LogWarning($"NetworkedDrillBehaviour found no MeshFilter below '{wallRoot.name}'.", wallRoot);
            return;
        }

        originalMeshObject.transform.SetParent(null, true);
        GameObject currentPiece = originalMeshObject;
        bool slicedAny = false;

        for (int i = 0; i < planeTransforms.Count; i++)
        {
            Transform plane = planeTransforms[i];
            if (plane == null || currentPiece == null)
                continue;

            GameObject[] hulls = Slice(
                currentPiece,
                plane.position,
                plane.up,
                crossSectionMaterial);

            if (hulls == null || hulls.Length == 0)
                continue;

            bool keepUpper = i >= keepUpperHull.Count || keepUpperHull[i];
            int keepIndex = keepUpper ? 0 : 1;
            if (keepIndex >= hulls.Length || hulls[keepIndex] == null)
                keepIndex = 0;

            GameObject keptPiece = hulls[keepIndex];
            for (int hullIndex = 0; hullIndex < hulls.Length; hullIndex++)
            {
                if (hullIndex != keepIndex && hulls[hullIndex] != null)
                    Destroy(hulls[hullIndex]);
            }

            ConfigureWallPiece(keptPiece);
            if (currentPiece != originalMeshObject)
                Destroy(currentPiece);

            currentPiece = keptPiece;
            slicedAny = true;
        }

        if (!slicedAny)
        {
            originalMeshObject.transform.SetParent(wallRoot.transform, true);
            return;
        }

        if (currentPiece != originalMeshObject)
            Destroy(originalMeshObject);

        if (wallRoot != originalMeshObject)
            Destroy(wallRoot);
    }

    private GameObject FindMeshObject(GameObject root)
    {
        if (root == null)
            return null;

        if (root.GetComponent<MeshFilter>() != null)
            return root;

        MeshFilter child = root.GetComponentInChildren<MeshFilter>();
        return child != null ? child.gameObject : null;
    }

    private void ConfigureWallPiece(GameObject piece)
    {
        if (piece == null)
            return;

        int layer = LayerMask.NameToLayer(wallLayerName);
        if (layer >= 0)
            piece.layer = layer;

        if (piece.GetComponent<MeshCollider>() == null)
            piece.AddComponent<MeshCollider>();
    }

    public GameObject[] Slice(
        GameObject objectToSlice,
        Vector3 planeWorldPosition,
        Vector3 planeWorldDirection,
        Material sectionMaterial)
    {
        if (objectToSlice == null)
            return null;

        return objectToSlice
            .SliceInstantiate(planeWorldPosition, planeWorldDirection, sectionMaterial)
            ?.Where(item => item != null)
            .ToArray();
    }
}
