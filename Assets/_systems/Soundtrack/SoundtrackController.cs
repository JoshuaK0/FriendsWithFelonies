using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public enum SoundtrackState : byte
{
	None,
	Setup,
	Active,
	Lockdown,
	LightSabotage,
	CopsWin,
	RobbersWin
}

[RequireComponent(typeof(SoundtrackAudioPlayer))]
public sealed class SoundtrackController : NetworkBehaviour
{
	private sealed class PlaybackMemory
	{
		public int TrackIndex = -1;
		public float PlaybackTime;
	}

	public static SoundtrackController Instance { get; private set; }

	[Header("Managers")]
	[SerializeField]
	private GameFlowManager gameFlowManager;

	[SerializeField]
	private LockdownManager lockdownManager;

	[SerializeField]
	private LightSabotageManager lightSabotageManager;

	[Header("Audio")]
	[SerializeField]
	private SoundtrackAudioPlayer audioPlayer;

	[Header("Setup Music")]
	[SerializeField]
	private SoundtrackPlaylist setupPlaylist =
		new SoundtrackPlaylist();

	[Header("Active Music")]
	[SerializeField]
	private SoundtrackPlaylist activePlaylist =
		new SoundtrackPlaylist();

	[Header("Lockdown Music")]
	[SerializeField]
	private SoundtrackPlaylist lockdownPlaylist =
		new SoundtrackPlaylist();

	[Tooltip(
		"Crossfade when lockdown starts. " +
		"-1 uses the default crossfade.")]
	[SerializeField, Min(-1f)]
	private float lockdownStartCrossFadeDuration = -1f;

	[Tooltip(
		"Crossfade when lockdown ends. " +
		"-1 uses the default crossfade.")]
	[SerializeField, Min(-1f)]
	private float lockdownEndCrossFadeDuration = -1f;

	[Header("Light Sabotage Music")]
	[SerializeField]
	private SoundtrackPlaylist lightSabotagePlaylist =
		new SoundtrackPlaylist();

	[Tooltip(
		"How long the current soundtrack takes to fade to silence.")]
	[SerializeField, Min(0f)]
	private float lightSabotageFadeOutDuration = 1f;

	[Tooltip(
		"How long to remain silent before sabotage music begins.")]
	[SerializeField, Min(0f)]
	private float lightSabotageDelay = 2f;

	[Tooltip(
		"Fade-in duration when sabotage music begins. " +
		"-1 uses the default crossfade.")]
	[SerializeField, Min(-1f)]
	private float lightSabotageCrossFadeDuration = -1f;

	[Tooltip(
		"Crossfade when power is restored. " +
		"-1 uses the default crossfade.")]
	[SerializeField, Min(-1f)]
	private float lightRestoreCrossFadeDuration = -1f;

	[Header("Win Music")]
	[SerializeField]
	private SoundtrackTrack copsWinTrack;

	[SerializeField]
	private SoundtrackTrack robbersWinTrack;

	[Header("Crossfade")]
	[SerializeField, Min(0f)]
	private float defaultCrossFadeDuration = 2f;

	[Header("Winner Detection")]
	[SerializeField]
	private RoundEndReason[] copsWinReasons =
	{
		RoundEndReason.AllRobbersCaptured
	};

	[SerializeField]
	private RoundEndReason[] robbersWinReasons =
	{
		RoundEndReason.LootStolen
	};

	[Header("Debug")]
	[SerializeField]
	private bool enableLogs = true;

	private readonly Dictionary<
		SoundtrackState,
		PlaybackMemory> playbackMemories = new();

	private Coroutine playlistCoroutine;
	private Coroutine lightSabotageDelayCoroutine;

	private SoundtrackState serverState =
		SoundtrackState.None;

	private int serverTrackIndex = -1;

	private uint serverTrackStartTick;
	private float serverTrackStartPosition;
	private bool serverTrackClockRunning;

	private bool lightSabotageMusicActive;
	private bool lightSabotagePending;

	private SoundtrackState preSabotageState =
		SoundtrackState.None;

	private bool gameFlowEventsRegistered;
	private bool lockdownEventsRegistered;
	private bool lightSabotageEventsRegistered;

	private GameFlowManager initEventSource;

	public SoundtrackState CurrentState =>
		serverState;

