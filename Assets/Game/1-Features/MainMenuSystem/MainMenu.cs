using System;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using Game.GameSystem;
using Game.Steam;
using Game.Utils;
using Steamworks;
using UnityEngine;
using FishNet.Object;

namespace Game
{
    public class MainMenu : BaseMonoBehaviour
    {

        [SerializeField] private GameObject _homeMenu;
        [SerializeField] private GameObject _partyMenu;

        [SerializeField] GameObject networkSceneManagerPrefab;
        [SerializeField] GameObject playerTeamsPrefab;
        
        private bool steamworksInitialized = false;

        protected override void RegisterEvents()
        {
            if (!SteamClient.IsValid)
            {  try
                {
                    SteamClient.Init(480);
                    steamworksInitialized = true;
                }
                catch (Exception e)
                {
                    // Something went wrong - it's one of these:
                    //
                    //     Steam is closed?
                    //     Can't find steam_api dll?
                    //     Don't have permission to play app?
                    //

                    steamworksInitialized = false;

                    Debug.LogException(e);
                    Application.Quit();
                }
                steamworksInitialized = false;
            }

          

            MyClient.OnStartClient += UpdateMenu;
            MyClient.OnStartClient += CreateNetworkSceneManager;
        }

        protected override void UnregisterEvents()
        {
            MyClient.OnStartClient -= UpdateMenu;
        }

        void CreateNetworkSceneManager(MyClient myClient)
        {
            if(NetworkSceneManager.Instance == null)
            {
				InstanceFinder.ServerManager.Spawn(Instantiate(networkSceneManagerPrefab));
			}
			if (PlayerTeams.Instance == null)
			{
				InstanceFinder.ServerManager.Spawn(Instantiate(playerTeamsPrefab));
			}
		}

		public void ReadyUp()
        {
            NetworkExtensions.GetLocalPlayer().Cmd_ReadyUp();
        }

        public void CreateParty()
        {
            InstanceFinder.TransportManager.GetTransport<Multipass>().SetClientTransport<FishyFacepunch.FishyFacepunch>();
            SteamLobby.Singleton.CreateLobbyAsync();
		}
        
        public void JoinParty()
        {
            InstanceFinder.TransportManager.GetTransport<Multipass>().SetClientTransport<FishyFacepunch.FishyFacepunch>();
            SteamLobby.Singleton.FindLobby();
        }
        
        public void CreatePartyLocal()
        {
            InstanceFinder.TransportManager.GetTransport<Multipass>().SetClientTransport<Tugboat>();
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();
        }
        
        public void JoinPartyLocal()
        {
            InstanceFinder.TransportManager.GetTransport<Multipass>().SetClientTransport<Tugboat>();
            InstanceFinder.ClientManager.StartConnection();
        }

        public void StartGame()
        {
            GameManager.StartGame();
        }

        private void UpdateMenu(MyClient client)
        {
            _homeMenu.SetActive(!client);
            _partyMenu.SetActive(client);
        }
    }
}