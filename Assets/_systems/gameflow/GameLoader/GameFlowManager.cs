using System;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Controls the order of the match.
///
/// Each round runs through a setup countdown before the active countdown.
/// Keep phase-specific initialization in the clearly named methods near the
/// bottom of this class. Complex implementation remains in focused services.
/// </summary>
public class GameFlowManager : NetworkBehaviour
{
	public static GameFlowManager Instance { get; private set; }

	[Header("Game Settings")]
	[SerializeField, Min(1)]
	private int totalRounds = 4;

	[SerializeField, Min(0f)]
	private float setupDuration = 15f;

	[SerializeField, Min(0f)]
	private float roundDuration = 180f;

	[SerializeField, Min(0f)]
	private float nextRoundDelay = 5f;

	[Header("Flow Services")]
	[SerializeField]
	private InitialTeamCoordinator initialTeamCoordinator;

	[SerializeField]
	private RoundFlowController roundFlowController;

	[SerializeField]
	private MatchRules matchRules;

	private readonly SyncVar<int> currentRound = new();
	private readonly SyncVar<bool> gameRunning = new();

	public int TotalRounds => totalRounds;
	public int CurrentRound => currentRound.Value;
	public float SetupDuration => setupDuration;
	public float RoundDuration => roundDuration;
	public float NextRoundDelay => nextRoundDelay;
	public bool GameRunning => gameRunning.Value;

	/// <summary>
	/// Echoes the current phase owned by RoundFlowController.
	/// </summary>
	public RoundFlowPhase RoundPhase =>
		roundFlowController != null
			? roundFlowController.Phase
			: RoundFlowPhase.None;

	// Network-facing events.

	/// <summary>
	/// Runs once for the lifetime of this GameFlowManager,
	/// immediately before the first game starts.
	/// </summary>
	public event Action OnInit;

	/// <summary>
	/// Runs every time a game starts.
	/// </summary>
	public event Action OnGameStart;

	public event Action OnGameStarted;
	public event Action OnGameFinished;

	public event Action<int> OnRoundSetupStarted;
	public event Action<int> OnRoundStarted;
	public event Action<int> OnRoundFinished;
	public event Action<int, RoundEndReason> OnRoundEnded;

	/// <summary>
	/// Fired on every client when the cops win a round.
	/// </summary>
	public event Action OnCopsWin;

	/// <summary>
	/// Fired on every client when the robbers win a round.
	/// </summary>
	public event Action OnRobbersWin;

	public event Action OnTeamTypesCycled;

	/// <summary>
	/// Echoes RoundFlowController.OnPhaseChanged.
	/// Raised whenever the round phase changes.
	/// </summary>
	public event Action<RoundFlowPhase> OnRoundPhaseChanged;

	private Coroutine gameCoroutine;
	private bool stopRequested;

	// Prevents OnInit from running more than once.
	private bool hasInitialized;

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

		currentRound.OnChange += HandleRoundChanged;
		gameRunning.OnChange += HandleGameRunningChanged;

