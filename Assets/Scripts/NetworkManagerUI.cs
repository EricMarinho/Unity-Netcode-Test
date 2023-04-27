using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Relay")]
    [SerializeField] private Button startRelayButton;
    [SerializeField] private Button joinRelayButton;
    [SerializeField] private TMP_InputField joinCodeInputField;


    [Header("Lobby")]
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private TestRelay testRelay;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button listPlayersButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button migrateLobbyHostButton;
    [SerializeField] private Button printLobbyHostButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button reconnectGameButton;

    private void Awake()
    {
        startRelayButton.onClick.AddListener(() =>
        {
            testRelay.CreateRelay();
        });

        joinRelayButton.onClick.AddListener(() =>
        {
            testRelay.JoinRelay(joinCodeInputField.text);
        });



        createLobbyButton.onClick.AddListener(() =>
        {
            lobbyManager.CreateLobby();
        });

        listPlayersButton.onClick.AddListener(() =>
        {
            lobbyManager.PrintLobbyInfo();
        });

        quickJoinButton.onClick.AddListener(() =>
        {
            lobbyManager.QuickJoinLobby();
        }); 

        leaveLobbyButton.onClick.AddListener(() =>
        {
            lobbyManager.LeaveCurrentLobby();
        });

        migrateLobbyHostButton.onClick.AddListener(() =>
        {
            lobbyManager.RefreshLobbyList();
        });

        startGameButton.onClick.AddListener(() =>
        {
            lobbyManager.StartGame();
        });

        reconnectGameButton.onClick.AddListener(() =>
        {
            lobbyManager.Reconnect();
        });

    }
}
