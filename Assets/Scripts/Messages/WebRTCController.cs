using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.WebRTC;

public class WebRTCController
{
    private RTCPeerConnection _peerConnection = null;
    private List<RTCRtpSender> pc1Senders;
    private VideoStreamTrack videoStreamTrack;
    private AudioStreamTrack audioStreamTrack;
    private MediaStream receiveAudioStream;
    private MediaStream receiveVideoStream;
    private WebCamTexture webCamTexture;
    public event Action<ClientMessage> OnLocalDescriptionCreated;
    public event Action<ClientMessage> OnIceCandidateCreated;

    public void Connect()
    {
        if (_peerConnection != null)
        {
            _peerConnection.Dispose();
        }
        RTCConfiguration config = GetConfig();
        _peerConnection = new RTCPeerConnection(ref config);
        _peerConnection.OnIceCandidate = HandleIceCandidate;
        _peerConnection.OnConnectionStateChange = HandleConnectionStateChange;
        _peerConnection.OnTrack = HandleTrackEvent;

    }
    public void Close()
    {
        if (_peerConnection != null)
        {
            _peerConnection.Close();
            _peerConnection.Dispose();
            _peerConnection = null;
            Debug.Log("RTCPeerConnection closed and disposed.");
        }
    }
    public void AddLocalTrack(MediaStreamTrack track, MediaStream stream)
    {
        if (_peerConnection != null)
        {
            _peerConnection.AddTrack(track, stream);
            Debug.Log($"Local track added: {track.Kind}");
        }
    }
    public async void OnOfferReceived(RTCSessionDescription offer)
    {
        if (_peerConnection == null) return;
        
        // 1. リモートのOfferを設定
        RTCSetSessionDescriptionAsyncOperation setRemoteOp = _peerConnection.SetRemoteDescription(ref offer);
        while (!setRemoteOp.IsDone)
        {
            await Task.Delay(10); // 短時間待機してUIスレッドをブロックしないようにする
        }
        if (setRemoteOp.IsError)
        {
            Debug.LogError($"Failed to set remote Offer: {setRemoteOp.Error.message}");
            return;
        }

        // 2. Answerを生成
        RTCSessionDescriptionAsyncOperation createAnswerOp = _peerConnection.CreateAnswer();
        while (!createAnswerOp.IsDone)
        {
            await Task.Delay(10);
        }

        if (createAnswerOp.IsError)
        {
            Debug.LogError($"Failed to create Answer: {createAnswerOp.Error.message}");
            return;
        }
        // 3. ローカルのAnswerを設定し、シグナリングサーバーへ送信
        await SetAndSignalLocalDescription(createAnswerOp.Desc);
    }
    private RTCConfiguration GetConfig()
    {
        RTCConfiguration result = default;
        result.iceServers = new[] { new RTCIceServer {
            urls = new[] { "stun:stun.l.google.com:19302" } 
        } };
        return result;
    }
    public void OnCandidateReceived(RTCIceCandidate candidate)
    {
        Debug.Log($"OnCandidateReceived PC? {(_peerConnection == null)}");
        if (_peerConnection == null) return;
        
        // ICE Candidateを追加
        _peerConnection.AddIceCandidate(candidate);
        Debug.Log($"Added remote ICE Candidate: {candidate.Candidate}");
    }
    private async Task SetAndSignalLocalDescription(RTCSessionDescription desc)
    {
        Debug.Log("SetAndSignalLocalDescription");
        // 1. ローカルのSDPを設定
        RTCSetSessionDescriptionAsyncOperation setLocalOp = _peerConnection.SetLocalDescription(ref desc);
        while (!setLocalOp.IsDone)
        {
            await Task.Delay(10);
        }

        if (setLocalOp.IsError)
        {
            Debug.LogError($"Failed to set local description: {setLocalOp.Error.message}");
            return;
        }

        // 2. シグナリングサーバーへ送信するためのイベントを発火
        OnLocalDescriptionCreated?.Invoke(GenerateAnswerMessage(desc));
        Debug.Log($"Set local {desc.type} and signaled to server.");
    }
    // ICE Candidateが生成された時に呼ばれる
    private void HandleIceCandidate(RTCIceCandidate candidate)
    {
        // シグナリングサーバーへ送信するためのイベントを発火
        OnIceCandidateCreated?.Invoke(GenerateCandidateMessage(candidate));
        Debug.Log($"Generated local ICE Candidate and signaled: {candidate.Candidate}");
    }

    // 接続状態が変化した時に呼ばれる
    private void HandleConnectionStateChange(RTCPeerConnectionState state)
    {
        Debug.Log($"Connection State Changed: {state}");
        if (state == RTCPeerConnectionState.Connected)
        {
            Debug.Log("WebRTC Connection Established!");
        }
    }

    // リモートピアからトラック（メディアストリーム）が追加された時に呼ばれる
    private void HandleTrackEvent(RTCTrackEvent e)
    {
        MediaStream remoteStream = e.Streams.FirstOrDefault();

        if (remoteStream != null)
        {
            Debug.Log($"Remote Track Received! Kind: {e.Track.Kind}, Stream ID: {remoteStream.Id}");
        }
        else
        {
            Debug.Log($"Remote Track Received! Kind: {e.Track.Kind}, No Stream ID found.");
        }
    }
    private ClientMessage GenerateAnswerMessage(RTCSessionDescription answerDescription)
    {
        SessionDescription desc = new SessionDescription
        {
            sdp = answerDescription.sdp,
        };
        switch(answerDescription.type)
        {
            case RTCSdpType.Answer:
                desc.type = "answer";
                break;
            default:
                // Answer以外は無いはず
                Debug.Log($"Generate other type {answerDescription.type}");
                break;
        }
        return new ClientMessage
        {
            @event = "answer",
            userName = "example",
            data = JsonUtility.ToJson(desc),
        };
    }
    private ClientMessage GenerateCandidateMessage(RTCIceCandidate newCandidate)
    {
        IceCandidate cnd = new IceCandidate
        {
            candidate = newCandidate.Candidate,
            sdpMid = newCandidate.SdpMid,
            sdpMLineIndex = newCandidate.SdpMLineIndex,
            usernameFragment = newCandidate.UserNameFragment,
        };
        return new ClientMessage
        {
            @event = "candidate",
            userName = "example",
            data = JsonUtility.ToJson(cnd),
        };
    }
}
