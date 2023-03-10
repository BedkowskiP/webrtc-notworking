using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    private RTCPeerConnection localPeer;
    private RTCPeerConnection remotePeer;

    private MediaStream sendStream;
    private MediaStream recvStream;

    private string microphone = null;
    private AudioClip m_clipInput;
    private AudioStreamTrack m_audioTrack;

    int m_samplingFrequency = 48000;
    int m_lengthSeconds = 1;

    private bool answered = false;
    public bool autoStart = true;

    private string answer;

    private RTCOfferAnswerOptions options;

    #region JSON_Classes
    public class JSON_Offer
    {
        public RTCSdpType type;
        public string sdp;
    }

    #endregion

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button createAnswerButton;
    [SerializeField] private Button addAnswerButton;

    [Header("Input fields")]
    [SerializeField] private InputField localSdpField;
    [SerializeField] private InputField createAnswerField;
    [SerializeField] private InputField answerField;
    [SerializeField] private InputField pasteAnswerField;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _inputAudio;
    [SerializeField] private AudioSource _outputAudio;

	private void Start()
	{
        RTCOfferAnswerOptions options = new RTCOfferAnswerOptions();
        options.iceRestart = true;

        microphone = Microphone.devices[0];
        m_clipInput = Microphone.Start(microphone, true, m_lengthSeconds, m_samplingFrequency);
        Debug.Log("Selected microphone: " + microphone);
		if (autoStart)
		{
            OnStart();
		}
    }

    public void OnStart()
	{
        CreatePeer();
        //startButton.enabled = false;
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
            Debug.Log($"Local: IceCandidate: {e.Candidate}");
            if(e.Candidate != null)
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
            Debug.Log($"Remote: IceCandidate: {e.Candidate}");
            if (e.Candidate != null)
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

    private IEnumerator HandleLocalNegotiation()
    {
        
        Debug.Log("Creating offer");
        var op1 = localPeer.CreateOffer(ref options);
        yield return op1;
        Debug.Log("Offer created");
        var desc = op1.Desc;
        var op2 = localPeer.SetLocalDescription(ref desc);
        Debug.Log($"Offer: {localPeer.LocalDescription.sdp}");
        yield return op2;
        ToJSON(desc.type, localPeer.LocalDescription.sdp, localSdpField);
        Debug.Log("Local negotiation done. Waiting for answer.");
        yield return StartCoroutine(WaitForAnswer());
        Debug.Log("Setting local peer remote description");
        RTCSessionDescription desc2 = new RTCSessionDescription();
        desc2.type = RTCSdpType.Answer;
        desc2.sdp = answer;
        yield return desc2;
        var op6 = localPeer.SetRemoteDescription(ref desc2);
        yield return op6;
        if (op6.IsError) Debug.Log(op6.Error);
    }

    private IEnumerator CreateAnswerEnum()
    {
        Debug.Log("Setting remote description");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc = FromJSON(createAnswerField);
        var op3 = remotePeer.SetRemoteDescription(ref desc);
        yield return op3;
        Debug.Log("Creating answer");
        var op4 = remotePeer.CreateAnswer(ref options);
        yield return op4;
        Debug.Log("Answer created");
        desc = op4.Desc;
        //yield return StartCoroutine(WaitForAnswer());
        var op5 = remotePeer.SetLocalDescription(ref desc);
        yield return op5;
        Debug.Log($"Answer: {remotePeer.LocalDescription.sdp}");
        ToJSON(desc.type, remotePeer.LocalDescription.sdp, answerField);
        Debug.Log("Remote negotiation done. Add answer to local peer.");
        //answered = true;
    }

    public void AddAnswer()
    {
        answered = true;
    }

    private void ToJSON(RTCSdpType type, string sdp, InputField field)
	{
        JSON_Offer offer = new JSON_Offer();
        offer.type = type;
        offer.sdp = sdp;
        string json = JsonUtility.ToJson(offer);
        field.text = json;
	}

    private RTCSessionDescription FromJSON(InputField field)
    {
        RTCSessionDescription offer = new RTCSessionDescription();
        offer = JsonUtility.FromJson<RTCSessionDescription>(field.text);
        return offer;
    }

    private IEnumerator WaitForAnswer()
	{
        while (!answered)
        {
            Debug.Log("waiting");
            yield return null;
        }
        answer = FromJSON(pasteAnswerField).sdp;
        Debug.Log("Answered");
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

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] {
			new RTCIceServer {
                urls = new[] {
                    "stun:relay.metered.ca:80"
                    }
			},
			new RTCIceServer
			{
				urls = new[] {
					"turn:relay.metered.ca:80"
					},
				username = "d2d749feafa6ce4889dcc085",
				credential = "eIhOg/8GijfCjDfR"
			},
            new RTCIceServer
            {
                urls = new[] {
                    "turn:relay.metered.ca:443"
                    },
                username = "d2d749feafa6ce4889dcc085",
                credential = "eIhOg/8GijfCjDfR"
            },
            new RTCIceServer
            {
                urls = new[] {
                    "turn:relay.metered.ca:443?transport=tcp"
                    },
                username = "d2d749feafa6ce4889dcc085",
                credential = "eIhOg/8GijfCjDfR"
            }

        };

        return config;
    }

    public void restartIce()
	{
        localPeer.RestartIce();
        remotePeer.RestartIce();
        Debug.Log("Ice restarted.");
	}
}
