using UnityEngine;

public class CrosshairController : MonoBehaviour
{
	[Header("Crosshair Pieces")]
	[SerializeField] private RectTransform left;
	[SerializeField] private RectTransform right;
	[SerializeField] private RectTransform top;
	[SerializeField] private RectTransform bottom;

	[Header("Gap")]
	[SerializeField] private float baseGap = 3f;
	[SerializeField] private float gapMultiplier = 1f;
	[SerializeField, Min(0f)] private float multiplierStrength = 1f;

	[Header("Piece Size")]
	[SerializeField] private float pieceWidth = 2f;
	[SerializeField] private float pieceHeight = 10f;

	private void Awake()
	{
		UpdateCrosshair();
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		UpdateCrosshair();
	}
#endif

	public void SetGapMultiplier(float multiplier)
	{
		gapMultiplier = Mathf.Max(0f, multiplier);
		UpdatePositions();
	}

	private void UpdateCrosshair()
	{
		UpdatePieceSizes();
		UpdatePositions();
	}

	private void UpdatePositions()
	{
		float effectiveMultiplier =
			1f +
			(gapMultiplier - 1f) *
			Mathf.Max(0f, multiplierStrength);

		float gap =
			Mathf.Max(0f, baseGap) *
			Mathf.Max(0f, effectiveMultiplier);

		if (left != null)
			left.anchoredPosition =
				new Vector2(-gap, 0f);

		if (right != null)
			right.anchoredPosition =
				new Vector2(gap, 0f);

		if (top != null)
			top.anchoredPosition =
				new Vector2(0f, gap);

		if (bottom != null)
			bottom.anchoredPosition =
				new Vector2(0f, -gap);
	}

	private void UpdatePieceSizes()
	{
		float width =
			Mathf.Max(0f, pieceWidth);

		float height =
			Mathf.Max(0f, pieceHeight);

		Vector2 verticalSize =
			new Vector2(
				width,
				height);

		Vector2 horizontalSize =
			new Vector2(
				height,
				width);

		if (left != null)
			left.sizeDelta =
				horizontalSize;

		if (right != null)
			right.sizeDelta =
				horizontalSize;

		if (top != null)
			top.sizeDelta =
				verticalSize;

		if (bottom != null)
			bottom.sizeDelta =
				verticalSize;
	}
}