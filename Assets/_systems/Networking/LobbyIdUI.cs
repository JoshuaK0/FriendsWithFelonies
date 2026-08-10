using Game.Steam;
using TMPro;
using UnityEngine;

public class LobbyIdUI : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyIdText;

    private string lobbyId;

    private void Update()
    {
        if (!SteamLobby.CurrentLobby.Id.IsValid)
        {
            lobbyId = "";
            lobbyIdText.text = "No Lobby";
            return;
        }

        lobbyId = SteamLobby.CurrentLobby.Id.ToString();
        lobbyIdText.text = lobbyId;
    }

    public void CopyLobbyId()
    {
        if (string.IsNullOrEmpty(lobbyId))
            return;

        GUIUtility.systemCopyBuffer = lobbyId;

        Debug.Log($"Copied lobby ID: {lobbyId}");
    }
}
