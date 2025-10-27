using UnityEngine;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Unity.WebRTC;
using System.Linq;

public class Main : MonoBehaviour
{
    private WebAccessor _webAccessor = null;
    private WebRTCController _webrtcCtrl = null;

    private WebCamTexture _webCamTexture;
    private AudioStreamTrack _audioStreamTrack;
    private VideoStreamTrack _videoStreamTrack;
    private MediaStream _localStream;

    private void Start()
    {
        SynchronizationContext mainContext = SynchronizationContext.Current;
        _webAccessor = new WebAccessor();
        _webrtcCtrl = new WebRTCController();
        _webrtcCtrl.OnLocalDescriptionCreated += async (desc) => {
            await _webAccessor.PostAsync("http://localhost:8080", desc);
        };
        _webrtcCtrl.OnIceCandidateCreated += async (candidate) => {
            await _webAccessor.PostAsync("http://localhost:8080", candidate);
        };

        _webAccessor.OnMessage = (message) => {
            HandleMessage(message);
        };
        Task.Run(() =>
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            _webAccessor.ConnectAsync("http://localhost:8080", "example", 
                mainContext, cts.Token);
        });
        StartCoroutine(StartLocalMediaAndAddTracks());
        _webrtcCtrl.Connect();
    }

    private void OnDestroy()
    {
        _webAccessor?.Dispose();

        _webCamTexture?.Stop();
        _audioStreamTrack?.Dispose();
        _videoStreamTrack?.Dispose();
        _localStream?.Dispose();
    }
    private void HandleMessage(string receivedData)
    {
        string[] splitted = receivedData.Split("data:");

        if(splitted.Length <= 1)
        {
            Debug.Log($"Invalid {receivedData}");
            return;
        }
        ClientMessage message = JsonUtility.FromJson<ClientMessage>(splitted[1]);
        if(message == null)
        {
            Debug.Log($"Failed deserialized: {receivedData}");
            return;
        }
        switch(message.@event)
        {
            case "offer":
                RTCSessionDescription offerMessage = JsonUtility.FromJson<RTCSessionDescription>(message.data);
                _webrtcCtrl.OnOfferReceived(offerMessage);
                break;
            case "candidate":
                IceCandidate candidateMessage = JsonUtility.FromJson<IceCandidate>(message.data);
                if (candidateMessage == null)
                {
                    Debug.Log($"Invalid candidate:{message.data}");
                    return;
                }
                RTCIceCandidateInit init = new RTCIceCandidateInit
                {
                    candidate = candidateMessage.candidate,
                    sdpMid = candidateMessage.sdpMid,
                    sdpMLineIndex = candidateMessage.sdpMLineIndex
                };
                _webrtcCtrl.OnCandidateReceived(new RTCIceCandidate(init));
                break;
            default:
                Debug.Log($"Other: {message.data}");
                break;
        }
    }
    public IEnumerator StartLocalMediaAndAddTracks()
    {
        // 1. Webカメラの初期化と映像トラックの生成
        yield return StartCoroutine(InitializeWebCam());

        // 2. マイクの初期化と音声トラックの生成
        InitializeMicrophone();

        // 3. ローカルストリームの作成
        _localStream = new MediaStream();
        if (_videoStreamTrack != null)
        {
            _localStream.AddTrack(_videoStreamTrack);
        }
        if (_audioStreamTrack != null)
        {
            _localStream.AddTrack(_audioStreamTrack);
        }

        // 4. トラックをPeerConnectionに追加
        if (_webrtcCtrl != null)
        {
            // WebRTCでは、MediaStream単位ではなく、個々のトラックをAddTrackするのが一般的です。
            if (_videoStreamTrack != null)
            {
                _webrtcCtrl.AddLocalTrack(_videoStreamTrack, _localStream);
            }
            if (_audioStreamTrack != null)
            {
                _webrtcCtrl.AddLocalTrack(_audioStreamTrack, _localStream);
            }
        }
        else
        {
            Debug.LogError("WebRTCController is not initialized.");
        }
    }

    // --- Webカメラ初期化 ---
    private IEnumerator InitializeWebCam()
    {
        // 利用可能なカメラを取得
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam devices found.");
            yield break;
        }

        // 最初のカメラを選択（必要に応じてユーザーに選択させる）
        WebCamDevice device = devices.First();
        
        // WebCamTextureの作成と開始
        _webCamTexture = new WebCamTexture(device.name, 1280, 720, 30);
        _webCamTexture.Play();

        // カメラが起動するのを待つ
        while (!_webCamTexture.didUpdateThisFrame)
        {
            yield return null;
        }

        // 映像トラックを生成
        _videoStreamTrack = new VideoStreamTrack(_webCamTexture);
        Debug.Log("WebCam initialized and VideoStreamTrack created.");
    }    
    private void InitializeMicrophone()
    {
        // AudioSourceコンポーネントが必要
        AudioSource audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 最初のマイクデバイス名を取得
        string micDeviceName = Microphone.devices.FirstOrDefault();
        if (string.IsNullOrEmpty(micDeviceName))
        {
            Debug.LogWarning("No microphone devices found.");
            _audioStreamTrack = null;
            return;
        }

        // マイクの録音を開始し、AudioSourceに設定
        audioSource.clip = Microphone.Start(micDeviceName, true, 10, AudioSettings.outputSampleRate);
        audioSource.loop = true;
        // マイクの音声をスピーカーから出力したくない場合はミュート
        // audioSource.mute = true;
        
        // add AudioStreamTrack
        _audioStreamTrack = new AudioStreamTrack(audioSource);
        Debug.Log($"Microphone '{micDeviceName}' initialized and AudioStreamTrack created.");
    }
}
