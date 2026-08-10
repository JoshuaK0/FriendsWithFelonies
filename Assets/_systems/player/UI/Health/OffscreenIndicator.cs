using UnityEngine;

public class OffscreenIndicator : MonoBehaviour
{
	public Vector3 targetWorldPosition;
	public bool hasTarget;
	public Camera targetCamera;
	public RectTransform canvasRect;

	public bool useCircle = true;
	public float circleRadius = 300f;
	public float borderPadding = 50f;
	public bool forceAlwaysOn = false;

	public bool useCameraDirection = true;
	public Transform directionReference;

	public float moveLerpSpeed = 0f;

	[SerializeField] GameObject visualRoot;

	RectTransform rectTransform;
	Vector2 currentDir;
	bool hasCurrentDir;
	bool justActivated;
	bool isActive;

	public bool IsIndicatorActive => isActive;

	void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		if (visualRoot == null) visualRoot = gameObject;
		visualRoot.SetActive(false);
	}

	public void Initialize(Vector3 newTargetWorldPosition, Camera cam, RectTransform canvas)
	{
		targetWorldPosition = newTargetWorldPosition;
		targetCamera = cam;
		canvasRect = canvas;
		hasTarget = true;
	}

	public void SetTargetPosition(Vector3 newTargetWorldPosition)
	{
		targetWorldPosition = newTargetWorldPosition;
		hasTarget = true;
	}

	public void SetIndicatorActive(bool active)
	{
		if (active && !isActive)
		{
			justActivated = true;
			hasCurrentDir = false;
		}

		isActive = active;

		if (visualRoot != null)
			visualRoot.SetActive(active);
	}

	void Update()
	{
		UpdateIndicator();
	}

	public void UpdateIndicator()
	{
		if (!isActive)
		{
			if (visualRoot != null && visualRoot.activeSelf)
				visualRoot.SetActive(false);
			return;
		}

		if (!hasTarget || targetCamera == null || canvasRect == null || rectTransform == null)
			return;

		Vector3 screenPos = targetCamera.WorldToScreenPoint(targetWorldPosition);
		bool inFront = screenPos.z > 0f;

		float screenWidth = Screen.width;
		float screenHeight = Screen.height;

		bool isOnScreen = inFront &&
						  screenPos.x >= 0f && screenPos.x <= screenWidth &&
						  screenPos.y >= 0f && screenPos.y <= screenHeight;

		if (!forceAlwaysOn && isOnScreen)
		{
			if (visualRoot != null && visualRoot.activeSelf)
				visualRoot.SetActive(false);
			return;
		}

		if (visualRoot != null && !visualRoot.activeSelf)
			visualRoot.SetActive(true);

		Vector2 targetDir;

		if (useCameraDirection || directionReference == null)
		{
			Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
			targetDir = new Vector2(screenPos.x - screenCenter.x, screenPos.y - screenCenter.y);

			if (!inFront)
				targetDir = -targetDir;
		}
		else
		{
			Vector3 worldDir = targetWorldPosition - directionReference.position;
			if (worldDir.sqrMagnitude < 0.0001f)
				worldDir = directionReference.forward;

			Vector3 localDir = directionReference.InverseTransformDirection(worldDir);
			targetDir = new Vector2(localDir.x, localDir.z);
		}

		if (targetDir.sqrMagnitude < 0.0001f)
			targetDir = Vector2.up;
		else
			targetDir.Normalize();

		bool shouldSmooth = moveLerpSpeed > 0f && !justActivated && hasCurrentDir;

		if (!shouldSmooth)
		{
			currentDir = targetDir;
		}
		else
		{
			float t = moveLerpSpeed * Time.deltaTime;
			if (t >= 1f)
			{
				currentDir = targetDir;
			}
			else
			{
				Vector2 lerped = Vector2.Lerp(currentDir, targetDir, t);
				if (lerped.sqrMagnitude < 0.0001f)
					currentDir = targetDir;
				else
				{
					lerped.Normalize();
					currentDir = lerped;
				}
			}
		}

		hasCurrentDir = true;
		justActivated = false;

		Vector2 dir = currentDir;
		if (dir.sqrMagnitude < 0.0001f)
			dir = Vector2.up;
		else
			dir.Normalize();

		Vector2 indicatorPos;

		if (useCircle)
		{
			indicatorPos = dir * circleRadius;
		}
		else
		{
			Vector2 halfCanvas = canvasRect.sizeDelta * 0.5f;
			Vector2 max = new Vector2(halfCanvas.x - borderPadding, halfCanvas.y - borderPadding);

			float tx = dir.x != 0f ? max.x / Mathf.Abs(dir.x) : float.MaxValue;
			float ty = dir.y != 0f ? max.y / Mathf.Abs(dir.y) : float.MaxValue;
			float t = Mathf.Min(tx, ty);

			indicatorPos = dir * t;
		}

		rectTransform.anchoredPosition = indicatorPos;

		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
		rectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
	}
}
