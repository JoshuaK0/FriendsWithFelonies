using System.Collections.Generic;
using UnityEngine;

public sealed class StickyCameraItem : HotbarHeldItem
{
    [Header("Placement")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private GameObject placementViewmodel;

    [Header("Camera list")]
    [SerializeField] private KeyCode nextCameraKey = KeyCode.Q;
    [SerializeField] private KeyCode previousCameraKey = KeyCode.E;

    [Header("Survey rig")]
    [SerializeField] private GameObject surveyingObject;
    [SerializeField] private Camera surveyCamera;
    [SerializeField] private CamLook surveyCamLook;
    [SerializeField] private GameObject minimizedUi;
    [SerializeField] private RenderTexture minimizedRenderTexture;
    [SerializeField] private StickyCameraManualUpdate manualUpdate;
    [SerializeField] private KeyCode minimizeKey = KeyCode.M;
    [SerializeField, Min(0.01f)] private float lookSendInterval = 0.05f;

    private int currentCameraIndex;
    private bool isSurveying;
    private bool isMinimized;
    private float nextLookSendTime;

    private StickyCameraItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart =
            ItemServices != null
                ? ItemServices.GetNetworkedStickyCamera()
                : null;

        if (StickyCameraManager.Instance == null &&
            Inventory != null)
        {
            StickyCameraManager.Instance =
                Inventory.GetComponentInParent<StickyCameraManager>();
        }

        if (manualUpdate == null && surveyCamera != null)
        {
            manualUpdate =
                surveyCamera.GetComponent<StickyCameraManualUpdate>();
        }

        // Keep the survey rig alive when the held item prefab is rebuilt.
        if (surveyingObject != null &&
            surveyingObject.transform.IsChildOf(transform))
        {
            surveyingObject.transform.SetParent(null, true);
        }
    }

    protected override void OnEquipped()
    {
        StickyCameraManager.Instance?.RefreshCameras();

        SetPlacementVisuals(true);
        preview?.SetVisible(true);
        SetSurveyRigActive(false);
    }

    protected override void OnEquippedUpdate()
    {
        List<StickyCameraProp> cameras =
            StickyCameraManager.Instance != null
                ? StickyCameraManager.Instance.GetCameras()
                : null;

        int cameraCount =
            cameras != null
                ? cameras.Count
                : 0;

        if (cameraCount <= 0 &&
            (isSurveying || isMinimized))
        {
            ExitSurveyMode();
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (isSurveying || isMinimized)
                ExitSurveyMode();
            else if (cameraCount > 0)
                EnterSurveyMode(false);
        }

        if (Input.GetKeyDown(minimizeKey) &&
            cameraCount > 0)
        {
            if (!isSurveying && !isMinimized)
                EnterSurveyMode(true);
            else
                SetMinimized(!isMinimized);
        }

        if (isSurveying || isMinimized)
            UpdateSurveyMode(cameras);
        else
            UpdatePlacementMode();
    }

    protected override void OnUnequipped()
    {
        ExitSurveyMode();

        preview?.SetVisible(false);
        SetPlacementVisuals(false);
    }

