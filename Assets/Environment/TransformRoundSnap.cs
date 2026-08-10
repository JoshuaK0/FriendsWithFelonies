using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TransformRoundSnap : MonoBehaviour
{
	[SerializeField] private float gridSize = 1f;
	[SerializeField] private float shiftGridSize = 0.25f;

	private Vector3 lastPosition;
	private Vector3 lastScale;

#if UNITY_EDITOR
	private static bool shiftHeld;
#endif

	private void OnEnable()
	{
		if (Application.isPlaying)
		{
			enabled = false;
			return;
		}

		lastPosition = transform.localPosition;
		lastScale = transform.localScale;

#if UNITY_EDITOR
		SceneView.duringSceneGui += OnSceneGUI;
#endif
	}

	private void OnDisable()
	{
#if UNITY_EDITOR
		SceneView.duringSceneGui -= OnSceneGUI;
#endif
	}

#if UNITY_EDITOR
	private static void OnSceneGUI(SceneView sceneView)
	{
		shiftHeld = Event.current.shift;
	}
#endif

	private void Update()
	{
		if (Application.isPlaying)
			return;

		if (!transform.hasChanged)
			return;

		float snapSize = gridSize;

#if UNITY_EDITOR
		if (shiftHeld)
			snapSize = shiftGridSize;
#endif

		Vector3 currentPosition = transform.localPosition;
		Vector3 currentScale = transform.localScale;

		// Only snap position if position changed.
		if (currentPosition != lastPosition)
		{
			currentPosition = SnapVector(currentPosition, snapSize);
			transform.localPosition = currentPosition;
		}

		// Only snap scale if scale changed.
		if (currentScale != lastScale)
		{
			currentScale = SnapVector(currentScale, snapSize);
			transform.localScale = currentScale;
		}

		lastPosition = transform.localPosition;
		lastScale = transform.localScale;

		transform.hasChanged = false;
	}

	private static Vector3 SnapVector(
		Vector3 value,
		float gridSize)
	{
		return new Vector3(
			Snap(value.x, gridSize),
			Snap(value.y, gridSize),
			Snap(value.z, gridSize)
		);
	}

	private static float Snap(float value, float gridSize)
	{
		if (gridSize <= 0f)
			return value;

		return Mathf.Round(value / gridSize) * gridSize;
	}
}