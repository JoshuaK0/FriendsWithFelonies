using UnityEngine;

public sealed class CameraOrbitController : MonoBehaviour
{
	[Header("Pivot")]
	[SerializeField]
	private Transform pivot;

	[Header("Orbit")]
	[SerializeField]
	private float mouseSensitivity = 3f;

	[SerializeField]
	private float minPitch = -30f;

	[SerializeField]
	private float maxPitch = 70f;

	[Header("Zoom")]
	[SerializeField]
	private float startingDistance = 5f;

	[SerializeField]
	private float minDistance = 1f;

	[SerializeField]
	private float maxDistance = 8f;

	[SerializeField]
	private float zoomSensitivity = 1f;

	[Header("Collision")]
	[SerializeField]
	private LayerMask collisionMask = ~0;

	[SerializeField, Min(0f)]
	private float collisionRadius = 0.2f;

	[SerializeField, Min(0f)]
	private float collisionPadding = 0.1f;

	[Header("Smoothing")]
	[SerializeField, Min(0f)]
	private float positionLerpSpeed = 15f;

	[SerializeField, Min(0f)]
	private float rotationLerpSpeed = 15f;

	private float yaw;
	private float pitch;
	private float targetDistance;

	private void Awake()
	{
		if (pivot == null)
			pivot = transform.parent;

		targetDistance = Mathf.Clamp(
			startingDistance,
			minDistance,
			maxDistance);

		Vector3 angles = transform.eulerAngles;

		yaw = angles.y;
		pitch = NormalizeAngle(angles.x);
	}

	private void Update()
	{
		HandleOrbitInput();
		HandleZoomInput();
	}

	private void LateUpdate()
	{
		UpdateCamera();
	}

	private void HandleOrbitInput()
	{
		float mouseX = Input.GetAxisRaw("Mouse X");
		float mouseY = Input.GetAxisRaw("Mouse Y");

		yaw += mouseX * mouseSensitivity;
		pitch -= mouseY * mouseSensitivity;

		pitch = Mathf.Clamp(
			pitch,
			minPitch,
			maxPitch);
	}

	private void HandleZoomInput()
	{
		float scroll = Input.mouseScrollDelta.y;

		if (Mathf.Approximately(scroll, 0f))
			return;

		targetDistance -=
			scroll * zoomSensitivity;

		targetDistance = Mathf.Clamp(
			targetDistance,
			minDistance,
			maxDistance);
	}

	private void UpdateCamera()
	{
		if (pivot == null)
			return;

		Quaternion targetRotation =
			Quaternion.Euler(
				pitch,
				yaw,
				0f);

		Vector3 direction =
			targetRotation * Vector3.back;

		float distance =
			GetCollisionDistance(direction);

		Vector3 targetPosition =
			pivot.position +
			direction * distance;

		float positionT =
			1f - Mathf.Exp(
				-positionLerpSpeed *
				Time.deltaTime);

		float rotationT =
			1f - Mathf.Exp(
				-rotationLerpSpeed *
				Time.deltaTime);

		transform.position =
			Vector3.Lerp(
				transform.position,
				targetPosition,
				positionT);

		transform.rotation =
			Quaternion.Slerp(
				transform.rotation,
				targetRotation,
				rotationT);
	}

	private float GetCollisionDistance(
		Vector3 direction)
	{
		if (pivot == null)
			return targetDistance;

		if (Physics.SphereCast(
			pivot.position,
			collisionRadius,
			direction,
			out RaycastHit hit,
			targetDistance,
			collisionMask,
			QueryTriggerInteraction.Ignore))
		{
			return Mathf.Max(
				0f,
				hit.distance -
				collisionPadding);
		}

		return targetDistance;
	}

	private static float NormalizeAngle(float angle)
	{
		if (angle > 180f)
			angle -= 360f;

		return angle;
	}
}