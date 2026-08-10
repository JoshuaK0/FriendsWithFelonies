using UnityEngine;

public class MouseLook : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Transform cameraHolder;

	[Header("Settings")]
	[SerializeField] private float mouseSensitivity = 200f;
	[SerializeField] private float minimumLookAngle = -90f;
	[SerializeField] private float maximumLookAngle = 90f;

	private float pitch;
	private float yaw;

	private void Start()
	{
		yaw = playerBody.eulerAngles.y;

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void Update()
	{
		float mouseX =
			Input.GetAxisRaw("Mouse X") *
			mouseSensitivity *
			Time.deltaTime;

		float mouseY =
			Input.GetAxisRaw("Mouse Y") *
			mouseSensitivity *
			Time.deltaTime;

		yaw += mouseX;
		pitch -= mouseY;

		pitch = Mathf.Clamp(
			pitch,
			minimumLookAngle,
			maximumLookAngle);

		// Left and right rotation.
		playerBody.rotation = Quaternion.Euler(
			0f,
			yaw,
			0f);

		// Up and down rotation.
		cameraHolder.localRotation = Quaternion.Euler(
			pitch,
			0f,
			0f);
	}
}