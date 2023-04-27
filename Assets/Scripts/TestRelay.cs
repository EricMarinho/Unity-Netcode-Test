using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;
using System.Threading.Tasks;
using System;

public class TestRelay : MonoBehaviour
{

    private void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += CheckHostDropped;
        //Maybe I will have to change to ShutdownInProgress or NetworkTransportFailure
    }

    public async Task<string> CreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log("Join code: " + joinCode);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();

            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.Log("error");
            Debug.Log(e);
            return null;
        }
        
    }

    public async void JoinRelay(string joinCode)
    {
        try
        {
            Debug.Log("Joining relay with code: " + joinCode);
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
    }

    public void CheckHostDropped(ulong clientID)
    {
        
        if (clientID == ((ulong)LobbyManager.Instance.actualHost))
        {
            Debug.Log("Host Dropped");
            //LobbyManager.Instance.ChangeActualHost();
            LobbyManager.Instance.Reconnect();
            LobbyManager.Instance.isOnLobby = true;
        }
        else
        {
            LobbyManager.Instance.KickPlayer(clientID);
        }
    }
}
