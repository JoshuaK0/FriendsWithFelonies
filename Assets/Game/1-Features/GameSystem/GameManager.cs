using System;
using FishNet;
using FishNet.Managing.Scened;
using Game;
using Game.PopupSystem;
using Game.Utils;
using UnityEngine;

namespace Game.GameSystem
{
    public class GameManager : BaseNetworkBehaviour
    {
        public static GameManager Instance;

        private void Awake()
        {
            Instance = this;
        }

        protected override void RegisterEvents()
        {
            
        }

        protected override void UnregisterEvents()
        {
            
        }

		public static async void StartGame()
		{
			if (!IsAllPlayersReady())
			{
				PopupManager.Popup_Show(
					new PopupContent(
						"CAN NOT START THE GAME",
						"ALL PLAYERS MUST BE READY TO START.",
						true));

				return;
			}

			if (NetworkSceneManager.Instance == null)
			{
				Debug.LogError("NetworkSceneManager instance was not found.");
				return;
			}

			await NetworkSceneManager.Instance.LoadSceneAsync(
				"GameLoader",
				true,
				false);

			await NetworkSceneManager.Instance.LoadSceneAsync(
				"Map2",
				true,
				true);

			await NetworkSceneManager.Instance.UnloadSceneAsync(
				"MainMenu");

			GameFlowManager.Instance.StartGame();

			Debug.Log("Loading map complete");
		}

		public static bool IsAllPlayersReady()
        {
            foreach (MyClient client in PlayerConnectionManager.Instance.AllClients)
            {
                if (!client.IsReady.Value)
                    return false;
            }

            return true;
        }
    }
}