using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{

    private NetworkVariable<MyCustomData> randomNumber = new NetworkVariable<MyCustomData>(
        new MyCustomData
        {
            _int = 0,
            _bool = false,
            _fixedString = new FixedString32Bytes("Hello World")
        }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public FixedString32Bytes _fixedString;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref _fixedString);
        }

    }

    public override void OnNetworkSpawn()
    {
        randomNumber.OnValueChanged += (MyCustomData previousValue, MyCustomData newValue) =>
        {
            Debug.Log(OwnerClientId + "; randomNumber: " + newValue._int + "; " + newValue._bool);
        };
    }

    private void Update()
    {

        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.S))
        {
            TestServerRpc(new ServerRpcParams());
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            TestClientRpc();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            randomNumber.Value = new MyCustomData
            {
                _int = Random.Range(0, 100),
                _bool = Random.Range(0, 2) == 0,
                _fixedString = new FixedString32Bytes("New Message")
            };
        }

        Vector2 moveDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        transform.Translate(moveDirection * Time.deltaTime * 5f);
    }

    [ServerRpc]
    private void TestServerRpc(ServerRpcParams serverRpcParams)
    {
        Debug.Log("TestServerRpc" + serverRpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void TestClientRpc()
    {
        Debug.Log("TestClientRpc");
    }

}
