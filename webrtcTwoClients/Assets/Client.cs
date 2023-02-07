using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    private RTCPeerConnection localPeer;
    private RTCPeerConnection remotePeer;

    [SerializeField] private AudioSource _inputAudio;
    [SerializeField] private AudioSource _outputAudio;

    [SerializeField] private InputField localText;
    [SerializeField] private InputField remoteText;

    private MediaStream sendStream;
    private MediaStream recvStream;

    private string microphone = null;
    private AudioClip m_clipInput;
    private AudioStreamTrack m_audioTrack;

    int m_samplingFrequency = 48000;
    int m_lengthSeconds = 1;

    private RTCDataChannel localChannel;
    private RTCDataChannel remoteChannel;

    private string localDesc;
    private string answer;

	private void Start()
	{
        microphone = Microphone.devices[0];
        m_clipInput = Microphone.Start(microphone, true, m_lengthSeconds, m_samplingFrequency);
        Debug.Log("Selected microphone: " + microphone);
        CreatePeer();
    }

    public void CreatePeer()
    {
        sendStream = new MediaStream();
        var configuration = GetSelectedSdpSemantics();
        localPeer = new RTCPeerConnection(ref configuration);
        remotePeer = new RTCPeerConnection(ref configuration);

        recvStream = new MediaStream();
        recvStream.OnAddTrack += OnAddTrack;

        localPeer.OnIceCandidate = e =>
        {
            remotePeer.AddIceCandidate(e);
        };
        localPeer.OnIceConnectionChange = (e) =>
        {
            Debug.Log($"Local: IceConnectionChange: {e}");
        };
        localPeer.OnConnectionStateChange = (e) =>
        {
            Debug.Log($"Local: ConnectionStateChange: {e}");
        };
        localPeer.OnIceGatheringStateChange = (e) =>
        {
            Debug.Log($"Local: IceGatheringStateChange: {e}");
        };
        localPeer.OnNegotiationNeeded = () => StartCoroutine(HandleLocalNegotiation());

        remotePeer.OnIceCandidate = e =>
        {
            localPeer.AddIceCandidate(e);
        };
        remotePeer.OnIceConnectionChange = (e) =>
        {
            Debug.Log($"Remote: IceConnectionChange: {e}");
        };
        remotePeer.OnConnectionStateChange = (e) =>
        {
            Debug.Log($"Remote: ConnectionStateChange: {e}");
        };
        remotePeer.OnIceGatheringStateChange = (e) =>
        {
            Debug.Log($"Remote: IceGatheringStateChange: {e}");
        };

        var transceiver2 = remotePeer.AddTransceiver(TrackKind.Audio);
        transceiver2.Direction = RTCRtpTransceiverDirection.RecvOnly;

        remotePeer.OnTrack = (RTCTrackEvent e) => handleRemoteTrack(e);

        _inputAudio.loop = true;
        _inputAudio.clip = m_clipInput;
        _inputAudio.Play();

        m_audioTrack = new AudioStreamTrack(_inputAudio);
        m_audioTrack.Loopback = true;
        localPeer.AddTrack(m_audioTrack, sendStream);
    }

    public void CreateAnswer()
	{
        StartCoroutine(CreateAnswerEnum());
    }

    public void handleRemoteTrack(RTCTrackEvent e)
    {
        Debug.Log("HandleRemoteTrack");
        if (e.Track is AudioStreamTrack)
        {
            recvStream.AddTrack(e.Track);
        }
    }

    void OnAddTrack(MediaStreamTrackEvent e)
    {
        Debug.Log("OnAddTrack");
        var track = e.Track as AudioStreamTrack;
        _outputAudio.SetTrack(track);
        _outputAudio.loop = true;
        _outputAudio.Play();
    }

    private IEnumerator HandleLocalNegotiation()
	{
        Debug.Log("Creating offer");
        var op1 = localPeer.CreateOffer();
        yield return op1;
        Debug.Log("Offer created");
        var desc = op1.Desc;
        var op2 = localPeer.SetLocalDescription(ref desc);
        yield return op2;
        localText.text = desc.sdp;
        Debug.Log("Local negotiation done. Waiting for answer.");
    }

    private IEnumerator CreateAnswerEnum()
    {
        Debug.Log("Setting remote description");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.type = RTCSdpType.Offer;
        desc.sdp = localText.text;
        var op3 = remotePeer.SetRemoteDescription(ref desc);
        yield return op3;
        Debug.Log("Creating answer");
        var op4 = remotePeer.CreateAnswer();
        yield return op4;
        Debug.Log("Answer created");
        desc = op4.Desc;
        var op5 = remotePeer.SetLocalDescription(ref desc);
        yield return op5;
        remoteText.text = desc.sdp;
        Debug.Log("Remote negotiation done. Add answer to local peer.");
    }

    private IEnumerator SetLocalRemoteDesc()
	{
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.type = RTCSdpType.Answer;
        desc.sdp = remoteText.text;
        var op6 = localPeer.SetRemoteDescription(ref desc);
        yield return op6;
    }

    public void buttonAction()
    {
        StartCoroutine(SetLocalRemoteDesc());
    }

    //private IEnumerator ExchangeOffer()
    //{
    //    var op1 = localPeer.CreateOffer();
    //    yield return op1;
    //    var desc = op1.Desc;
    //    var op2 = localPeer.SetLocalDescription(ref desc);
    //    yield return op2;
    //    desc = op1.Desc;
    //    var op3 = remotePeer.SetRemoteDescription(ref desc);
    //    yield return op3;
    //    var op4 = remotePeer.CreateAnswer();
    //    yield return op4;
    //    desc = op4.Desc;
    //    var op5 = remotePeer.SetLocalDescription(ref desc);
    //    yield return op5;
    //    desc = op4.Desc;
    //    var op6 = localPeer.SetRemoteDescription(ref desc);
    //    yield return op6;
    //}

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } };

        return config;
    }

    public void restartIce()
	{
        localPeer.RestartIce();
        remotePeer.RestartIce();
        Debug.Log("Ice restarted.");
	}
}
