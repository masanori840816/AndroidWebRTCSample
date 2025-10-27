using UnityEngine;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class Main : MonoBehaviour
{
    private WebAccessor webAccessor = null;
    private VideoStreamTrack videoStreamTrack;
    private AudioStreamTrack audioStreamTrack;
    private MediaStream receiveAudioStream;
    private MediaStream receiveVideoStream;

    private void Start()
    {
        webAccessor = new WebAccessor();
        webAccessor.OnMessage = (message) => {
            Debug.Log(message);
        };
        Task.Run(() =>
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            webAccessor.ConnectAsync("http://localhost:8080", "example", 
                SynchronizationContext.Current, cts.Token);
        });
    }

    private void OnDestroy()
    {
        webAccessor?.Dispose();
    }
    private void HandleMessage(string jsonMessage)
    {
        ClientMessage message = JsonUtility.FromJson<ClientMessage>(jsonMessage);
        if(message == null)
        {
            Debug.Log("Failed deserialized");
        }
        Debug.Log($"Data: {message.data}");
    }
}
