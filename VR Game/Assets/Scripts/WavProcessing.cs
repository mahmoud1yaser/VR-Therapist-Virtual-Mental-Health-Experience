using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.Networking;
using System.IO;

public class WavProcessing : MonoBehaviour
{
    public string baseUri = "http://<FLASK_HOST>:<FLASK_PORT>";
    private string baseWavPath = "Wav Files/";

    private AudioSource _audioSource;
    private Coroutine _recordAudioCoroutine;
    private Coroutine _sendWavFileCoroutine;

    private float recordBtnValue = 0f;
    private Image _imageLoader;
    private Button _recordBtn;
    private Button _resetBtn;
    private Button _stopBtn;

    private bool _isRecording;

    // The transform that the prefab or animator should look at
    private Transform lookAtTarget;


    // Start is called before the first frame update
    void Start()
    {
        _recordBtn = GameObject.Find("Record_Btn").GetComponent<Button>();
        _resetBtn = GameObject.Find("Reset_Btn").GetComponent<Button>();
        _stopBtn = GameObject.Find("Stop_Btn").GetComponent<Button>();


        _stopBtn.onClick.AddListener(EndRecording);
        _recordBtn.onClick.AddListener(RecordAndProcess);
        _resetBtn.onClick.AddListener(Reset);

        if (!Directory.Exists(Path.Combine(Application.persistentDataPath, baseWavPath)))
        {
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, baseWavPath));
        }

        // Get the AudioSource component from the prefab
        _audioSource = GetComponent<AudioSource>();

        // Get the Image component for image loader
        _imageLoader = GameObject.Find("ImageLoader").GetComponent<Image>();

        // Set the lookAtTarget to Patient
        lookAtTarget = GameObject.Find("Patient").transform;
    }

    // Update is called once per frame
    void Update()
    {
        // Make the prefab or animator always look at the player's head
        transform.LookAt(lookAtTarget.position);
    }

void RecordAndProcess()
    {
        // Record audio for 10 seconds

        _isRecording = true;
        if (_recordAudioCoroutine != null)
            StopCoroutine(RecordAudioCoroutine());

        _recordAudioCoroutine = StartCoroutine(RecordAudioCoroutine());

    }

    void EndRecording()
    {
        _isRecording = false;
    }


    private void Reset()
    {
        _isRecording = true;
        StartCoroutine(ResetProcess()); 
    }


    private IEnumerator ResetProcess()
    {
        string uri = $"{baseUri}/reset_conversation";
        WWWForm form = new WWWForm();
        form.AddField("reset_conversation", "yes");

        using (UnityWebRequest request = UnityWebRequest.Post(uri, form))
        {
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError(request.error);
            }
            else
            {
                Debug.Log(request.downloadHandler.text);
            }
        }
    }

    

    IEnumerator RecordAudioCoroutine()
    {
        string wavFilePath = Path.Combine(Application.persistentDataPath, $"{baseWavPath}patient_speech.wav");
        int recordingTime = 120;
        float fillSpeed = 1f / recordingTime;
        float fillAmount = 0f;
        _imageLoader.fillAmount = fillAmount;

        AudioClip recordedClip = Microphone.Start(null, false, recordingTime, 44100);

        while ((fillAmount < 1f) && (_isRecording))
        {
            fillAmount += fillSpeed * Time.deltaTime;
            _imageLoader.fillAmount = fillAmount;
            yield return null;
        }

        if (_isRecording) {
            // Wait for the recording time to elapse
            yield return new WaitForSeconds(recordingTime);
        }

        _imageLoader.fillAmount = 1f;

        Microphone.End(null);

        // Save the recorded audio as a wav file
        SavWav.Save(wavFilePath, recordedClip);

        // Disable all buttons
        _recordBtn.interactable = false;
        _resetBtn.interactable = false;
        _stopBtn.interactable = false;

        // Send a POST request to the Flask server with "patient_speech.wav" string
        if (_sendWavFileCoroutine == null)
            _sendWavFileCoroutine = StartCoroutine(SendPatientWav());

        // Check status periodically until it's "done"
        yield return StartCoroutine(CheckStatus());
    }

    IEnumerator SendPatientWav()
    {
        string uri = $"{baseUri}/process_wav";
        WWWForm form = new WWWForm();
        form.AddField("loaded_wav_file", "patient_speech");
        form.AddField("path", Path.Combine(Application.persistentDataPath, baseWavPath));
       

        using (UnityWebRequest request = UnityWebRequest.Post(uri, form))
        {
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError(request.error);
            }
            else
            {
                Debug.Log(request.downloadHandler.text);
            }
        }

        _sendWavFileCoroutine = null;

    }

    IEnumerator CheckStatus()
    {
        string uri = $"{baseUri}/check_status";

        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(uri))
            {
                yield return request.SendWebRequest();

                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.LogError(request.error);
                }
                else
                {
                    string response = request.downloadHandler.text;

                    if (response.Contains("done"))
                    {
                        // Start playing therapist_speech.wav
                        StartCoroutine(PlayTherapistWav());
                     
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(1f); // Wait for 1 seconds before checking status again
        }
    }

    IEnumerator PlayTherapistWav()
    {
        string therapistWavPath = Path.Combine(Application.persistentDataPath, $"{baseWavPath}therapist_speech.mp3");

        // Load the audio clip from therapistWavPath
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(therapistWavPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                // Assign the loaded audio clip to the AudioSource component
                _audioSource.clip = DownloadHandlerAudioClip.GetContent(www);

                if (_audioSource.clip == null)
                {
                    Debug.Log("Audio clip is empty");
                }

                // Play the audio clip
                _audioSource.Play();

                // Wait for the audio clip to finish playing
                yield return new WaitForSeconds(_audioSource.clip.length);

                // Activate all buttons
                _recordBtn.interactable = true;
                _resetBtn.interactable = true;
                _stopBtn.interactable = true;
            }
        }
    }
}