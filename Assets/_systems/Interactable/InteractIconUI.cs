using UnityEngine;
using UnityEngine.UI;

public sealed class InteractIconUI : MonoBehaviour
{
	[SerializeField] private Canvas canvas;
	[SerializeField] private RectTransform iconRoot;
	[SerializeField] private Slider holdSlider;

	private Transform worldAnchor;
	private Camera worldCamera;
	private bool isCentered;

	private void Awake()
	{
		if (canvas == null)
			canvas = GetComponentInParent<Canvas>();

		if (iconRoot == null)
			iconRoot = transform as RectTransform;

		Hide();
	}

	private void LateUpdate()
	{
		if (iconRoot == null || !iconRoot.gameObject.activeSelf)
			return;

		if (isCentered)
		{
			SetScreenPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
			return;
		}

		if (worldAnchor == null || worldCamera == null)
		{
			Hide();
			return;
		}

		Vector3 screenPosition =
			worldCamera.WorldToScreenPoint(worldAnchor.position);

		if (screenPosition.z <= 0f)
		{
			iconRoot.gameObject.SetActive(false);
			return;
		}

		SetScreenPosition(screenPosition);
	}

	public void ShowWorld(Transform anchor, Camera targetCamera)
	{
		if (iconRoot == null || anchor == null || targetCamera == null)
		{
			Hide();
			return;
		}

		worldAnchor = anchor;
		worldCamera = targetCamera;
		isCentered = false;
		iconRoot.gameObject.SetActive(true);
	}

	public void ShowCentered()
	{
		if (iconRoot == null)
			return;

		worldAnchor = null;
		worldCamera = null;
		isCentered = true;
		iconRoot.gameObject.SetActive(true);
	}

	public void SetHoldProgress(float normalizedProgress, bool visible)
	{
		if (holdSlider == null)
			return;

		holdSlider.value = Mathf.Clamp01(normalizedProgress);
		holdSlider.gameObject.SetActive(visible);
	}

	public void Hide()
	{
		worldAnchor = null;
		worldCamera = null;
		isCentered = false;

		if (holdSlider != null)
		{
			holdSlider.value = 0f;
			holdSlider.gameObject.SetActive(false);
		}

		if (iconRoot != null)
			iconRoot.gameObject.SetActive(false);
	}

	private void SetScreenPosition(Vector2 screenPosition)
	{
		RectTransform parent = iconRoot.parent as RectTransform;

		if (parent == null || canvas == null)
		{
			iconRoot.position = screenPosition;
			return;
		}

		Camera uiCamera =
			canvas.renderMode == RenderMode.ScreenSpaceOverlay
				? null
				: canvas.worldCamera;

		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
			parent,
			screenPosition,
			uiCamera,
			out Vector2 localPosition))
		{
			iconRoot.anchoredPosition = localPosition;
		}
	}
}