		if (roundFlowController != null)
		{
			roundFlowController.OnPhaseChanged +=
				HandleRoundPhaseChanged;
		}
	}

	public override void OnStopServer()
	{
		StopFlowImmediately(cleanUpServer: true);

		base.OnStopServer();
	}

	public override void OnStopNetwork()
	{
		currentRound.OnChange -= HandleRoundChanged;
		gameRunning.OnChange -= HandleGameRunningChanged;

		if (roundFlowController != null)
		{
			roundFlowController.OnPhaseChanged -=
				HandleRoundPhaseChanged;
		}

		StopFlowImmediately(cleanUpServer: false);

		base.OnStopNetwork();
	}

	[Server]
	public void StartGame()
	{
		if (gameCoroutine != null || gameRunning.Value)
			return;

		if (!ValidateReferences())
			return;

		stopRequested = false;

		gameCoroutine =
			StartCoroutine(RunGameCoroutine());
	}

	[Server]
	public void StopGame()
	{
		if (gameCoroutine == null && !gameRunning.Value)
			return;

		stopRequested = true;

		initialTeamCoordinator.Cancel();
		roundFlowController.StopRound();
	}

	/// <summary>
	/// Call this from the server-side capture system once
	/// every robber has been captured.
	/// </summary>
	[Server]
	public bool ReportAllRobbersCaptured()
	{
		return TryEndRound(
			RoundEndReason.AllRobbersCaptured,
			scoreMultiplier: 1);
	}

	/// <summary>
	/// Called by the server-authoritative loot capture zone.
	/// The robber reward is multiplied by the amount of
	/// loot captured.
	/// </summary>
	[Server]
	public bool ReportLootStolen(int capturedLootCount)
	{
		if (capturedLootCount <= 0)
			return false;

		return TryEndRound(
			RoundEndReason.LootStolen,
			capturedLootCount);
	}

	[Server]
	private bool TryEndRound(
		RoundEndReason reason,
		int scoreMultiplier)
	{
		Debug.Log(
			$"TryEndRound: reason={reason}, " +
			$"multiplier={scoreMultiplier}, " +
			$"gameRunning={gameRunning.Value}, " +
			$"roundFlowController={(roundFlowController != null)}");

		if (!gameRunning.Value)
		{
			Debug.LogWarning(
				"TryEndRound rejected: gameRunning is false.");

			return false;
		}

		if (roundFlowController == null)
		{
			Debug.LogWarning(
				"TryEndRound rejected: roundFlowController is null.");

			return false;
		}

		bool result =
			roundFlowController.TryFinishRound(
				reason,
				scoreMultiplier);

		Debug.Log(
			$"RoundFlowController.TryFinishRound returned {result}");

		return result;
	}

	[Server]
	private IEnumerator RunGameCoroutine()
	{
		currentRound.Value = 0;

		// Clients confirm that their initial team
		// assignments have replicated.
		yield return initialTeamCoordinator.InitializeTeams();

		if (stopRequested)
		{
			FinishGame();
			yield break;
		}

		if (!initialTeamCoordinator.Succeeded)
		{
			AbortGame(
				initialTeamCoordinator.FailureReason);

			yield break;
		}

		InitializeGame();

		// Runs once, immediately before the first game starts.
		if (!hasInitialized)
		{
			hasInitialized = true;
			InitObserversRpc();
		}

		// Runs every time a game starts.
		GameStartObserversRpc();

		gameRunning.Value = true;

		for (int round = 1;
			 round <= totalRounds;
			 round++)
		{
			if (stopRequested)
				break;

			InitializeRoundSetup(round);

			if (!roundFlowController.StartSetup(
					setupDuration,
					out string setupError))
			{
				AbortGame(setupError);
				yield break;
			}

			// Changing the round means that its
			// setup phase has started.
			currentRound.Value = round;

			while (!roundFlowController.IsSetupFinished &&
				   !stopRequested)
			{
				yield return null;
			}

			if (stopRequested)
				break;

			FinalizeRoundSetup(round);

			if (!roundFlowController.StartRound(
					roundDuration,
					out string roundError))
			{
				AbortGame(roundError);
				yield break;
			}

			RoundStartedObserversRpc(round);

			while (!roundFlowController.IsRoundFinished &&
				   !stopRequested)
			{
				yield return null;
			}

			if (stopRequested)
			{
				roundFlowController.StopRound();
				break;
			}

			RoundEndReason endReason =
				roundFlowController.EndReason;

			int scoreMultiplier =
				roundFlowController.ScoreMultiplier;

			roundFlowController.StopRound();

			FinalizeRound(
				round,
				endReason,
				scoreMultiplier);

			RoundFinishedObserversRpc(
				round,
				endReason);

			// Keep the finished round state visible before
			// preparing the next round.
			// Do not delay after the final round.
			if (round < totalRounds &&
				nextRoundDelay > 0f)
			{
				yield return new WaitForSeconds(
					nextRoundDelay);

				if (stopRequested)
					break;
			}

			if (ShouldRotateTeamsAfter(round))
			{
				if (!matchRules.RotateTeams(
						out string rotationError))
				{
					AbortGame(rotationError);
					yield break;
				}

				InitializeAfterTeamRotation();

				TeamTypesCycledObserversRpc();
			}
		}

		FinishGame();
	}

	[Server]
	private void AbortGame(string reason)
	{
		Debug.LogError(
			"Game flow aborted. " + reason);

		stopRequested = true;

		FinishGame();
	}

	[Server]
	private void FinishGame()
	{
		roundFlowController?.StopRound();
		initialTeamCoordinator?.Cancel();

		FinalizeGame();

		gameRunning.Value = false;
		currentRound.Value = 0;

		stopRequested = false;
		gameCoroutine = null;
	}

	private bool ValidateReferences()
	{
		if (initialTeamCoordinator == null)
		{
			Debug.LogError(
				"GameFlowManager requires an InitialTeamCoordinator.");

			return false;
		}

		if (roundFlowController == null)
		{
			Debug.LogError(
				"GameFlowManager requires a RoundFlowController.");

			return false;
		}

		if (matchRules == null)
		{
			Debug.LogError(
				"GameFlowManager requires MatchRules.");

			return false;
		}

		return true;
	}

	private bool ShouldRotateTeamsAfter(int round)
	{
		return totalRounds > 1 &&
			   round == totalRounds / 2 &&
			   round < totalRounds;
	}

	private void StopFlowImmediately(
		bool cleanUpServer)
	{
		if (gameCoroutine != null)
		{
			StopCoroutine(gameCoroutine);
			gameCoroutine = null;
		}

		if (cleanUpServer)
		{
			initialTeamCoordinator?.Cancel();
			roundFlowController?.StopRound();
		}

		stopRequested = true;
	}

	#region Initialization Points

	/// <summary>
	/// Runs once after teams have synchronized
	/// and before the match starts.
	/// </summary>
	[Server]
	private void InitializeGame()
	{
		// Example:
		// TeamScores.Instance.ResetScores();
		// ObjectiveManager.Instance.CreateObjectives();
	}

	/// <summary>
	/// Runs immediately before each setup countdown begins.
	/// Players are spawned when StartSetup is called.
	/// </summary>
	[Server]
	private void InitializeRoundSetup(int round)
	{
		// Example:
		// ObjectiveManager.Instance.ResetObjectives();
		// LoadoutManager.Instance.AssignLoadouts();
	}

	/// <summary>
	/// Runs after setup reaches zero and immediately
	/// before active play begins.
	/// </summary>
	[Server]
	private void FinalizeRoundSetup(int round)
	{
		// Example:
		// PlayerManager.UnlockRoundMovementForAllPlayers();
		// ObjectiveManager.Instance.EnableObjectives();
	}

	/// <summary>
	/// Runs after any active-round end condition is reached.
	/// </summary>
	[Server]
	private void FinalizeRound(
		int round,
		RoundEndReason endReason,
		int scoreMultiplier)
	{
		matchRules.AwardRoundResult(
			endReason,
			scoreMultiplier);

		// Add round cleanup or result processing here.
	}

	/// <summary>
	/// Runs after the team roles have rotated.
	/// </summary>
	[Server]
	private void InitializeAfterTeamRotation()
	{
		// Example:
		// LoadoutManager.Instance.RefreshRoleLoadouts();
	}

	/// <summary>
	/// Runs once when the match finishes,
	/// is stopped, or is aborted.
	/// </summary>
	[Server]
	private void FinalizeGame()
	{
		// Example:
		// ObjectiveManager.Instance.ClearObjectives();
		// PlayerManager.Instance.ReturnToLobbyState();
	}

	#endregion

	#region Network Events

	private void HandleRoundChanged(
		int previous,
		int next,
		bool asServer)
	{
		if (next > previous && next > 0)
			OnRoundSetupStarted?.Invoke(next);
	}

	private void HandleGameRunningChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		if (next)
		{
			OnGameStarted?.Invoke();
		}
		else if (previous)
		{
			OnGameFinished?.Invoke();
		}
	}

	private void HandleRoundPhaseChanged(
		RoundFlowPhase phase)
	{
		OnRoundPhaseChanged?.Invoke(phase);
	}

	/// <summary>
	/// Fired once for every currently connected client
	/// before the first game starts.
	/// </summary>
	[ObserversRpc(RunLocally = true)]
	private void InitObserversRpc()
	{
		OnInit?.Invoke();
	}

	/// <summary>
	/// Fired every time a new game starts.
	/// </summary>
	[ObserversRpc(RunLocally = true)]
	private void GameStartObserversRpc()
	{
		OnGameStart?.Invoke();
	}

	[ObserversRpc(RunLocally = true)]
	private void RoundStartedObserversRpc(
		int round)
	{
		OnRoundStarted?.Invoke(round);
	}

	[ObserversRpc(RunLocally = true)]
	private void RoundFinishedObserversRpc(
		int round,
		RoundEndReason endReason)
	{
		OnRoundFinished?.Invoke(round);
		OnRoundEnded?.Invoke(round, endReason);

		switch (endReason)
		{
			case RoundEndReason.AllRobbersCaptured:
				OnCopsWin?.Invoke();
				break;

			case RoundEndReason.LootStolen:
				OnRobbersWin?.Invoke();
				break;

			case RoundEndReason.TimeExpired:
				OnCopsWin?.Invoke();
				break;
		}
	}

	[ObserversRpc(RunLocally = true)]
	private void TeamTypesCycledObserversRpc()
	{
		OnTeamTypesCycled?.Invoke();
	}

	#endregion
}