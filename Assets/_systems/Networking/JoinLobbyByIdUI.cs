using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine;

public class JoinLobbyByIdUI : MonoBehaviour
{
	[SerializeField] private TMP_InputField lobbyIdInput;

	public async void JoinLobby()
	{
		if (!ulong.TryParse(lobbyIdInput.text, out ulong id))
		{
			Debug.LogError("Invalid lobby ID.");
			return;
		}

		SteamId lobbyId = id;

		Debug.Log($"Joining lobby: {lobbyId}");

		Lobby? lobby =
			await SteamMatchmaking.JoinLobbyAsync(lobbyId);

		if (!lobby.HasValue)
		{
			Debug.LogError($"Failed to join lobby: {lobbyId}");
			return;
		}

		Debug.Log($"Joined lobby: {lobby.Value.Id}");
	}
}