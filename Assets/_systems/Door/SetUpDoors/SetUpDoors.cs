using System.Collections;
using UnityEngine;

public sealed class SetUpDoors : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Transform target;

	[Header("Scale")]
	[SerializeField] private Vector3 setupScale = Vector3.one;
	[SerializeField] private Vector3 roundScale = Vector3.zero;

	[Header("Transition")]
	[SerializeField, Min(0f)]
	private float transitionDuration = 1f;

	[SerializeField, Min(0f)]
	private float lockdownTransitionDuration = 0.25f;

	private Coroutine scaleRoutine;

	private void OnEnable()
	{
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted +=
				HandleRoundSetupStarted;

			GameFlowManager.Instance.OnRoundStarted +=
				HandleRoundStarted;
		}

		if (LockdownManager.Instance != null)
		{
			LockdownManager.Instance.OnLockdownStarted +=
				HandleLockdownStarted;

			LockdownManager.Instance.OnLockdownEnded +=
				HandleLockdownEnded;
		}
	}

	private void OnDisable()
	{
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted -=
				HandleRoundSetupStarted;

			GameFlowManager.Instance.OnRoundStarted -=
				HandleRoundStarted;
		}

		if (LockdownManager.Instance != null)
		{
			LockdownManager.Instance.OnLockdownStarted -=
				HandleLockdownStarted;

			LockdownManager.Instance.OnLockdownEnded -=
				HandleLockdownEnded;
		}

		StopScaleRoutine();
	}

	private void HandleRoundSetupStarted(int round)
	{
		if (target == null)
			return;

		StopScaleRoutine();

		target.gameObject.SetActive(true);
		target.localScale = setupScale;
	}

	private void HandleRoundStarted(int round)
	{
		OpenDoors();
	}

	private void HandleLockdownStarted()
	{
		CloseDoorsForLockdown();
	}

	private void HandleLockdownEnded()
	{
		OpenDoors();
	}

	private void CloseDoorsForLockdown()
	{
		if (target == null)
			return;

		StopScaleRoutine();

		target.gameObject.SetActive(true);

		scaleRoutine = StartCoroutine(
			LerpScale(
				target.localScale,
				setupScale,
				lockdownTransitionDuration,
				false));
	}

	private void OpenDoors()
	{
		if (target == null)
			return;

		StopScaleRoutine();

		target.gameObject.SetActive(true);

		scaleRoutine = StartCoroutine(
			LerpScale(
				target.localScale,
				roundScale,
				transitionDuration,
				true));
	}

	private IEnumerator LerpScale(
		Vector3 startScale,
		Vector3 endScale,
		float duration,
		bool disableWhenFinished)
	{
		if (duration <= 0f)
		{
			target.localScale = endScale;

			if (disableWhenFinished)
				target.gameObject.SetActive(false);

			scaleRoutine = null;
			yield break;
		}

		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;

			float t = Mathf.Clamp01(
				elapsed / duration);

			target.localScale = Vector3.Lerp(
				startScale,
				endScale,
				t);

			yield return null;
		}

		target.localScale = endScale;

		if (disableWhenFinished)
			target.gameObject.SetActive(false);

		scaleRoutine = null;
	}

	private void StopScaleRoutine()
	{
		if (scaleRoutine == null)
			return;

		StopCoroutine(scaleRoutine);
		scaleRoutine = null;
	}
}