	public int CurrentTrackIndex =>
		serverTrackIndex;

	public bool IsLocallyMuted =>
		audioPlayer != null &&
		audioPlayer.IsLocallyMuted;

	public float DefaultCrossFadeDuration =>
		defaultCrossFadeDuration;

	private void Awake()
	{
		if (Instance != null &&
			Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;

		if (audioPlayer == null)
		{
			audioPlayer =
				GetComponent<SoundtrackAudioPlayer>();
		}

		SubscribeToGameFlowInit();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		SubscribeToGameFlowInit();
	}

	public override void OnStopServer()
	{
		UnregisterServerEvents();

		CancelLightSabotageDelay();
		StopPlaylistCoroutine();

		serverTrackClockRunning = false;

		base.OnStopServer();
	}

	private void OnDestroy()
	{
		if (initEventSource != null)
		{
			initEventSource.OnInit -=
				HandleGameFlowInitialized;
		}

		if (Instance == this)
			Instance = null;
	}

	private void SubscribeToGameFlowInit()
	{
		GameFlowManager current =
			GameFlowManager.Instance;

		if (current == null ||
			initEventSource == current)
		{
			return;
		}

		if (initEventSource != null)
		{
			initEventSource.OnInit -=
				HandleGameFlowInitialized;
		}

		initEventSource = current;

		initEventSource.OnInit +=
			HandleGameFlowInitialized;
	}

	private void HandleGameFlowInitialized()
	{
		FindManagers();

		if (IsServerInitialized)
		{
			RegisterServerEvents();
		}
	}

	private void FindManagers()
	{
		gameFlowManager =
			GameFlowManager.Instance;

		lockdownManager =
			LockdownManager.Instance;

		lightSabotageManager =
			LightSabotageManager.Instance;
	}

	private void RegisterServerEvents()
	{
		if (!gameFlowEventsRegistered &&
			gameFlowManager != null)
		{
			gameFlowManager.OnRoundPhaseChanged +=
				HandleRoundPhaseChanged;

			gameFlowManager.OnRoundEnded +=
				HandleRoundEnded;

			gameFlowEventsRegistered = true;
		}

		if (!lockdownEventsRegistered &&
			lockdownManager != null)
		{
			lockdownManager.OnLockdownStarted +=
				HandleLockdownStarted;

			lockdownManager.OnLockdownEnded +=
				HandleLockdownEnded;

			lockdownEventsRegistered = true;
		}

		if (!lightSabotageEventsRegistered &&
			lightSabotageManager != null)
		{
			lightSabotageManager.OnLightsChanged +=
				HandleLightsChanged;

			lightSabotageEventsRegistered = true;
		}
	}

	private void UnregisterServerEvents()
	{
		if (gameFlowEventsRegistered &&
			gameFlowManager != null)
		{
			gameFlowManager.OnRoundPhaseChanged -=
				HandleRoundPhaseChanged;

			gameFlowManager.OnRoundEnded -=
				HandleRoundEnded;
		}

		if (lockdownEventsRegistered &&
			lockdownManager != null)
		{
			lockdownManager.OnLockdownStarted -=
				HandleLockdownStarted;

			lockdownManager.OnLockdownEnded -=
				HandleLockdownEnded;
		}

		if (lightSabotageEventsRegistered &&
			lightSabotageManager != null)
		{
			lightSabotageManager.OnLightsChanged -=
				HandleLightsChanged;
		}

		gameFlowEventsRegistered = false;
		lockdownEventsRegistered = false;
		lightSabotageEventsRegistered = false;
	}

	#region Game Flow

	private void HandleRoundPhaseChanged(
		RoundFlowPhase phase)
	{
		if (!IsServerInitialized)
			return;

		if (lightSabotageMusicActive ||
			lightSabotagePending)
		{
			UpdateSabotageRestoreTarget(
				phase
			);

			return;
		}

		switch (phase)
		{
			case RoundFlowPhase.Setup:

				/*
				 * A new round starts with fresh playlist memory.
				 * Temporary transitions inside the round still resume.
				 */
				ClearPlaybackMemories();

				StartSetupMusic();
				break;

			case RoundFlowPhase.Active:

				if (lockdownManager != null &&
					lockdownManager.IsLockedDown)
				{
					return;
				}

				StartActiveMusic();
				break;
		}
	}

	private void HandleRoundEnded(
		int round,
		RoundEndReason reason)
	{
		if (!IsServerInitialized)
			return;

		ClearLightSabotageOverride();

		if (ContainsReason(
				copsWinReasons,
				reason))
		{
			PlayCopsWinMusic();
			return;
		}

		if (ContainsReason(
				robbersWinReasons,
				reason))
		{
			PlayRobbersWinMusic();
		}
	}

	private bool ContainsReason(
		RoundEndReason[] reasons,
		RoundEndReason target)
	{
		if (reasons == null)
			return false;

		for (int i = 0; i < reasons.Length; i++)
		{
			if (reasons[i] == target)
				return true;
		}

		return false;
	}

	#endregion

	#region Lockdown

	private void HandleLockdownStarted()
	{
		if (!IsServerInitialized)
			return;

		if (lightSabotageMusicActive ||
			lightSabotagePending)
		{
			preSabotageState =
				SoundtrackState.Lockdown;

			return;
		}

		StartLockdownMusic(
			lockdownStartCrossFadeDuration
		);
	}

	private void HandleLockdownEnded()
	{
		if (!IsServerInitialized)
			return;

		if (lightSabotageMusicActive ||
			lightSabotagePending)
		{
			SetSabotageRestoreTargetToCurrentRound();
			return;
		}

		if (serverState !=
			SoundtrackState.Lockdown)
		{
			return;
		}

		EndLockdownMusic(
			lockdownEndCrossFadeDuration
		);
	}

	#endregion

	#region Light Sabotage

	private void HandleLightsChanged(
		bool lightsOn)
	{
		if (!IsServerInitialized)
			return;

		if (lightsOn)
		{
			HandleLightsRestored();
		}
		else
		{
			HandleLightsTurnedOff();
		}
	}

	[Server]
	private void HandleLightsTurnedOff()
	{
		CancelLightSabotageDelay();

		if (IsResultMusicPlaying())
			return;

		if (lightSabotageMusicActive ||
			lightSabotagePending)
		{
			return;
		}

		preSabotageState =
			serverState;

		/*
		 * Save the exact song and playhead before
		 * the fade-to-silence begins.
		 */
		PauseCurrentPlayback();
		StopPlaylistCoroutine();

		lightSabotagePending = true;

		PlayTrackObserversRpc(
			SoundtrackState.None,
			-1,
			0f,
			0u,
			lightSabotageFadeOutDuration
		);

		lightSabotageDelayCoroutine =
			StartCoroutine(
				LightSabotageDelayRoutine()
			);
	}

	[Server]
	private IEnumerator LightSabotageDelayRoutine()
	{
		if (lightSabotageFadeOutDuration > 0f)
		{
			yield return new WaitForSeconds(
				lightSabotageFadeOutDuration
			);
		}

		if (lightSabotageDelay > 0f)
		{
			yield return new WaitForSeconds(
				lightSabotageDelay
			);
		}

		lightSabotageDelayCoroutine = null;

		if (lightSabotageManager == null ||
			lightSabotageManager.IsOn)
		{
			lightSabotagePending = false;
			yield break;
		}

		if (IsResultMusicPlaying())
		{
			lightSabotagePending = false;
			yield break;
		}

		BeginLightSabotageMusic();
	}

	[Server]
	private void BeginLightSabotageMusic()
	{
		if (lightSabotageMusicActive)
			return;

		lightSabotagePending = false;

		if (lightSabotagePlaylist == null ||
			!lightSabotagePlaylist.HasValidTracks())
		{
			LogWarning(
				"No light sabotage tracks assigned."
			);

			float restoreFade =
				ResolveCrossFade(
					lightRestoreCrossFadeDuration
				);

			SoundtrackState restoreState =
				preSabotageState;

			ClearStoredSabotageState();

			RestoreState(
				restoreState,
				restoreFade
			);

			return;
		}

		if (IsResultMusicPlaying())
			return;

		lightSabotageMusicActive = true;

		StartPlaylist(
			SoundtrackState.LightSabotage,
			lightSabotagePlaylist,
			ResolveCrossFade(
				lightSabotageCrossFadeDuration
			)
		);
	}

	[Server]
	private void HandleLightsRestored()
	{
		CancelLightSabotageDelay();

		bool wasPending =
			lightSabotagePending;

		bool wasActive =
			lightSabotageMusicActive;

		if (!wasPending &&
			!wasActive)
		{
			return;
		}

		if (wasActive &&
			serverState !=
				SoundtrackState.LightSabotage)
		{
			ClearStoredSabotageState();
			return;
		}

		float restoreFade =
			ResolveCrossFade(
				lightRestoreCrossFadeDuration
			);

		SoundtrackState restoreState =
			preSabotageState;

		ClearStoredSabotageState();

		RestoreState(
			restoreState,
			restoreFade
		);
	}

	private void UpdateSabotageRestoreTarget(
		RoundFlowPhase phase)
	{
		switch (phase)
		{
			case RoundFlowPhase.Setup:
				preSabotageState =
					SoundtrackState.Setup;
				break;

			case RoundFlowPhase.Active:

				if (lockdownManager != null &&
					lockdownManager.IsLockedDown)
				{
					preSabotageState =
						SoundtrackState.Lockdown;
				}
				else
				{
					preSabotageState =
						SoundtrackState.Active;
				}

				break;
		}
	}

	private void SetSabotageRestoreTargetToCurrentRound()
	{
		if (gameFlowManager == null)
		{
			preSabotageState =
				SoundtrackState.None;

			return;
		}

		switch (gameFlowManager.RoundPhase)
		{
			case RoundFlowPhase.Setup:
				preSabotageState =
					SoundtrackState.Setup;
				break;

			case RoundFlowPhase.Active:
				preSabotageState =
					SoundtrackState.Active;
				break;

			default:
				preSabotageState =
					SoundtrackState.None;
				break;
		}
	}

	private void CancelLightSabotageDelay()
	{
		if (lightSabotageDelayCoroutine == null)
			return;

		StopCoroutine(
			lightSabotageDelayCoroutine
		);

		lightSabotageDelayCoroutine = null;
	}

	private void ClearLightSabotageOverride()
	{
		CancelLightSabotageDelay();
		ClearStoredSabotageState();
	}

	private void ClearStoredSabotageState()
	{
		lightSabotagePending = false;
		lightSabotageMusicActive = false;

		preSabotageState =
			SoundtrackState.None;
	}

	#endregion

	#region Public Server API

	[Server]
	public void StartSetupMusic(
		float crossFadeDuration = -1f)
	{
		StartPlaylist(
			SoundtrackState.Setup,
			setupPlaylist,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void StartActiveMusic(
		float crossFadeDuration = -1f)
	{
		StartPlaylist(
			SoundtrackState.Active,
			activePlaylist,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void StartLockdownMusic(
		float crossFadeDuration = -1f)
	{
		StartPlaylist(
			SoundtrackState.Lockdown,
			lockdownPlaylist,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void EndLockdownMusic(
		float crossFadeDuration = -1f)
	{
		RestoreCurrentRoundMusic(
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void PlayCopsWinMusic(
		float crossFadeDuration = -1f)
	{
		ClearLightSabotageOverride();

		PlaySingleTrack(
			SoundtrackState.CopsWin,
			copsWinTrack,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void PlayRobbersWinMusic(
		float crossFadeDuration = -1f)
	{
		ClearLightSabotageOverride();

		PlaySingleTrack(
			SoundtrackState.RobbersWin,
			robbersWinTrack,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	[Server]
	public void StopMusic(
		float crossFadeDuration = -1f)
	{
		PauseCurrentPlayback();
		StopPlaylistCoroutine();

		serverState =
			SoundtrackState.None;

		serverTrackIndex = -1;
		serverTrackClockRunning = false;

		PlayTrackObserversRpc(
			SoundtrackState.None,
			-1,
			0f,
			0u,
			ResolveCrossFade(
				crossFadeDuration
			)
		);
	}

	public void ToggleMuteLocal()
	{
		if (audioPlayer != null)
		{
			audioPlayer.ToggleMuteLocal();
		}
	}

	public void SetMutedLocal(bool muted)
	{
		if (audioPlayer != null)
		{
			audioPlayer.SetMutedLocal(
				muted
			);
		}
	}

	public void SetMusicVolumeLocal(float volume)
	{
		if (audioPlayer != null)
		{
			audioPlayer.SetMusicVolume(
				volume
			);
		}
	}

	#endregion

	#region Restore Music

	[Server]
	private void RestoreState(
		SoundtrackState state,
		float crossFadeDuration)
	{
		switch (state)
		{
			case SoundtrackState.Setup:
				StartSetupMusic(
					crossFadeDuration
				);
				break;

			case SoundtrackState.Active:
				StartActiveMusic(
					crossFadeDuration
				);
				break;

			case SoundtrackState.Lockdown:
				StartLockdownMusic(
					crossFadeDuration
				);
				break;

			default:
				RestoreCurrentGameMusic(
					crossFadeDuration
				);
				break;
		}
	}

	[Server]
	private void RestoreCurrentGameMusic(
		float crossFadeDuration)
	{
		if (lockdownManager != null &&
			lockdownManager.IsLockedDown)
		{
			StartLockdownMusic(
				crossFadeDuration
			);

			return;
		}

		RestoreCurrentRoundMusic(
			crossFadeDuration
		);
	}

	[Server]
	private void RestoreCurrentRoundMusic(
		float crossFadeDuration)
	{
		if (gameFlowManager == null)
		{
			StopMusic(
				crossFadeDuration
			);

			return;
		}

		switch (gameFlowManager.RoundPhase)
		{
			case RoundFlowPhase.Setup:
				StartSetupMusic(
					crossFadeDuration
				);
				break;

			case RoundFlowPhase.Active:
				StartActiveMusic(
					crossFadeDuration
				);
				break;

			default:
				StopMusic(
					crossFadeDuration
				);
				break;
		}
	}

	#endregion

	#region Server Playlist

	[Server]
	private void StartPlaylist(
		SoundtrackState state,
		SoundtrackPlaylist playlist,
		float transitionCrossFade,
		float playlistCrossFade = -1f)
	{
		if (playlist == null ||
			!playlist.HasValidTracks())
		{
			Log(
				$"No tracks assigned for {state}."
			);

			return;
		}

		/*
		 * Ignore duplicate start events while the same
		 * playlist is already running.
		 *
		 * If its coroutine is stopped, this method resumes
		 * from the remembered position instead.
		 */
		if (serverState == state &&
			playlistCoroutine != null)
		{
			return;
		}

		PauseCurrentPlayback();
		StopPlaylistCoroutine();

		serverState = state;

		float resolvedPlaylistCrossFade =
			playlistCrossFade >= 0f
				? playlistCrossFade
				: transitionCrossFade;

		playlistCoroutine =
			StartCoroutine(
				RunPlaylist(
					state,
					playlist,
					transitionCrossFade,
					resolvedPlaylistCrossFade
				)
			);
	}

	[Server]
	private IEnumerator RunPlaylist(
		SoundtrackState state,
		SoundtrackPlaylist playlist,
		float firstCrossFade,
		float playlistCrossFade)
	{
		int previousIndex = -1;
		bool firstTrack = true;

		while (serverState == state)
		{
			int trackIndex = -1;
			float playbackTime = 0f;

			if (firstTrack)
			{
				TryGetPlaybackMemory(
					state,
					playlist,
					out trackIndex,
					out playbackTime
				);
			}

			if (trackIndex < 0)
			{
				trackIndex =
					GetRandomTrackIndex(
						playlist,
						previousIndex
					);

				playbackTime = 0f;
			}

			if (trackIndex < 0)
			{
				yield return null;
				continue;
			}

			SoundtrackTrack track =
				playlist.GetTrack(
					trackIndex
				);

			if (track == null ||
				!track.IsValid)
			{
				previousIndex =
					trackIndex;

				firstTrack = false;

				yield return null;
				continue;
			}

			float fade =
				firstTrack
					? firstCrossFade
					: playlistCrossFade;

			BeginServerTrack(
				state,
				trackIndex,
				playbackTime,
				fade
			);

			previousIndex =
				trackIndex;

			firstTrack = false;

			float waitDuration =
				Mathf.Max(
					0.1f,
					track.Clip.length -
					playbackTime -
					playlistCrossFade
				);

			yield return new WaitForSeconds(
				waitDuration
			);
		}

		playlistCoroutine = null;
	}

	[Server]
	private void BeginServerTrack(
		SoundtrackState state,
		int trackIndex,
		float playbackTime,
		float crossFadeDuration)
	{
		serverState = state;
		serverTrackIndex = trackIndex;

		serverTrackStartPosition =
			Mathf.Max(
				0f,
				playbackTime
			);

		serverTrackStartTick =
			TimeManager.Tick;

		serverTrackClockRunning = true;

		PlayTrackObserversRpc(
			state,
			trackIndex,
			serverTrackStartPosition,
			serverTrackStartTick,
			crossFadeDuration
		);
	}

	[Server]
	private void PlaySingleTrack(
		SoundtrackState state,
		SoundtrackTrack track,
		float crossFadeDuration)
	{
		if (track == null ||
			!track.IsValid)
		{
			LogWarning(
				$"No track assigned for {state}."
			);

			return;
		}

		PauseCurrentPlayback();
		StopPlaylistCoroutine();

		serverState = state;
		serverTrackIndex = 0;

		serverTrackStartPosition = 0f;
		serverTrackStartTick = TimeManager.Tick;
		serverTrackClockRunning = true;

		PlayTrackObserversRpc(
			state,
			0,
			0f,
			serverTrackStartTick,
			crossFadeDuration
		);
	}

	private int GetRandomTrackIndex(
		SoundtrackPlaylist playlist,
		int previousIndex)
	{
		if (playlist == null ||
			playlist.Count == 0)
		{
			return -1;
		}

		int validCount = 0;

		for (int i = 0; i < playlist.Count; i++)
		{
			SoundtrackTrack track =
				playlist.GetTrack(i);

			if (track != null &&
				track.IsValid)
			{
				validCount++;
			}
		}

		if (validCount == 0)
			return -1;

		if (validCount == 1)
		{
			for (int i = 0; i < playlist.Count; i++)
			{
				SoundtrackTrack track =
					playlist.GetTrack(i);

				if (track != null &&
					track.IsValid)
				{
					return i;
				}
			}
		}

		int startIndex =
			Random.Range(
				0,
				playlist.Count
			);

		for (int offset = 0;
			 offset < playlist.Count;
			 offset++)
		{
			int index =
				(startIndex + offset) %
				playlist.Count;

			if (index == previousIndex)
				continue;

			SoundtrackTrack track =
				playlist.GetTrack(index);

			if (track != null &&
				track.IsValid)
			{
				return index;
			}
		}

		return previousIndex;
	}

	private void StopPlaylistCoroutine()
	{
		if (playlistCoroutine == null)
			return;

		StopCoroutine(
			playlistCoroutine
		);

		playlistCoroutine = null;
	}

	#endregion

	#region Playback Memory

	[Server]
	private void PauseCurrentPlayback()
	{
		if (!serverTrackClockRunning)
			return;

		if (!IsPlaylistState(
				serverState))
		{
			serverTrackClockRunning = false;
			return;
		}

		SoundtrackTrack track =
			GetTrack(
				serverState,
				serverTrackIndex
			);

		if (track == null ||
			!track.IsValid)
		{
			serverTrackClockRunning = false;
			return;
		}

		float playbackTime =
			GetCurrentServerPlaybackTime(
				track
			);

		PlaybackMemory memory =
			GetOrCreatePlaybackMemory(
				serverState
			);

		memory.TrackIndex =
			serverTrackIndex;

		memory.PlaybackTime =
			playbackTime;

		serverTrackClockRunning = false;
	}

	private float GetCurrentServerPlaybackTime(
		SoundtrackTrack track)
	{
		if (!serverTrackClockRunning ||
			track == null ||
			!track.IsValid)
		{
			return serverTrackStartPosition;
		}

		float elapsed =
			(float)TimeManager.TimePassed(
				serverTrackStartTick
			);

		return Mathf.Clamp(
			serverTrackStartPosition +
			elapsed,
			0f,
			track.Clip.length
		);
	}

	private PlaybackMemory GetOrCreatePlaybackMemory(
		SoundtrackState state)
	{
		if (!playbackMemories.TryGetValue(
				state,
				out PlaybackMemory memory))
		{
			memory =
				new PlaybackMemory();

			playbackMemories.Add(
				state,
				memory
			);
		}

		return memory;
	}

	private bool TryGetPlaybackMemory(
		SoundtrackState state,
		SoundtrackPlaylist playlist,
		out int trackIndex,
		out float playbackTime)
	{
		trackIndex = -1;
		playbackTime = 0f;

		if (!playbackMemories.TryGetValue(
				state,
				out PlaybackMemory memory))
		{
			return false;
		}

		SoundtrackTrack track =
			playlist.GetTrack(
				memory.TrackIndex
			);

		if (track == null ||
			!track.IsValid)
		{
			playbackMemories.Remove(
				state
			);

			return false;
		}

		if (memory.PlaybackTime >=
			track.Clip.length - 0.05f)
		{
			playbackMemories.Remove(
				state
			);

			return false;
		}

		trackIndex =
			memory.TrackIndex;

		playbackTime =
			Mathf.Clamp(
				memory.PlaybackTime,
				0f,
				track.Clip.length
			);

		return true;
	}

	private void ClearPlaybackMemories()
	{
		playbackMemories.Clear();
	}

	private bool IsPlaylistState(
		SoundtrackState state)
	{
		return
			state == SoundtrackState.Setup ||
			state == SoundtrackState.Active ||
			state == SoundtrackState.Lockdown ||
			state == SoundtrackState.LightSabotage;
	}

	#endregion

	#region Track Lookup

	private SoundtrackPlaylist GetPlaylist(
		SoundtrackState state)
	{
		switch (state)
		{
			case SoundtrackState.Setup:
				return setupPlaylist;

			case SoundtrackState.Active:
				return activePlaylist;

			case SoundtrackState.Lockdown:
				return lockdownPlaylist;

			case SoundtrackState.LightSabotage:
				return lightSabotagePlaylist;

			default:
				return null;
		}
	}

	private SoundtrackTrack GetTrack(
		SoundtrackState state,
		int index)
	{
		switch (state)
		{
			case SoundtrackState.CopsWin:
				return index == 0
					? copsWinTrack
					: null;

			case SoundtrackState.RobbersWin:
				return index == 0
					? robbersWinTrack
					: null;

			default:
				return GetPlaylist(state)?
					.GetTrack(index);
		}
	}

	private bool IsResultMusicPlaying()
	{
		return
			serverState ==
				SoundtrackState.CopsWin ||
			serverState ==
				SoundtrackState.RobbersWin;
	}

	#endregion

	#region Crossfade Settings

	private float ResolveCrossFade(
		float requestedDuration)
	{
		if (requestedDuration >= 0f)
			return requestedDuration;

		return defaultCrossFadeDuration;
	}

	#endregion

	#region Network Playback

	[ObserversRpc(
		BufferLast = true,
		RunLocally = true)]
	private void PlayTrackObserversRpc(
		SoundtrackState state,
		int trackIndex,
		float playbackTime,
		uint startedTick,
		float crossFadeDuration)
	{
		if (audioPlayer == null)
			return;

		if (state ==
			SoundtrackState.None)
		{
			audioPlayer.FadeOut(
				crossFadeDuration
			);

			return;
		}

		SoundtrackTrack track =
			GetTrack(
				state,
				trackIndex
			);

		if (track == null ||
			!track.IsValid)
		{
			LogWarning(
				"Could not resolve soundtrack track. " +
				$"State: {state}, " +
				$"Index: {trackIndex}"
			);

			return;
		}

		/*
		 * Compensate for RPC/network delay and buffered
		 * RPC delivery to late joiners.
		 */
		float networkElapsed =
			(float)TimeManager.TimePassed(
				startedTick
			);

		float resolvedPlaybackTime =
			playbackTime +
			networkElapsed;

		audioPlayer.CrossFadeTo(
			track,
			resolvedPlaybackTime,
			crossFadeDuration
		);
	}

	#endregion

	#region Debug

	private void Log(string message)
	{
		if (!enableLogs)
			return;

		Debug.Log(
			$"[SoundtrackController] {message}",
			this
		);
	}

	private void LogWarning(string message)
	{
		if (!enableLogs)
			return;

		Debug.LogWarning(
			$"[SoundtrackController] {message}",
			this
		);
	}

	#endregion
}