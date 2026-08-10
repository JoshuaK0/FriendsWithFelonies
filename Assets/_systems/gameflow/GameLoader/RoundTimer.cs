using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class RoundTimer : NetworkBehaviour
{
	public static RoundTimer Instance { get; private set; }

	private readonly SyncVar<float> duration = new(300f);
	private readonly SyncVar<float> timeRemaining = new(300f);
	private readonly SyncVar<bool> isRunning = new(false);

	public float Duration => duration.Value;
	public float TimeRemaining => timeRemaining.Value;
	public bool IsRunning => isRunning.Value;

	public event Action OnTimerStarted;
	public event Action OnTimerStopped;
	public event Action OnTimerReset;
	public event Action OnTimerFinished;
	public event Action<float> OnTimeChanged;
	public event Action<float> OnDurationChanged;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		duration.OnChange += HandleDurationChanged;
		timeRemaining.OnChange += HandleTimeChanged;
		isRunning.OnChange += HandleRunningChanged;
	}

	public override void OnStopNetwork()
	{
		duration.OnChange -= HandleDurationChanged;
		timeRemaining.OnChange -= HandleTimeChanged;
		isRunning.OnChange -= HandleRunningChanged;

		base.OnStopNetwork();
	}

	private void Update()
	{
		if (!IsServerStarted || !isRunning.Value)
			return;

		timeRemaining.Value -= Time.deltaTime;

		if (timeRemaining.Value > 0f)
			return;

		timeRemaining.Value = 0f;
		isRunning.Value = false;

		// RunLocally is important: the server-side round flow is also
		// subscribed to this event, including on a dedicated server.
		TimerFinishedObserversRpc();
	}

	[Server]
	public void StartTimer()
	{
		if (timeRemaining.Value <= 0f)
			timeRemaining.Value = duration.Value;

		isRunning.Value = true;
	}

	[Server]
	public void StopTimer()
	{
		isRunning.Value = false;
	}

	[Server]
	public void ResetTimer()
	{
		isRunning.Value = false;
		timeRemaining.Value = duration.Value;
		TimerResetObserversRpc();
	}

	[Server]
	public void SetDuration(float newDuration)
	{
		duration.Value = Mathf.Max(0f, newDuration);
		timeRemaining.Value = duration.Value;
	}

	public int GetSecondsRemaining()
	{
		return Mathf.CeilToInt(timeRemaining.Value);
	}

	public string GetFormattedTime()
	{
		int totalSeconds = GetSecondsRemaining();
		int minutes = totalSeconds / 60;
		int seconds = totalSeconds % 60;

		return $"{minutes:00}:{seconds:00}";
	}

	private void HandleDurationChanged(
		float previous,
		float next,
		bool asServer)
	{
		OnDurationChanged?.Invoke(next);
	}

	private void HandleTimeChanged(
		float previous,
		float next,
		bool asServer)
	{
		OnTimeChanged?.Invoke(next);
	}

	private void HandleRunningChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		if (next)
			OnTimerStarted?.Invoke();
		else if (previous)
			OnTimerStopped?.Invoke();
	}

	[ObserversRpc(RunLocally = true)]
	private void TimerResetObserversRpc()
	{
		OnTimerReset?.Invoke();
	}

	[ObserversRpc(RunLocally = true)]
	private void TimerFinishedObserversRpc()
	{
		OnTimerFinished?.Invoke();
	}
}
