using FishNet.Object;
using UnityEngine;

public class ScreenIndicatorUI : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private RectTransform indicatorTransform;

	[SerializeField]
	private GameObject onScreenIndicator;

	[SerializeField]
	private GameObject offscreenIndicator;

	[Header("Settings")]
	[SerializeField]
	private float outOfSightOffset = 20f;

	[SerializeField]
	private bool requireOwnership = true;

	private GameObject target;
	private Camera mainCamera;

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (requireOwnership && !IsOwner)
		{
			gameObject.SetActive(false);
		}
	}

	public void InitialiseTargetIndicator(
		GameObject target,
		Camera mainCamera)
	{
		this.target = target;
		this.mainCamera = mainCamera;
	}

	private void Update()
	{
		UpdateTargetIndicator();
	}

	public void UpdateTargetIndicator()
	{
		if (target == null || mainCamera == null)
			return;

		SetIndicatorPosition();
	}

	private void SetIndicatorPosition()
	{
		Vector3 screenPosition =
			mainCamera.WorldToScreenPoint(
				target.transform.position);

		bool inFront =
			screenPosition.z > 0f;

		bool onScreen =
			inFront &&
			screenPosition.x >= 0f &&
			screenPosition.x <= Screen.width &&
			screenPosition.y >= 0f &&
			screenPosition.y <= Screen.height;

		if (onScreen)
		{
			screenPosition.z = 0f;

			SetTargetOutOfSight(
				false,
				screenPosition);
		}
		else
		{
			screenPosition =
				GetOffscreenIndicatorPosition(
					screenPosition);

			SetTargetOutOfSight(
				true,
				screenPosition);
		}

		indicatorTransform.position =
			screenPosition;
	}

	private Vector3 GetOffscreenIndicatorPosition(
		Vector3 screenPosition)
	{
		bool inFront =
			screenPosition.z > 0f;

		screenPosition.z = 0f;

		Vector3 screenCenter =
			new Vector3(
				Screen.width * 0.5f,
				Screen.height * 0.5f,
				0f);

		// Convert into coordinates relative
		// to the centre of the screen.
		screenPosition -= screenCenter;

		// If the target is behind the camera,
		// invert the direction.
		if (!inFront)
		{
			screenPosition *= -1f;
		}

		float halfWidth =
			Screen.width * 0.5f -
			outOfSightOffset;

		float halfHeight =
			Screen.height * 0.5f -
			outOfSightOffset;

		/*
		 * Work out where the direction vector
		 * intersects the edge of the screen.
		 */

		float xRatio =
			Mathf.Abs(screenPosition.x) > Mathf.Epsilon
				? halfWidth /
				  Mathf.Abs(screenPosition.x)
				: float.MaxValue;

		float yRatio =
			Mathf.Abs(screenPosition.y) > Mathf.Epsilon
				? halfHeight /
				  Mathf.Abs(screenPosition.y)
				: float.MaxValue;

		float ratio =
			Mathf.Min(
				xRatio,
				yRatio);

		screenPosition *= ratio;

		screenPosition += screenCenter;

		return screenPosition;
	}

	private void SetTargetOutOfSight(
		bool outOfSight,
		Vector3 indicatorPosition)
	{
		if (outOfSight)
		{
			if (!offscreenIndicator.activeSelf)
				offscreenIndicator.SetActive(true);

			if (onScreenIndicator.activeSelf)
				onScreenIndicator.SetActive(false);

			offscreenIndicator.transform.rotation =
				Quaternion.Euler(
					GetOffscreenIndicatorRotation(
						indicatorPosition));
		}
		else
		{
			if (offscreenIndicator.activeSelf)
				offscreenIndicator.SetActive(false);

			if (!onScreenIndicator.activeSelf)
				onScreenIndicator.SetActive(true);
		}
	}

	private Vector3 GetOffscreenIndicatorRotation(
		Vector3 indicatorPosition)
	{
		Vector3 screenCenter =
			new Vector3(
				Screen.width * 0.5f,
				Screen.height * 0.5f,
				0f);

		Vector3 direction =
			indicatorPosition -
			screenCenter;

		float angle =
			Vector3.SignedAngle(
				Vector3.up,
				direction,
				Vector3.forward);

		return new Vector3(
			0f,
			0f,
			angle);
	}

	public void EnableUI(bool isEnabled)
	{
		indicatorTransform.gameObject.SetActive(
			isEnabled);
	}
}