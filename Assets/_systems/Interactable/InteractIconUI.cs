using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class InteractIconUI : MonoBehaviour
{
	[SerializeField] private RectTransform icon;

	[Header("Position")]
	[SerializeField] private Vector3 worldOffset = Vector3.zero;

	[Header("Scale")]
	[SerializeField] private float baseSize = 120f;
	[SerializeField] private float maxScale = 1.5f;
	[SerializeField] private float minScale = 0.25f;

	private Transform _target;
	private Camera _cam;

	private void Awake()
	{
		if (icon == null)
			icon = GetComponent<RectTransform>();

		Hide();
	}

	public void Show(Transform target, Camera cam)
	{
		_target = target;
		_cam = cam;

		SetIconVisible(true);
	}

	public void Hide()
	{
		_target = null;
		_cam = null;

		SetIconVisible(false);
	}

	private void LateUpdate()
	{
		if (_target == null || _cam == null || icon == null)
			return;

		Vector3 worldPosition = _target.position + worldOffset;

		Vector3 screenPosition =
			_cam.WorldToScreenPoint(worldPosition);

		bool isVisible =
			screenPosition.z > 0f &&
			screenPosition.x >= 0f &&
			screenPosition.x <= Screen.width &&
			screenPosition.y >= 0f &&
			screenPosition.y <= Screen.height;

		if (!isVisible)
		{
			SetIconVisible(false);
			return;
		}

		SetIconVisible(true);

		// Screen Space Overlay uses screen coordinates directly.
		icon.position = screenPosition;

		float distance = Vector3.Distance(
			_cam.transform.position,
			worldPosition
		);

		float scale = Mathf.Clamp(
			baseSize / Mathf.Max(0.001f, distance),
			minScale,
			maxScale
		);

		icon.localScale = new Vector3(scale, scale, 1f);
	}

	private void SetIconVisible(bool visible)
	{
		if (icon != null && icon.gameObject.activeSelf != visible)
			icon.gameObject.SetActive(visible);
	}
}