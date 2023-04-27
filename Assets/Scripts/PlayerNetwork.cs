using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{

    [SerializeField] private Transform spawnedObjectPrefab;
    private Transform spawnedObject;
    private Animator animator;
    private float speed = 5f;
    Vector2 lookDirection = new Vector2(1, 0);

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
        animator = GetComponent<Animator>();
    }

    private void Update()
    {

        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            TestServerRpc(new ServerRpcParams());
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            TestClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new List<ulong> { 1 }
                }
            });
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
        if (!Mathf.Approximately(moveDirection.x, moveDirection.y))
        {
            lookDirection.Set(moveDirection.x, moveDirection.y);
            lookDirection.Normalize();
        }
        transform.Translate(moveDirection * Time.deltaTime * speed);
        animator.SetFloat("Look X", lookDirection.x);
        animator.SetFloat("Look Y", lookDirection.y);
        animator.SetFloat("Speed", moveDirection.magnitude);

        if (!IsServer) return;

        if (Input.GetKeyDown(KeyCode.C))
        {
            spawnedObject = Instantiate(spawnedObjectPrefab);
            spawnedObject.GetComponent<NetworkObject>().Spawn(true);
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if (spawnedObject != null)
            {
                spawnedObject.GetComponent<NetworkObject>().Despawn(true);
            }
        }

    }

    [ServerRpc]
    private void TestServerRpc(ServerRpcParams serverRpcParams)
    {
        Debug.Log("TestServerRpc" + serverRpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void TestClientRpc(ClientRpcParams clientRpcParams)
    {
        Debug.Log("TestClientRpc");
    }

}
