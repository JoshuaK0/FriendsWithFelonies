using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Local mouse-look controller used by the player camera and sticky-camera
/// survey rig. This is intentionally a MonoBehaviour rather than a
/// NetworkBehaviour; network synchronization is handled by StickyCameraProp.
/// </summary>
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

	/// <summary>
	/// Current look direction where X is pitch and Y is yaw.
	/// Assigning this property immediately updates the transforms.
	/// </summary>
	public Vector2 LookDir
	{
		get => lookDir;
		set => SetLookDir(value);
	}

	/// <summary>
	/// Alias retained for scripts that use the longer property name.
	/// </summary>
	public Vector2 LookDirection
	{
		get => lookDir;
		set => SetLookDir(value);
	}

	public bool IsPaused => isPaused;

	private void Awake()
	{
		ReadLookDirectionFromTransforms();
		RefreshSensitivity();
	}

	private void Update()
	{
		if (!isPaused)
			Look();
	}

	/// <summary>
	/// Reads mouse input and updates yaw and pitch.
	/// This uses Unity's legacy Input Manager axes: Mouse X and Mouse Y.
	/// </summary>
	public void Look()
	{
		lookDir.y += Input.GetAxis("Mouse X") * lookSpeed * Time.deltaTime;
		lookDir.x -= Input.GetAxis("Mouse Y") * lookSpeed * Time.deltaTime;

		ClampAndNormalizeLookDirection();
		ApplyLookDirection();
	}

	public void ToggleCamLookPaused(bool paused)
	{
		isPaused = paused;
	}

	public void SetPaused(bool paused)
	{
		isPaused = paused;
	}

	/// <summary>
	/// Sets pitch and yaw and applies them immediately.
	/// X is pitch. Y is yaw.
	/// </summary>
	public void SetLookDir(Vector2 direction)
	{
		lookDir = direction;
		ClampAndNormalizeLookDirection();
		ApplyLookDirection();
	}

	public Vector2 GetLookDir()
	{
		return lookDir;
	}

	// Compatibility aliases used by some versions of the adapted item package.
	public void SetLookDirection(Vector2 direction)
	{
		SetLookDir(direction);
	}

	public Vector2 GetLookDirection()
	{
		return GetLookDir();
	}

	public void ResetLook()
	{
		SetLookDir(Vector2.zero);
	}

	public void RefreshSensitivity()
	{
		lookSpeed = PlayerPrefs.GetFloat(
			sensitivityPlayerPref,
			defaultSensitivity);
	}

	// Compatibility with projects that previously called this through a
	// controls-settings interface.
	public void OnControlsUpdate()
	{
		RefreshSensitivity();
	}

	private void ReadLookDirectionFromTransforms()
	{
		float pitch = 0f;
		if (pitchPivot != null)
			pitch = NormalizeSignedAngle(pitchPivot.localEulerAngles.x);

		float yaw = NormalizeSignedAngle(transform.eulerAngles.y);
		lookDir = new Vector2(pitch, yaw);

		ClampAndNormalizeLookDirection();
		ApplyLookDirection();
	}

	private void ClampAndNormalizeLookDirection()
	{
		if (minimumPitch > maximumPitch)
		{
			float previousMinimum = minimumPitch;
			minimumPitch = maximumPitch;
			maximumPitch = previousMinimum;
		}

		lookDir.x = Mathf.Clamp(lookDir.x, minimumPitch, maximumPitch);
		lookDir.y = NormalizeSignedAngle(lookDir.y);
	}

	private void ApplyLookDirection()
	{
		transform.rotation = Quaternion.Euler(0f, lookDir.y, 0f);

		if (pitchPivot != null)
			pitchPivot.localRotation = Quaternion.Euler(lookDir.x, 0f, 0f);
	}

	private static float NormalizeSignedAngle(float angle)
	{
		return Mathf.Repeat(angle + 180f, 360f) - 180f;
	}

	private void OnValidate()
	{
		minimumPitch = Mathf.Clamp(minimumPitch, -89f, 0f);
		maximumPitch = Mathf.Clamp(maximumPitch, 0f, 89f);
		defaultSensitivity = Mathf.Max(0f, defaultSensitivity);
	}
}