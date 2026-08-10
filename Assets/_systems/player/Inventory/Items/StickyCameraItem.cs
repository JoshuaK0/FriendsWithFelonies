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
    [SerializeField] private StickyCameraManager cameraManager;
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
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedStickyCamera() : null;
        if (cameraManager == null && Inventory != null)
            cameraManager = Inventory.GetComponentInParent<StickyCameraManager>();

        if (manualUpdate == null && surveyCamera != null)
            manualUpdate = surveyCamera.GetComponent<StickyCameraManualUpdate>();

        if (surveyingObject != null && surveyingObject.transform.IsChildOf(transform))
            surveyingObject.transform.SetParent(null, true);
    }

    protected override void OnEquipped()
    {
        cameraManager?.RefreshCameras();
        SetPlacementVisuals(true);
        preview?.SetVisible(true);
        SetSurveyRigActive(false);
    }

    protected override void OnEquippedUpdate()
    {
        List<StickyCameraProp> cameras = cameraManager != null ? cameraManager.GetCameras() : null;
        int cameraCount = cameras != null ? cameras.Count : 0;

        if (cameraCount <= 0 && (isSurveying || isMinimized))
            ExitSurveyMode();

        if (Input.GetMouseButtonDown(1))
        {
            if (isSurveying || isMinimized)
                ExitSurveyMode();
            else if (cameraCount > 0)
                EnterSurveyMode(false);
        }

        if (Input.GetKeyDown(minimizeKey) && cameraCount > 0)
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

        if (rayOrigin == null || preview == null || networkedCounterpart == null)
            return;

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
        Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
        Vector3 position = hit.point + hit.normal * surfaceOffset;
        preview.SetPose(position, rotation);

        if (preview.EvaluateClear() && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceStickyCamera(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    private void UpdateSurveyMode(List<StickyCameraProp> cameras)
    {
        if (cameras == null || cameras.Count == 0)
        {
            ExitSurveyMode();
            return;
        }

        currentCameraIndex = Mathf.Clamp(currentCameraIndex, 0, cameras.Count - 1);

        if (Input.GetKeyDown(nextCameraKey))
            SwitchCamera(cameras, 1);
        else if (Input.GetKeyDown(previousCameraKey))
            SwitchCamera(cameras, -1);

        StickyCameraProp cameraProp = cameras[currentCameraIndex];
        if (cameraProp == null)
        {
            cameraManager?.RefreshCameras();
            return;
        }

        SetPlacementVisuals(false);
        preview?.SetVisible(false);
        SetSurveyRigActive(true);

        Transform lookPoint = cameraProp.LookPoint;
        if (surveyingObject != null && lookPoint != null)
        {
            surveyingObject.transform.position = lookPoint.position;
            if (surveyCamLook == null)
                surveyingObject.transform.rotation = lookPoint.rotation;
        }

        if (!isMinimized && surveyCamLook != null && Time.time >= nextLookSendTime)
        {
            nextLookSendTime = Time.time + lookSendInterval;
            cameraProp.RequestSetLookDirection(surveyCamLook.GetLookDir());
        }
    }

    private void EnterSurveyMode(bool minimized)
    {
        List<StickyCameraProp> cameras = cameraManager != null ? cameraManager.GetCameras() : null;
        if (cameras == null || cameras.Count == 0)
            return;

        currentCameraIndex = Mathf.Clamp(currentCameraIndex, 0, cameras.Count - 1);
        StickyCameraProp cameraProp = cameras[currentCameraIndex];
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
        ConfigureSurveyCamera(false, true);
        SetSurveyRigActive(false);
    }

    private void SetMinimized(bool minimized)
    {
        if (!isSurveying && !isMinimized)
            return;

        isMinimized = minimized;
        isSurveying = !minimized;
        ConfigureSurveyCamera(minimized);
    }

    private void SwitchCamera(List<StickyCameraProp> cameras, int direction)
    {
        if (cameras == null || cameras.Count == 0)
            return;

        DisableCurrentCameraLight();
        currentCameraIndex = (currentCameraIndex + direction + cameras.Count) % cameras.Count;
        StickyCameraProp next = cameras[currentCameraIndex];
        MoveLookToCamera(next);
        next?.RequestSetLight(true);
    }

    private void MoveLookToCamera(StickyCameraProp cameraProp)
    {
        if (cameraProp == null)
            return;

        if (surveyingObject != null)
            surveyingObject.transform.SetPositionAndRotation(cameraProp.LookPoint.position, cameraProp.LookPoint.rotation);

        surveyCamLook?.SetLookDir(cameraProp.LookDirection);
    }

    private void DisableCurrentCameraLight()
    {
        List<StickyCameraProp> cameras = cameraManager != null ? cameraManager.GetCameras() : null;
        if (cameras == null || cameras.Count == 0)
            return;

        currentCameraIndex = Mathf.Clamp(currentCameraIndex, 0, cameras.Count - 1);
        cameras[currentCameraIndex]?.RequestSetLight(false);
    }

    private void ConfigureSurveyCamera(bool minimized, bool disabled = false)
    {
        if (surveyCamLook != null)
            surveyCamLook.ToggleCamLookPaused(minimized || disabled);

        if (minimizedUi != null)
            minimizedUi.SetActive(minimized && !disabled);

        if (surveyCamera != null)
            surveyCamera.targetTexture = minimized && !disabled ? minimizedRenderTexture : null;

        manualUpdate?.ToggleManualRefresh(minimized && !disabled);
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