    private void UpdatePlacementMode()
    {
        SetPlacementVisuals(true);
        SetSurveyRigActive(false);

        if (rayOrigin == null ||
            preview == null ||
            networkedCounterpart == null)
        {
            return;
        }

        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                range,
                surfaceMask,
                QueryTriggerInteraction.Ignore))
        {
            preview.SetVisible(false);
            return;
        }

        preview.SetVisible(true);

        // Forward points directly away from the surface.
        Quaternion rotation =
            Quaternion.LookRotation(
                hit.normal,
                Vector3.up);

        Vector3 position =
            hit.point +
            hit.normal * surfaceOffset;

        preview.SetPose(
            position,
            rotation);

        if (preview.EvaluateClear() &&
            Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceStickyCamera(
                position,
                rotation);

            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    private void UpdateSurveyMode(
        List<StickyCameraProp> cameras)
    {
        if (cameras == null ||
            cameras.Count == 0)
        {
            ExitSurveyMode();
            return;
        }

        currentCameraIndex =
            Mathf.Clamp(
                currentCameraIndex,
                0,
                cameras.Count - 1);

        if (Input.GetKeyDown(nextCameraKey))
        {
            SwitchCamera(
                cameras,
                1);
        }
        else if (Input.GetKeyDown(previousCameraKey))
        {
            SwitchCamera(
                cameras,
                -1);
        }

        StickyCameraProp cameraProp =
            cameras[currentCameraIndex];

        if (cameraProp == null)
        {
            StickyCameraManager.Instance?.RefreshCameras();
            return;
        }

        SetPlacementVisuals(false);
        preview?.SetVisible(false);
        SetSurveyRigActive(true);

        UpdateSurveyRigBase(cameraProp);

        if (!isMinimized &&
            surveyCamLook != null &&
            Time.time >= nextLookSendTime)
        {
            nextLookSendTime =
                Time.time + lookSendInterval;

            cameraProp.RequestSetLookDirection(
                surveyCamLook.GetLookDir());
        }
    }

    private void EnterSurveyMode(bool minimized)
    {
        List<StickyCameraProp> cameras =
            StickyCameraManager.Instance != null
                ? StickyCameraManager.Instance.GetCameras()
                : null;

        if (cameras == null ||
            cameras.Count == 0)
        {
            return;
        }

        currentCameraIndex =
            Mathf.Clamp(
                currentCameraIndex,
                0,
                cameras.Count - 1);

        StickyCameraProp cameraProp =
            cameras[currentCameraIndex];

        if (cameraProp == null)
            return;

        isSurveying = !minimized;
        isMinimized = minimized;

        SetSurveyRigActive(true);
        ConfigureSurveyCamera(minimized);

        MoveLookToCamera(cameraProp);

        cameraProp.RequestSetLight(true);
    }

    private void ExitSurveyMode()
    {
        DisableCurrentCameraLight();

        isSurveying = false;
        isMinimized = false;

        ConfigureSurveyCamera(
            false,
            true);

        SetSurveyRigActive(false);
    }

    private void SetMinimized(bool minimized)
    {
        if (!isSurveying &&
            !isMinimized)
        {
            return;
        }

        isMinimized = minimized;
        isSurveying = !minimized;

        ConfigureSurveyCamera(minimized);
    }

    private void SwitchCamera(
        List<StickyCameraProp> cameras,
        int direction)
    {
        if (cameras == null ||
            cameras.Count == 0)
        {
            return;
        }

        DisableCurrentCameraLight();

        currentCameraIndex =
            (currentCameraIndex +
             direction +
             cameras.Count) %
            cameras.Count;

        StickyCameraProp next =
            cameras[currentCameraIndex];

        MoveLookToCamera(next);

        next?.RequestSetLight(true);
    }

    /// <summary>
    /// Updates only the survey rig's mounted/base transform.
    /// CamLook should apply its local pitch/yaw on a child pivot, not on
    /// surveyingObject itself.
    /// </summary>
    private void UpdateSurveyRigBase(
        StickyCameraProp cameraProp)
    {
        if (cameraProp == null ||
            surveyingObject == null ||
            cameraProp.LookPoint == null)
        {
            return;
        }

        surveyingObject.transform.position = cameraProp.LookPoint.position;

        if (surveyCamLook != null)
        {
            // CamLook now preserves this mounted rotation and applies yaw
            // relative to it. This works even when CamLook is attached
            // directly to surveyingObject.
            surveyCamLook.SetBaseRotation(cameraProp.SurveyBaseRotation);
        }
        else
        {
            surveyingObject.transform.rotation = cameraProp.SurveyBaseRotation;
        }
    }

    private void MoveLookToCamera(
        StickyCameraProp cameraProp)
    {
        if (cameraProp == null)
            return;

        UpdateSurveyRigBase(cameraProp);

        if (surveyCamLook != null)
        {
            surveyCamLook.SetLookDir(
                cameraProp.LookDirection);
        }
    }

    private void DisableCurrentCameraLight()
    {
        List<StickyCameraProp> cameras =
            StickyCameraManager.Instance != null
                ? StickyCameraManager.Instance.GetCameras()
                : null;

        if (cameras == null ||
            cameras.Count == 0)
        {
            return;
        }

        currentCameraIndex =
            Mathf.Clamp(
                currentCameraIndex,
                0,
                cameras.Count - 1);

        cameras[currentCameraIndex]
            ?.RequestSetLight(false);
    }

    private void ConfigureSurveyCamera(
        bool minimized,
        bool disabled = false)
    {
        if (surveyCamLook != null)
        {
            surveyCamLook.ToggleCamLookPaused(
                minimized || disabled);
        }

        if (minimizedUi != null)
        {
            minimizedUi.SetActive(
                minimized && !disabled);
        }

        if (surveyCamera != null)
        {
            surveyCamera.targetTexture =
                minimized && !disabled
                    ? minimizedRenderTexture
                    : null;
        }

        manualUpdate?.ToggleManualRefresh(
            minimized && !disabled);
    }

    private void SetPlacementVisuals(bool enabled)
    {
        if (placementViewmodel != null)
            placementViewmodel.SetActive(enabled);
    }

    private void SetSurveyRigActive(bool enabled)
    {
        if (surveyingObject != null)
            surveyingObject.SetActive(enabled);
    }

    private void OnDestroy()
    {
        if (surveyingObject != null)
            Destroy(surveyingObject);
    }
}
