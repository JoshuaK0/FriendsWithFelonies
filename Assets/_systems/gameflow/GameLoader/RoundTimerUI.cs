using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundTimerUI : MonoBehaviour
{
	[Header("Timer Reference")]
	[Tooltip("Optional. If left empty, RoundTimer.Instance will be used.")]
	[SerializeField]
	private RoundTimer roundTimer;

	[Header("UI References")]
	[SerializeField]
	private TMP_Text timerText;

	[Tooltip("Optional image using Image Type: Filled.")]
	[SerializeField]
	private Image progressFill;

	[Tooltip("Optional object enabled while the timer is running.")]
	[SerializeField]
	private GameObject runningIndicator;

	[Tooltip("Optional object enabled when the timer reaches zero.")]
	[SerializeField]
	private GameObject finishedIndicator;

	[Header("Display")]
	[SerializeField]
	private string unavailableText = "--:--";

	[SerializeField]
	private bool showHours;

	private RoundTimer subscribedTimer;

	private void OnEnable()
	{
		TryBindTimer();
	}

	private void OnDisable()
	{
		UnbindTimer();
	}

	private void Update()
	{
		// The networked RoundTimer may spawn after this UI object.
		if (subscribedTimer == null)
			TryBindTimer();
	}

	private void TryBindTimer()
	{
		RoundTimer timerToBind = roundTimer;

		if (timerToBind == null)
			timerToBind = RoundTimer.Instance;

		if (timerToBind == null)
		{
			SetUnavailableState();
			return;
		}

		if (subscribedTimer == timerToBind)
			return;

		UnbindTimer();

		subscribedTimer = timerToBind;
		roundTimer = timerToBind;

		subscribedTimer.OnTimerStarted += HandleTimerStarted;
		subscribedTimer.OnTimerStopped += HandleTimerStopped;
		subscribedTimer.OnTimerReset += HandleTimerReset;
		subscribedTimer.OnTimerFinished += HandleTimerFinished;
		subscribedTimer.OnTimeChanged += HandleTimeChanged;
		subscribedTimer.OnDurationChanged += HandleDurationChanged;

		RefreshAll();
	}

	private void UnbindTimer()
	{
		if (subscribedTimer == null)
			return;

		subscribedTimer.OnTimerStarted -= HandleTimerStarted;
		subscribedTimer.OnTimerStopped -= HandleTimerStopped;
		subscribedTimer.OnTimerReset -= HandleTimerReset;
		subscribedTimer.OnTimerFinished -= HandleTimerFinished;
		subscribedTimer.OnTimeChanged -= HandleTimeChanged;
		subscribedTimer.OnDurationChanged -= HandleDurationChanged;

		subscribedTimer = null;
	}

	private void RefreshAll()
	{
		if (subscribedTimer == null)
		{
			SetUnavailableState();
			return;
		}

		UpdateTimeDisplay(subscribedTimer.TimeRemaining);
		UpdateProgress(
			subscribedTimer.TimeRemaining,
			subscribedTimer.Duration);

		SetRunningState(subscribedTimer.IsRunning);

		bool isFinished =
			!subscribedTimer.IsRunning &&
			subscribedTimer.TimeRemaining <= 0f;

		SetFinishedState(isFinished);
	}

	private void HandleTimerStarted()
	{
		SetRunningState(true);
		SetFinishedState(false);

		if (subscribedTimer != null)
		{
			UpdateTimeDisplay(subscribedTimer.TimeRemaining);
			UpdateProgress(
				subscribedTimer.TimeRemaining,
				subscribedTimer.Duration);
		}
	}

	private void HandleTimerStopped()
	{
		SetRunningState(false);
	}

	private void HandleTimerReset()
	{
		if (subscribedTimer == null)
			return;

		UpdateTimeDisplay(subscribedTimer.TimeRemaining);
		UpdateProgress(
			subscribedTimer.TimeRemaining,
			subscribedTimer.Duration);

		SetRunningState(false);
		SetFinishedState(false);
	}

	private void HandleTimerFinished()
	{
		UpdateTimeDisplay(0f);
		UpdateProgress(0f, subscribedTimer != null
			? subscribedTimer.Duration
			: 0f);

		SetRunningState(false);
		SetFinishedState(true);
	}

	private void HandleTimeChanged(float timeRemaining)
	{
		UpdateTimeDisplay(timeRemaining);

		if (subscribedTimer != null)
		{
			UpdateProgress(
				timeRemaining,
				subscribedTimer.Duration);
		}
	}

	private void HandleDurationChanged(float duration)
	{
		if (subscribedTimer == null)
			return;

		UpdateProgress(
			subscribedTimer.TimeRemaining,
			duration);
	}

	private void UpdateTimeDisplay(float timeRemaining)
	{
		if (timerText == null)
			return;

		int totalSeconds =
			Mathf.Max(0, Mathf.CeilToInt(timeRemaining));

		if (showHours)
		{
			int hours = totalSeconds / 3600;
			int minutes = totalSeconds % 3600 / 60;
			int seconds = totalSeconds % 60;

			timerText.text =
				$"{hours:00}:{minutes:00}:{seconds:00}";
		}
		else
		{
			int minutes = totalSeconds / 60;
			int seconds = totalSeconds % 60;

			timerText.text = $"{minutes:00}:{seconds:00}";
		}
	}

	private void UpdateProgress(
		float timeRemaining,
		float duration)
	{
		if (progressFill == null)
			return;

		progressFill.fillAmount = duration > 0f
			? Mathf.Clamp01(timeRemaining / duration)
			: 0f;
	}

	private void SetRunningState(bool running)
	{
		if (runningIndicator != null)
			runningIndicator.SetActive(running);
	}

	private void SetFinishedState(bool finished)
	{
		if (finishedIndicator != null)
			finishedIndicator.SetActive(finished);
	}

	private void SetUnavailableState()
	{
		if (timerText != null)
			timerText.text = unavailableText;

		if (progressFill != null)
			progressFill.fillAmount = 0f;

		SetRunningState(false);
		SetFinishedState(false);
	}
}