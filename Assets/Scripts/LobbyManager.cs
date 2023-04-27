using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using ParrelSync;
using System.Linq;
using Unity.Netcode;
using System.Collections;

public class LobbyManager : MonoBehaviour
{

    private Lobby hostLobby;
    private float heartbeatTimer = 0f;
    [SerializeField] private float heatbeatRate = 15f;
    private float lobbyUpdateTimer = 0f;
    [SerializeField] private float lobbyUpdateRate = 1.5f;
    private Lobby joinedLobby;
    private int hostIndex = 0;
    [SerializeField] TestRelay testRelay;
    public bool isOnLobby = false;
    private Action UpdateLobby;
    public string relayCode { get; private set; }
    private string currentJoinCode = "0";
    private string previousJoinCode = "0";
    public string hostOrder = "0";
    private string currentLobbyId = "none";
    public int actualHost = 0;
    private int newHost = 0;

    //singleton

    public static LobbyManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        UpdateLobby += HandleLobbyUpdate;
    }

    private async void Start()
    {
        var options = new InitializationOptions();

    #if UNITY_EDITOR
        options.SetProfile(ClonesManager.IsClone() ? ClonesManager.GetArgument() : "Primary");
    #endif
        await UnityServices.InitializeAsync(options);

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        UpdateLobby();
    }

    private async void HandleLobbyHeartbeat()
    {
        if (hostLobby != null)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer > heatbeatRate)
            {
                heartbeatTimer = 0f;
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
                Debug.Log("Heartbeat Sent");

            }
        }
    }

    private async void HandleLobbyUpdate()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer += Time.deltaTime;
            if (lobbyUpdateTimer > lobbyUpdateRate)
            {
                lobbyUpdateTimer = 0f;
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
            }
            //if (joinedLobby.Data["JoinCode"].Value != "0" && isOnLobby)
            //{
            //    previousJoinCode = currentJoinCode;
            //    currentJoinCode = joinedLobby.Data["JoinCode"].Value;
            //    testRelay.JoinRelay(currentJoinCode);
            //    isOnLobby = false;
                //UpdateLobby -= HandleLobbyUpdate;
                //UpdateLobby += HandleLobbyUpdateMatchStarted;
            //    StartCoroutine(CheckAllocation(currentJoinCode));
            //}
        }
    }

    private async void HandleLobbyUpdateMatchStarted()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer += Time.deltaTime;
            if (lobbyUpdateTimer > lobbyUpdateRate)
            {
                lobbyUpdateTimer = 0f;
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
            }
            if (isOnLobby && (joinedLobby.Players[actualHost].Data["JoinLobbyCode"].Value != currentJoinCode) &&
               (joinedLobby.Players[actualHost].Data["JoinLobbyCode"].Value != "0") &&
               (joinedLobby.Players[actualHost].Data["JoinLobbyCode"].Value != previousJoinCode))
            {
                if (hostOrder != actualHost.ToString())
                {
                    previousJoinCode = currentJoinCode;
                    currentJoinCode = joinedLobby.Players[actualHost].Data["JoinLobbyCode"].Value;
                    testRelay.JoinRelay(currentJoinCode);
                    isOnLobby = false;
                    StartCoroutine(CheckAllocation(currentJoinCode));
                }
            }
        }
    }

    IEnumerator CheckAllocation(string joinCode)
    {
        yield return new WaitForSeconds(5f);
        if (!(NetworkManager.Singleton.IsConnectedClient))
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Trying to Connect Again.");
            testRelay.JoinRelay(joinCode);
            StartCoroutine(CheckAllocation(currentJoinCode));
        }
        else
        {
            UpdateHostOrder();
        }
    }

    public Lobby GetJoinedLobby()
    {
        return joinedLobby;
    }

    public void GetIsHostLobby()
    {
        Debug.Log(IsLobbyHost());
    }

    public bool IsLobbyHost()
    {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private bool IsPlayerInLobby()
    {
        if (joinedLobby != null && joinedLobby.Players != null)
        {
            foreach (Player player in joinedLobby.Players)
            {
                if (player.Id == AuthenticationService.Instance.PlayerId)
                {
                    // This player is in this lobby
                    return true;
                }
            }
        }
        return false;
    }

    public async void CreateLobby(bool isPrivate = false)
    {
        string lobbyName = "Lobby " + UnityEngine.Random.Range(0, 1000);
        int maxPlayers = 4;

        if (PlayerPrefs.GetString("CurrentLobby", "none") == "none")
        {
            currentLobbyId = (Time.time.ToString() + UnityEngine.Random.Range(0, 1000).ToString());
            PlayerPrefs.SetString("CurrentLobby", currentLobbyId);
        }

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = isPrivate,
            Player = GetNewPlayer(),
            Data = new Dictionary<string, DataObject>
            {
                {"JoinCode", new DataObject(DataObject.VisibilityOptions.Member, "0" )},
                {"LobbyID", new DataObject(DataObject.VisibilityOptions.Public, PlayerPrefs.GetString("CurrentLobby"),DataObject.IndexOptions.S5)} 
            }
        };
        try
        {
            hostLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            joinedLobby = hostLobby;
            Debug.Log("Created a Room!");
            isOnLobby= true;
            StartGame();
        }
        catch(Exception e)
        {
            Debug.Log(e);
        }    
    }

    public async void RefreshLobbyList()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter> {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder> {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbyListQueryResponse = await Lobbies.Instance.QueryLobbiesAsync(options);
            foreach(Lobby lobby in lobbyListQueryResponse.Results)
            {
                Debug.Log(lobby.Name);
            }

        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            Debug.Log("Joined a Room!");
            isOnLobby= true;
            PlayerPrefs.SetString("CurrentLobby", joinedLobby.Data["LobbyID"].Value);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void JoinLobby(Lobby lobby)
    {
        try { 

            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
            Debug.Log("Joined a Room!");
            isOnLobby = true;
            PlayerPrefs.SetString("CurrentLobby", joinedLobby.Data["LobbyID"].Value);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
    public async void QuickJoinLobby()
    {
        try
        {
            QuickJoinLobbyOptions options;
            if (PlayerPrefs.GetString("CurrentLobby","none") == "none")
            {
                Debug.Log("1");
                options = new QuickJoinLobbyOptions
                {
                    Player = GetNewPlayer()
                };
            }
            else
            {
                Debug.Log("2");
                options = new QuickJoinLobbyOptions
                {
                    Player = GetNewPlayer(),
                    Filter = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.S5, PlayerPrefs.GetString("CurrentLobby"), QueryFilter.OpOptions.EQ)
                    }
                };
            }

            joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            isOnLobby = true;
            Debug.Log("QuickJoined!");
            PlayerPrefs.SetString("CurrentLobby", joinedLobby.Data["LobbyID"].Value);
            currentJoinCode = joinedLobby.Data["JoinCode"].Value;
            testRelay.JoinRelay(currentJoinCode);
            isOnLobby = false;
            //UpdateLobby -= HandleLobbyUpdate;
            //UpdateLobby += HandleLobbyUpdateMatchStarted;
            StartCoroutine(CheckAllocation(currentJoinCode));

        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        
    }

    public async void KickPlayer(ulong playerId)
    {
        if (IsLobbyHost())
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, joinedLobby.Players[((int)playerId)].Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    public void PrintLobbyInfo()
    {
        foreach (Player player in joinedLobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }   
    }

    private Player GetNewPlayer() {

        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
                {
                    {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, $"Player{UnityEngine.Random.Range(0,1000)}")},
                    {"HostOrder", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0")},
                    {"JoinLobbyCode", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0")}
                }
        };
    }

    private async void UpdateHostOrder()
    {
        try
        {
            for(int i=0; i<joinedLobby.Players.Count;i++)
            {
                if (joinedLobby.Players[i].Id == AuthenticationService.Instance.PlayerId)
                {
                    hostOrder = i.ToString();
                    break;
                }
            }
            
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {"HostOrder", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, hostOrder)},
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId ,options);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void UpdateJoinCode(string joinCode)
    {
        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {"JoinLobbyCode", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, joinCode)},
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void LeaveCurrentLobby()
    {
        if (joinedLobby == null)
            return;
        try
        {
            testRelay.Disconnect();
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            joinedLobby = null;
            isOnLobby = false;
            hostLobby = null;
            PlayerPrefs.SetString("CurrentLobby", "none");
        }
        catch (LobbyServiceException e) 
        { 
            Debug.Log(e);
        }
    }

    public async void LeaveOldLobby()
    {
        if (joinedLobby == null)
            return;
        try
        {
            testRelay.Disconnect();
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            joinedLobby = null;
            isOnLobby = false;
            hostLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void MigrateLobbyHost()
    {
        if(joinedLobby.Players.Count <= 1)
        {
            Debug.Log("It needs to have at least two players to migrate the host");
            return;
        }

        for(int i =0; i < joinedLobby.Players.Count; i++)
        {
            if (i == hostIndex)
                continue;
            hostIndex = i;
            break;
        }

        try
        {
            joinedLobby = await Lobbies.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                HostId= joinedLobby.Players[hostIndex].Id
            });
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public void CheckHost()
    {
        if (joinedLobby == null)
            return;

        if (!(joinedLobby.HostId == joinedLobby.Players[actualHost].Id))
        {
            foreach (var (player, i) in joinedLobby.Players.Select((value, i) => (value, i)))
            {
                if (player.Id == joinedLobby.HostId)
                {
                    actualHost = i;
                }
            }
        }
    }

    public async void StartGame()
    {
        if (IsLobbyHost())
        {
            try
            {
                relayCode = await testRelay.CreateRelay();
                UpdateLobbyOptions options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {"JoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayCode )}
                    }
                };

                await Lobbies.Instance.UpdateLobbyAsync(joinedLobby.Id, options);

                Debug.Log("New Relay code: " + relayCode);
                isOnLobby = false;
                //UpdateLobby -= HandleLobbyUpdate;
                //UpdateLobby += HandleLobbyUpdateMatchStarted;
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    public void Reconnect()
    {
        testRelay.Disconnect();
        LeaveOldLobby();
        if (hostOrder == actualHost.ToString() || IsLobbyHost())
        {
            CreateLobby();
        }
    }

    //public async void RestartRelay()
    //{
    //    if (hostOrder == actualHost.ToString() || IsLobbyHost())
    //    {
    //        try
    //        {
    //            relayCode = await testRelay.CreateRelay();
    //            UpdateJoinCode(relayCode);
    //            Debug.Log("New Relay code: " + relayCode);
    //            isOnLobby = false;
    //        }
    //        catch (LobbyServiceException e)
    //        {
    //            Debug.Log(e);
    //        }
    //    }
    //}

    public void ChangeActualHost()
    {
        actualHost++;
        if(actualHost >= joinedLobby.MaxPlayers)
        {
            actualHost= 0;
        }
    }
}