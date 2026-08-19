using UnityEngine;
using UnityEngine.Serialization;

public sealed class CamLook : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("xPivot")]
    [SerializeField] private Transform pitchPivot;

    [Header("Sensitivity")]
    [SerializeField, Min(0f)] private float defaultSensitivity = 75f;
    [SerializeField] private string sensitivityPlayerPref = "MouseSensitivity";

    [Header("Pitch Limits")]
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -89f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 89f;

    private float lookSpeed;
    private Vector2 lookDir;
    private bool isPaused;
    private bool hasBaseRotation;
    private Quaternion baseRotation = Quaternion.identity;

    public Vector2 LookDir { get => lookDir; set => SetLookDir(value); }
    public Vector2 LookDirection { get => lookDir; set => SetLookDir(value); }
    public bool IsPaused => isPaused;

    private void Awake()
    {
        RefreshSensitivity();
    }

    private void Update()
    {
        if (!isPaused)
            Look();
    }

    public void Look()
    {
        lookDir.y += Input.GetAxis("Mouse X") * lookSpeed * Time.deltaTime;
        lookDir.x -= Input.GetAxis("Mouse Y") * lookSpeed * Time.deltaTime;
        ClampAndNormalize();
        ApplyLookDirection();
    }

    public void ToggleCamLookPaused(bool paused) => isPaused = paused;
    public void SetPaused(bool paused) => isPaused = paused;

    // Used by the sticky-camera survey rig. Yaw becomes relative to this
    // mounted world-space rotation instead of relative to world Y.
    public void SetBaseRotation(Quaternion mountedRotation)
    {
        baseRotation = mountedRotation;
        hasBaseRotation = true;
        ApplyLookDirection();
    }

    public void ClearBaseRotation()
    {
        hasBaseRotation = false;
        ApplyLookDirection();
    }

    public void SetLookDir(Vector2 direction)
    {
        lookDir = direction;
        ClampAndNormalize();
        ApplyLookDirection();
    }

    public Vector2 GetLookDir() => lookDir;
    public void SetLookDirection(Vector2 direction) => SetLookDir(direction);
    public Vector2 GetLookDirection() => GetLookDir();
    public void ResetLook() => SetLookDir(Vector2.zero);

    public void RefreshSensitivity()
    {
        lookSpeed = PlayerPrefs.GetFloat(sensitivityPlayerPref, defaultSensitivity);
    }

    public void OnControlsUpdate() => RefreshSensitivity();

    private void ClampAndNormalize()
    {
        if (minimumPitch > maximumPitch)
        {
            float t = minimumPitch;
            minimumPitch = maximumPitch;
            maximumPitch = t;
        }

        lookDir.x = Mathf.Clamp(lookDir.x, minimumPitch, maximumPitch);
        lookDir.y = Mathf.Repeat(lookDir.y + 180f, 360f) - 180f;
    }

    private void ApplyLookDirection()
    {
        Quaternion yaw = Quaternion.Euler(0f, lookDir.y, 0f);

        // This is the key fix. In sticky-camera mode we preserve the wall's
        // mounted rotation and apply yaw relative to that rotation.
        transform.rotation = hasBaseRotation ? baseRotation * yaw : yaw;

        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(lookDir.x, 0f, 0f);
    }

    private void OnValidate()
    {
        minimumPitch = Mathf.Clamp(minimumPitch, -89f, 0f);
        maximumPitch = Mathf.Clamp(maximumPitch, 0f, 89f);
        defaultSensitivity = Mathf.Max(0f, defaultSensitivity);
    }
}
