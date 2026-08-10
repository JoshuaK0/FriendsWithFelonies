using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public enum RoundFlowPhase : byte
{
	None,
	Setup,
	Ready,
	Active,
	Finished
}

/// <summary>
/// Owns the setup countdown, active countdown, and end state for one round.
/// Both timed phases reuse the shared networked RoundTimer.
/// </summary>
public class RoundFlowController : NetworkBehaviour
{
	private readonly SyncVar<RoundFlowPhase> phase =
		new(RoundFlowPhase.None);

	private readonly SyncVar<RoundEndReason> endReason =
		new(RoundEndReason.None);

	private readonly SyncVar<int> scoreMultiplier = new(1);

	public RoundFlowPhase Phase => phase.Value;
	public RoundEndReason EndReason => endReason.Value;
	public int ScoreMultiplier => scoreMultiplier.Value;

	public bool IsSetupRunning =>
		phase.Value == RoundFlowPhase.Setup;

	public bool IsSetupFinished =>
		phase.Value == RoundFlowPhase.Ready;

	public bool IsRoundRunning =>
		phase.Value == RoundFlowPhase.Active;

	public bool IsRoundFinished =>
		phase.Value == RoundFlowPhase.Finished;

	public event Action<RoundFlowPhase> OnPhaseChanged;
	public event Action<RoundEndReason> OnEndReasonChanged;

	private bool timerSubscribed;

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		phase.OnChange += HandlePhaseChanged;
		endReason.OnChange += HandleEndReasonChanged;
	}

	public override void OnStopNetwork()
	{
		phase.OnChange -= HandlePhaseChanged;
		endReason.OnChange -= HandleEndReasonChanged;

		UnsubscribeFromTimer();
		base.OnStopNetwork();
	}

	/// <summary>
	/// Spawns the players and begins the pre-round setup countdown.
	/// </summary>
	[Server]
	public bool StartSetup(
		float duration,
		out string failureReason)
	{
		if (!ValidateTimer(out failureReason))
			return false;

		if (phase.Value != RoundFlowPhase.None &&
		    phase.Value != RoundFlowPhase.Finished)
		{
			failureReason =
				$"Cannot start setup while phase is {phase.Value}.";
			return false;
		}

		SubscribeToTimer();
		SpawnPlayersObserversRpc();

		endReason.Value = RoundEndReason.None;
		scoreMultiplier.Value = 1;
		phase.Value = RoundFlowPhase.Setup;
		StartCountdown(duration);

		failureReason = null;
		return true;
	}

	/// <summary>
	/// Begins the active round after the setup countdown has finished.
	/// </summary>
	[Server]
	public bool StartRound(
		float duration,
		out string failureReason)
	{
		if (!ValidateTimer(out failureReason))
			return false;

		if (phase.Value != RoundFlowPhase.Ready)
		{
			failureReason =
				$"Cannot start the round while phase is {phase.Value}.";
			return false;
		}

		endReason.Value = RoundEndReason.None;
		scoreMultiplier.Value = 1;
		phase.Value = RoundFlowPhase.Active;
		StartCountdown(duration);

		failureReason = null;
		return true;
	}

	/// <summary>
	/// Ends the active round for a non-timer gameplay condition.
	/// The first successful call wins; later calls return false.
	/// </summary>
	[Server]
	public bool TryFinishRound(
		RoundEndReason reason,
		int resultScoreMultiplier = 1)
	{
		Debug.Log(
			$"TryFinishRound: reason={reason}, " +
			$"phase={phase.Value}, " +
			$"multiplier={resultScoreMultiplier}");

		if (reason == RoundEndReason.None ||
			reason == RoundEndReason.TimeExpired)
		{
			Debug.LogWarning(
				$"{reason} cannot be submitted as an external round result.");
			return false;
		}

		if (phase.Value != RoundFlowPhase.Active)
		{
			Debug.LogWarning(
				$"TryFinishRound rejected because phase is {phase.Value}, " +
				$"expected {RoundFlowPhase.Active}.");

			return false;
		}

		FinishActiveRound(
			reason,
			stopTimer: true,
			resultScoreMultiplier: resultScoreMultiplier);

		return true;
	}

	[Server]
	public void StopRound()
	{
		if (RoundTimer.Instance != null)
			RoundTimer.Instance.StopTimer();

		UnsubscribeFromTimer();
		phase.Value = RoundFlowPhase.None;
	}

	[Server]
	private void StartCountdown(float duration)
	{
		RoundTimer.Instance.SetDuration(duration);
		RoundTimer.Instance.ResetTimer();
		RoundTimer.Instance.StartTimer();
	}

	private void HandleTimerFinished()
	{
		switch (phase.Value)
		{
			case RoundFlowPhase.Setup:
				phase.Value = RoundFlowPhase.Ready;
				break;

			case RoundFlowPhase.Active:
				FinishActiveRound(
					RoundEndReason.TimeExpired,
					stopTimer: false,
					resultScoreMultiplier: 1);
				break;
		}
	}

	[Server]
	private void FinishActiveRound(
		RoundEndReason reason,
		bool stopTimer,
		int resultScoreMultiplier)
	{
		if (phase.Value != RoundFlowPhase.Active)
			return;

		if (stopTimer && RoundTimer.Instance != null)
			RoundTimer.Instance.StopTimer();

		endReason.Value = reason;
		scoreMultiplier.Value = Mathf.Max(1, resultScoreMultiplier);
		phase.Value = RoundFlowPhase.Finished;
	}

	private void HandlePhaseChanged(
		RoundFlowPhase previous,
		RoundFlowPhase next,
		bool asServer)
	{
		OnPhaseChanged?.Invoke(next);
	}

	private void HandleEndReasonChanged(
		RoundEndReason previous,
		RoundEndReason next,
		bool asServer)
	{
		OnEndReasonChanged?.Invoke(next);
	}

	private bool ValidateTimer(out string failureReason)
	{
		if (RoundTimer.Instance == null)
		{
			failureReason =
				"RoundFlowController requires RoundTimer.Instance.";
			return false;
		}

		failureReason = null;
		return true;
	}

	private void SubscribeToTimer()
	{
		if (timerSubscribed || RoundTimer.Instance == null)
			return;

		RoundTimer.Instance.OnTimerFinished += HandleTimerFinished;
		timerSubscribed = true;
	}

	private void UnsubscribeFromTimer()
	{
		if (!timerSubscribed)
			return;

		if (RoundTimer.Instance != null)
		{
			RoundTimer.Instance.OnTimerFinished -=
				HandleTimerFinished;
		}

		timerSubscribed = false;
	}

	[ObserversRpc]
	private void SpawnPlayersObserversRpc()
	{
		if (PlayerManager.Instance == null)
		{
			Debug.LogError(
				"Cannot spawn the local player because " +
				"PlayerManager.Instance is null.");
			return;
		}

		PlayerManager.Instance.SpawnPlayer();
	}
}
