using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class NetworkSceneManager : NetworkBehaviour
{
	public static NetworkSceneManager Instance { get; private set; }

	private TaskCompletionSource<bool> loadTask;
	private TaskCompletionSource<bool> unloadTask;

	private readonly HashSet<int> waitingForLoad = new();
	private readonly HashSet<int> waitingForUnload = new();

	private int currentLoadId;
	private int currentUnloadId;

	private int clientLoadId = -1;
	private int clientUnloadId = -1;

	private bool serverFinishedLoading;
	private bool serverFinishedUnloading;

	// Current server load settings.
	private string currentLoadSceneName;
	private bool currentLoadSetActive;

	// Current client load settings.
	private string clientLoadSceneName;
	private bool clientLoadSetActive;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;
		InstanceFinder.SceneManager.OnUnloadEnd += OnUnloadEnd;
	}

	public override void OnStopNetwork()
	{
		if (InstanceFinder.SceneManager != null)
		{
			InstanceFinder.SceneManager.OnLoadEnd -= OnLoadEnd;
			InstanceFinder.SceneManager.OnUnloadEnd -= OnUnloadEnd;
		}

		if (Instance == this)
			Instance = null;

		base.OnStopNetwork();
	}

	// ----------------------------------------------------------------------
	// Loading
	// ----------------------------------------------------------------------

	[Server]
	public void LoadScene(
		string sceneName,
		bool additive = false,
		bool setActiveScene = false)
	{
		SceneLoadData loadData = new SceneLoadData(sceneName);

		loadData.ReplaceScenes = additive
			? ReplaceOption.None
			: ReplaceOption.All;

		currentLoadSceneName = sceneName;
		currentLoadSetActive = additive && setActiveScene;

		BeginLoadObserversRpc(
			-1,
			sceneName,
			currentLoadSetActive);

		InstanceFinder.SceneManager.LoadGlobalScenes(loadData);
	}

	[Server]
	public async Task LoadSceneAsync(
		string sceneName,
		bool additive = false,
		bool setActiveScene = false)
	{
		if (loadTask != null)
		{
			Debug.LogWarning(
				$"Cannot load {sceneName}. A scene is already loading.");

			return;
		}

		currentLoadId++;
		serverFinishedLoading = false;

		currentLoadSceneName = sceneName;
		currentLoadSetActive = additive && setActiveScene;

		waitingForLoad.Clear();

		foreach (NetworkConnection connection
				 in InstanceFinder.ServerManager.Clients.Values)
		{
			waitingForLoad.Add(connection.ClientId);
		}

		loadTask = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		Task currentTask = loadTask.Task;

		BeginLoadObserversRpc(
			currentLoadId,
			sceneName,
			currentLoadSetActive);

		SceneLoadData loadData = new SceneLoadData(sceneName);

		loadData.ReplaceScenes = additive
			? ReplaceOption.None
			: ReplaceOption.All;

		InstanceFinder.SceneManager.LoadGlobalScenes(loadData);

		await currentTask;
	}

	[ObserversRpc]
	private void BeginLoadObserversRpc(
		int loadId,
		string sceneName,
		bool setActiveScene)
	{
		clientLoadId = loadId;
		clientLoadSceneName = sceneName;
		clientLoadSetActive = setActiveScene;
	}

	private void OnLoadEnd(SceneLoadEndEventArgs args)
	{
		if (args.QueueData.AsServer)
		{
			if (currentLoadSetActive)
				SetActiveScene(currentLoadSceneName);

			serverFinishedLoading = true;
			CheckLoadComplete();
		}
		else if (IsClientStarted)
		{
			if (clientLoadSetActive)
				SetActiveScene(clientLoadSceneName);

			if (clientLoadId != -1)
			{
				int completedLoadId = clientLoadId;
				clientLoadId = -1;

				ReportLoadCompleteServerRpc(completedLoadId);
			}

			clientLoadSceneName = null;
			clientLoadSetActive = false;
		}
	}

	private void SetActiveScene(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName))
			return;

		UnityEngine.SceneManagement.Scene scene =
			UnitySceneManager.GetSceneByName(sceneName);

		if (!scene.IsValid() || !scene.isLoaded)
		{
			Debug.LogWarning(
				$"Cannot set '{sceneName}' as the active scene because it is not loaded.");

			return;
		}

		UnitySceneManager.SetActiveScene(scene);
	}

	[ServerRpc(RequireOwnership = false)]
	private void ReportLoadCompleteServerRpc(
		int loadId,
		NetworkConnection sender = null)
	{
		if (sender == null)
			return;

		if (loadId != currentLoadId)
			return;

		waitingForLoad.Remove(sender.ClientId);

		CheckLoadComplete();
	}

	[Server]
	private void CheckLoadComplete()
	{
		RemoveDisconnectedClients(waitingForLoad);

		if (!serverFinishedLoading)
			return;

		if (waitingForLoad.Count > 0)
			return;

		TaskCompletionSource<bool> completedTask = loadTask;

		loadTask = null;

		currentLoadSceneName = null;
		currentLoadSetActive = false;

		completedTask?.TrySetResult(true);
	}

	// ----------------------------------------------------------------------
	// Unloading
	// ----------------------------------------------------------------------

	[Server]
	public void UnloadScene(string sceneName)
	{
		SceneUnloadData unloadData = new SceneUnloadData(sceneName);

		InstanceFinder.SceneManager.UnloadGlobalScenes(unloadData);
	}

	[Server]
	public async Task UnloadSceneAsync(string sceneName)
	{
		if (unloadTask != null)
		{
			Debug.LogWarning(
				$"Cannot unload {sceneName}. A scene is already unloading.");

			return;
		}

		currentUnloadId++;
		serverFinishedUnloading = false;

		waitingForUnload.Clear();

		foreach (NetworkConnection connection
				 in InstanceFinder.ServerManager.Clients.Values)
		{
			waitingForUnload.Add(connection.ClientId);
		}

		unloadTask = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		Task currentTask = unloadTask.Task;

		BeginUnloadObserversRpc(currentUnloadId);
		UnloadScene(sceneName);

		await currentTask;
	}

	[ObserversRpc]
	private void BeginUnloadObserversRpc(int unloadId)
	{
		clientUnloadId = unloadId;
	}

	private void OnUnloadEnd(SceneUnloadEndEventArgs args)
	{
		if (args.QueueData.AsServer)
		{
			serverFinishedUnloading = true;
			CheckUnloadComplete();
		}
		else if (IsClientStarted && clientUnloadId != -1)
		{
			int completedUnloadId = clientUnloadId;
			clientUnloadId = -1;

			ReportUnloadCompleteServerRpc(completedUnloadId);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	private void ReportUnloadCompleteServerRpc(
		int unloadId,
		NetworkConnection sender = null)
	{
		if (sender == null)
			return;

		if (unloadId != currentUnloadId)
			return;

		waitingForUnload.Remove(sender.ClientId);

		CheckUnloadComplete();
	}

	[Server]
	private void CheckUnloadComplete()
	{
		RemoveDisconnectedClients(waitingForUnload);

		if (!serverFinishedUnloading)
			return;

		if (waitingForUnload.Count > 0)
			return;

		TaskCompletionSource<bool> completedTask = unloadTask;

		unloadTask = null;

		completedTask?.TrySetResult(true);
	}

	// ----------------------------------------------------------------------

	[Server]
	private void RemoveDisconnectedClients(HashSet<int> waitingClients)
	{
		waitingClients.RemoveWhere(clientId =>
			!InstanceFinder.ServerManager.Clients.ContainsKey(clientId));
	}
}