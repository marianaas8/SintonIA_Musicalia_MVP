using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Required for Encoding

/*
This script handles audio communication with a Python AI backend for an avatar.
It first initializes the AI system on the server by sending an API Key.
Then, it records user audio, sends it to a specified API endpoint, and plays back the AI's audio response.
It also manages UI feedback, plays 'thinking' audio, and detects/emits emotion states.
*/

public class AvatarAIAudioCommunicator : MonoBehaviour
{
    // Actions for external subscriptions
    public static Action<string> OnEmotionDetected; // Event triggered when an emotion is detected from the AI response
    public static Action<bool> OnTalkingStateChanged; // Event triggered when the avatar starts or stops talking/thinking

    [Header("API Configuration")]
    [Tooltip("Base URL of the Python server. E.g., https://your-render-app.onrender.com or http://localhost:5000")]
    public string pythonServerBaseUrl = "https://musicalia-rtkk.onrender.com"; // Default to Render
    // public string pythonServerBaseUrl = "http://localhost:5000"; // Uncomment for local Windows
    // public string pythonServerBaseUrl = "http://<your_ip>:5000"; // Uncomment for local macOS, replace <your_ip>

    private string pythonApiInitializationUrl => $"{pythonServerBaseUrl}/initialize_ai";
    private string pythonApiInteractUrl => $"{pythonServerBaseUrl}/interact_audio";


    [Tooltip("Name of the environment variable storing the OpenAI API Key.")]
    public string openAIApiKeyEnvVarName = "OPENAI_API_KEY"; // Use a specific name


    [Tooltip("AudioSource component to play the received audio response OR the fallback audio.")]
    public AudioSource audioSource;
    [Tooltip("UI Text component to display status or messages.")]
    public TMP_Text responseTextUI;

    [Tooltip("Maximum effective recording duration in seconds before sending to the server. Suggested: 15-20 seconds.")]
    [Range(5f, 30f)]
    public float maxEffectiveRecordingDuration = 15f;

    [Tooltip("Filename used when sending the audio (WAV format).")]
    public string filename = "user_audio.wav";

    [Header("Fallback Audio")]
    [Tooltip("The AudioClip to play when an error occurs during server communication.")]
    public AudioClip fallbackAudioClip;

    [Header("Thinking Audio")]
    [Tooltip("A list of AudioClips to play while waiting for the server response (e.g., hmm, thinking...).")]
    public AudioClip[] thinkingAudioClips;

    private bool isRecording = false;
    private AudioClip recordedClip;
    private int recordingStartSample = 0;
    private AudioClip currentThinkingClip; // Keep track of the currently playing thinking clip

    // Flag to control if any relevant audio is playing (talking or thinking)
    private bool isAnyAudioPlaying = false;
    private bool isAiSystemInitialized = false; // New flag for AI system readiness
    private string openAIApiKey;


    // Dictionary mapping emotion codes from the server to their display names
    private Dictionary<int, string> emotionCodeMap = new Dictionary<int, string>
    {
        { -2, "Pensar" }, // Thinking state
        { 0, "Neutro" },   // Neutral emotion
        { 1, "Feliz" },    // Happy emotion
        { 2, "Triste" }    // Sad emotion
    };

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) Debug.LogError("AudioSource not assigned or found. Audio playback will not function.");
        }
        if (audioSource != null) audioSource.loop = false;

        // Validate URLs
        if (string.IsNullOrEmpty(pythonServerBaseUrl))
        {
            Debug.LogError("Python Server Base URL is not set in the Inspector!");
            if (responseTextUI != null) responseTextUI.text = "Server URL Missing!";
            return;
        }


        // Get API Key from Environment Variable
        openAIApiKey = Environment.GetEnvironmentVariable(openAIApiKeyEnvVarName);
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            Debug.LogError($"OpenAI API Key not found in environment variable '{openAIApiKeyEnvVarName}'. AI system cannot be initialized.");
            if (responseTextUI != null) responseTextUI.text = $"API Key Env Var '{openAIApiKeyEnvVarName}' Missing!";
            isAiSystemInitialized = false;
            return;
        }
        else
        {
            Debug.Log($"API Key loaded from env var '{openAIApiKeyEnvVarName}'.");
        }

        // Start AI System Initialization
        StartCoroutine(InitializeAISystem());
    }

    IEnumerator InitializeAISystem()
    {
        if (responseTextUI != null) responseTextUI.text = "Initializing AI System...";
        Debug.Log($"Attempting to initialize AI system at {pythonApiInitializationUrl}...");

        // Create JSON payload
        string jsonPayload = $"{{\"api_key\": \"{openAIApiKey}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(pythonApiInitializationUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60; // 60 seconds timeout for initialization

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log($"AI System Initialization Response: {responseJson}");
                // Optionally parse the JSON response if needed, e.g., to check a specific message
                // For now, success status code (200-299) is enough.
                isAiSystemInitialized = true;
                if (responseTextUI != null) responseTextUI.text = "AI Ready! Press Space to talk.";
                Debug.Log("AI System Initialized successfully on the server.");
            }
            else
            {
                isAiSystemInitialized = false;
                string errorMsg = $"Failed to initialize AI system. Error: {request.error}";
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    errorMsg += $"\nServer Response: {request.downloadHandler.text}";
                }
                Debug.LogError(errorMsg);
                if (responseTextUI != null) responseTextUI.text = "AI Initialization Failed. Check Logs.";
                // Optionally, implement retry logic here or guide the user.
            }
        }
    }


    void Update()
    {
        // Check for Spacebar press to start/stop recording
        // Only allow if AI system is initialized
        if (isAiSystemInitialized && Input.GetKeyDown(KeyCode.Space))
        {
            if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecordingAndSend();
            }
        }
        else if (!isAiSystemInitialized && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.LogWarning("AI System not initialized. Cannot start recording.");
            if (responseTextUI != null && responseTextUI.text != "AI Initialization Failed. Check Logs." && responseTextUI.text != $"API Key Env Var '{openAIApiKeyEnvVarName}' Missing!" && responseTextUI.text != "Server URL Missing!")
            {
                if (string.IsNullOrEmpty(openAIApiKey))
                {
                    responseTextUI.text = $"API Key Env Var '{openAIApiKeyEnvVarName}' Missing!";
                }
                else
                {
                    responseTextUI.text = "AI not ready. Initializing...";
                }
            }
        }


        if (isAnyAudioPlaying && audioSource != null && !audioSource.isPlaying)
        {
            isAnyAudioPlaying = false;
            audioSource.loop = false;
            OnTalkingStateChanged?.Invoke(false);
            Debug.Log("AudioSource stopped playing. Notifying OnTalkingStateChanged(false).");
        }
    }

    public void StartRecording()
    {
        if (!isAiSystemInitialized)
        {
            Debug.LogError("Cannot start recording: AI System is not initialized.");
            if (responseTextUI != null) responseTextUI.text = "AI Not Initialized!";
            return;
        }

        if (!isRecording)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.loop = false;
                isAnyAudioPlaying = false;
                OnTalkingStateChanged?.Invoke(false);
            }

            Debug.Log("Starting recording...");
            int bufferDuration = (int)(maxEffectiveRecordingDuration * 2);
            recordedClip = Microphone.Start(null, false, bufferDuration, 44100);
            if (recordedClip == null)
            {
                Debug.LogError("Failed to start microphone. Check permissions.");
                if (responseTextUI != null) responseTextUI.text = "Microphone Error.";
                return;
            }
            recordingStartSample = Microphone.GetPosition(null);
            isRecording = true;
            if (responseTextUI != null) responseTextUI.text = "Recording...";
        }
    }

    public void StopRecordingAndSend()
    {
        if (!isAiSystemInitialized)
        {
            Debug.LogError("Cannot stop recording/send: AI System is not initialized.");
            if (responseTextUI != null) responseTextUI.text = "AI Not Initialized!";
            isRecording = false; // Ensure recording flag is reset
            return;
        }

        if (isRecording)
        {
            Debug.Log("Stopping recording and sending...");
            int recordingEndSample = Microphone.GetPosition(null);
            Microphone.End(null);

            if (recordedClip == null || recordedClip.samples == 0 || recordingEndSample == recordingStartSample) // Allow recordingEndSample to be less if it wrapped
            {
                Debug.LogWarning("Recorded audio is likely empty or very short.");
                // Allow proceeding to ExtractClipData which handles wrap-around logic.
                // ExtractClipData will return null if it's truly invalid.
            }

            AudioClip finalClipToSend = ExtractClipData(recordedClip, recordingStartSample, recordingEndSample);
            if (recordedClip != null) Destroy(recordedClip); // Destroy the temporary full buffer clip
            recordedClip = null;

            if (finalClipToSend == null || finalClipToSend.samples == 0)
            {
                Debug.LogError("Failed to extract recorded audio or it was empty. The final clip is empty.");
                if (responseTextUI != null) responseTextUI.text = "Empty or Error in Audio.";
                isRecording = false;
                HandleAudioStopImmediately();
                return;
            }
            Debug.Log($"Effective recorded audio duration: {finalClipToSend.length}s.");

            isRecording = false;
            OnEmotionDetected?.Invoke("Pensar");

            StartCoroutine(HandleThinkingAndSend(finalClipToSend));
        }
    }

    private IEnumerator HandleThinkingAndSend(AudioClip finalClipToSend)
    {
        yield return new WaitForSeconds(0.5f);

        if (thinkingAudioClips != null && thinkingAudioClips.Length > 0 && audioSource != null)
        {
            int randomIndex = UnityEngine.Random.Range(0, thinkingAudioClips.Length);
            currentThinkingClip = thinkingAudioClips[randomIndex];
            audioSource.clip = currentThinkingClip;
            audioSource.loop = true;
            float randomStartTime = UnityEngine.Random.Range(0f, Mathf.Max(0, currentThinkingClip.length - 0.1f));
            audioSource.time = randomStartTime;
            audioSource.Play();
            isAnyAudioPlaying = true;
            OnTalkingStateChanged?.Invoke(true);
            if (responseTextUI != null) responseTextUI.text = "Thinking...";
            Debug.Log($"Playing 'thinking' sound: {currentThinkingClip.name} from {randomStartTime:F2} seconds");
        }

        StartCoroutine(PostAudioRequestToAI(finalClipToSend));
    }

    private AudioClip ExtractClipData(AudioClip sourceClip, int startSample, int endSample)
    {
        if (sourceClip == null) return null;
        // If startSample and endSample are the same, it means no new audio was captured by Microphone.GetPosition
        // or the microphone hasn't advanced. This can happen with very short taps.
        if (startSample == endSample && Microphone.GetPosition(null) == startSample)
        {
            Debug.LogWarning("ExtractClipData: startSample and endSample are identical, and mic position hasn't changed. Likely no audio.");
            return null;
        }


        float[] samples;
        int numSamples = 0;
        int channels = sourceClip.channels;
        int frequency = sourceClip.frequency;
        int totalBufferSamples = sourceClip.samples; // Total samples in the circular buffer

        if (endSample >= startSample) // Standard case or full buffer record (endSample might equal totalBufferSamples if it recorded exactly the buffer length from start)
        {
            numSamples = endSample - startSample;
        }
        else // Wrapped recording (endSample is before startSample in the buffer indices)
        {
            numSamples = (totalBufferSamples - startSample) + endSample;
        }

        // If numSamples is 0 after calculation (e.g., start and end are same and not wrapped), return null.
        if (numSamples <= 0)
        {
            Debug.LogWarning($"ExtractClipData: Calculated numSamples is {numSamples}. No audio data to extract.");
            return null;
        }

        // Trim to maxEffectiveRecordingDuration
        int maxEffectiveSamples = Mathf.FloorToInt(maxEffectiveRecordingDuration * frequency);
        if (numSamples > maxEffectiveSamples)
        {
            Debug.Log($"ExtractClipData: Recorded audio ({numSamples / frequency}s) exceeds max effective duration ({maxEffectiveRecordingDuration}s). Trimming.");
            numSamples = maxEffectiveSamples;
        }


        samples = new float[numSamples * channels];

        if (endSample >= startSample)
        {
            int actualStartSample = (endSample - startSample > numSamples) ? (endSample - numSamples) : startSample;
            sourceClip.GetData(samples, actualStartSample);
        }
        else // Wrapped recording
        {
            int samplesToEndOfBuffer = totalBufferSamples - startSample;
            int samplesFromStartOfBuffer = endSample;

            if (numSamples == maxEffectiveSamples) // Was trimmed
            {

                float[] fullWrappedSamples = new float[((totalBufferSamples - startSample) + endSample) * channels];
                float[] part1 = new float[samplesToEndOfBuffer * channels];
                sourceClip.GetData(part1, startSample);
                float[] part2 = new float[samplesFromStartOfBuffer * channels];
                sourceClip.GetData(part2, 0);
                Buffer.BlockCopy(part1, 0, fullWrappedSamples, 0, part1.Length * sizeof(float));
                Buffer.BlockCopy(part2, 0, fullWrappedSamples, part1.Length * sizeof(float), part2.Length * sizeof(float));

                // If the combined wrapped audio was longer than the (potentially trimmed) numSamples,
                // we take the LATEST 'numSamples' from this combined 'fullWrappedSamples'.
                if (fullWrappedSamples.Length / channels > numSamples)
                {
                    Buffer.BlockCopy(fullWrappedSamples, (fullWrappedSamples.Length - samples.Length) * sizeof(float), samples, 0, samples.Length * sizeof(float));
                }
                else
                {
                    // This case should ideally not happen if numSamples was set correctly
                    // or means fullWrappedSamples is shorter than or equal to the target (trimmed) numSamples.
                    Buffer.BlockCopy(fullWrappedSamples, 0, samples, 0, fullWrappedSamples.Length * sizeof(float));
                    if (fullWrappedSamples.Length < samples.Length)
                    {
                        Debug.LogWarning("ExtractClipData wrapped: fullWrappedSamples is smaller than target 'samples' array. Padding with zeros.");
                        // samples array will have zeros at the end.
                    }
                }
            }
            else // Not trimmed, use original wrapped logic
            {
                float[] tempPart1 = new float[samplesToEndOfBuffer * channels];
                sourceClip.GetData(tempPart1, startSample);
                float[] tempPart2 = new float[samplesFromStartOfBuffer * channels];
                sourceClip.GetData(tempPart2, 0);
                Buffer.BlockCopy(tempPart1, 0, samples, 0, tempPart1.Length * sizeof(float));
                Buffer.BlockCopy(tempPart2, 0, samples, tempPart1.Length * sizeof(float), tempPart2.Length * sizeof(float));
            }
        }

        if (numSamples <= 0)
        { // Double check after all calculations
            Debug.LogError("ExtractClipData: numSamples is zero or negative before creating AudioClip. Returning null.");
            return null;
        }

        AudioClip newClip = AudioClip.Create("RecordedAudioSegment", numSamples, channels, frequency, false);
        newClip.SetData(samples, 0);

        return newClip;
    }


    IEnumerator PostAudioRequestToAI(AudioClip audioClipToSend)
    {
        if (audioClipToSend == null || audioClipToSend.samples == 0)
        {
            Debug.LogError("No valid audio to send after extraction.");
            if (responseTextUI != null) responseTextUI.text = "Error: Empty Audio.";
            HandleAudioStopImmediately();
            yield break;
        }

        byte[] audioDataBytes = WavUtil.FromAudioClip(audioClipToSend);
        Destroy(audioClipToSend);

        if (audioDataBytes == null || audioDataBytes.Length == 0)
        {
            Debug.LogError("Failed to convert audio to WAV for sending.");
            if (responseTextUI != null) responseTextUI.text = "Error preparing audio.";
            HandleAudioStopImmediately();
            yield break;
        }

        Debug.Log($"WAV audio size to send: {audioDataBytes.Length / (1024f * 1024f):F2} MB.");
        if (audioDataBytes.Length > 25 * 1024 * 1024)
        {
            Debug.LogWarning($"[WARNING] The generated WAV file ({audioDataBytes.Length / (1024f * 1024f):F2} MB) exceeds OpenAI's recommended 25MB limit.");
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioDataBytes, filename, "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(pythonApiInteractUrl, form))
        {
            request.downloadHandler = new DownloadHandlerAudioClip(request.uri, AudioType.MPEG); // Expecting MP3
            request.timeout = 120;

            Debug.Log($"Sending audio request to {pythonApiInteractUrl}...");
            yield return request.SendWebRequest();
            Debug.Log($"Audio request completed. Result: {request.result}");

            HandleAudioStopImmediately(); // Stop thinking audio regardless of outcome

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"[REQUEST ERROR] Server communication failed for interact_audio: {request.error}";
                if (request.responseCode == 403)
                { // Forbidden - likely AI not initialized on server
                    errorMsg += "\nServer indicated: AI System Not Initialized. Attempting to re-initialize...";
                    Debug.LogError(errorMsg);
                    if (responseTextUI != null) responseTextUI.text = "AI Error. Re-initializing...";
                    isAiSystemInitialized = false; // Mark as not initialized
                    StartCoroutine(InitializeAISystem()); // Try to re-initialize
                }
                else
                {
                    string serverResponseText = "";
                    if (request.downloadHandler != null && request.downloadHandler.data != null && request.downloadHandler.data.Length > 0)
                    {
                        try { serverResponseText = Encoding.UTF8.GetString(request.downloadHandler.data); errorMsg += $"\n[DEBUG] Server Response: {serverResponseText}"; } catch { /* Ignore */ }
                    }
                    Debug.LogWarning(errorMsg);
                    TriggerFallbackLogic();
                }
            }
            else // Request was successful
            {
                string emotionHeader = request.GetResponseHeader("X-Musicalia-Emotion-Codes");
                string emotionDisplayString = "Emotion: N/A";

                if (!string.IsNullOrEmpty(emotionHeader))
                {
                    try
                    {
                        string[] codeStrings = emotionHeader.Split(',');
                        List<int> receivedEmotionCodes = codeStrings.Select(s => int.TryParse(s.Trim(), out int code) ? code : -1).Where(c => c != -1).ToList();

                        string prominentEmotion = GetProminentEmotion(receivedEmotionCodes);
                        if (!string.IsNullOrEmpty(prominentEmotion))
                        {
                            emotionDisplayString = $"Emotion: {prominentEmotion}";
                            Debug.Log($"Prominent emotion received from server: {prominentEmotion}");
                            OnEmotionDetected?.Invoke(prominentEmotion);
                        }
                        else
                        {
                            Debug.LogWarning("Emotion header received, but no valid or prominent emotion determined.");
                            emotionDisplayString = "Emotion: (Undetermined)";
                            OnEmotionDetected?.Invoke("Neutro");
                        }
                    }
                    catch (Exception headerEx)
                    {
                        Debug.LogError($"Error processing emotion header: {headerEx.Message}");
                        emotionDisplayString = "Error reading emotion.";
                        OnEmotionDetected?.Invoke("Neutro");
                    }
                }
                else
                {
                    Debug.LogWarning("Header 'X-Musicalia-Emotion-Codes' not found in response.");
                    emotionDisplayString = "Emotion: (Header missing)";
                    OnEmotionDetected?.Invoke("Neutro");
                }

                try
                {
                    Debug.Log("Request successful. Processing audio response...");
                    AudioClip responseAudioClip = DownloadHandlerAudioClip.GetContent(request);

                    if (responseAudioClip != null && responseAudioClip.loadState == AudioDataLoadState.Loaded)
                    {
                        if (audioSource != null)
                        {
                            // Check for effectively silent/empty audio from server
                            if (responseAudioClip.length < 0.1f && responseAudioClip.samples < 100)
                            {
                                Debug.LogWarning("Received very short or empty audio clip from server. Possibly no response or transcription was empty.");
                                if (responseTextUI != null) responseTextUI.text = $"No verbal response. {emotionDisplayString}";
                                // Do not play it, let Update handle OnTalkingStateChanged(false)
                                OnTalkingStateChanged?.Invoke(false); // Explicitly set to false as we are not playing.
                            }
                            else
                            {
                                audioSource.clip = responseAudioClip;
                                audioSource.Play();
                                isAnyAudioPlaying = true;
                                OnTalkingStateChanged?.Invoke(true);
                                Debug.Log($"Musicalia response audio played. Duration: {responseAudioClip.length}s.");
                                if (responseTextUI != null) responseTextUI.text = $"Musicalia Response. {emotionDisplayString}";
                            }
                        }
                        else Debug.LogError("AudioSource missing to play AI response.");
                    }
                    else
                    {
                        Debug.LogError("Request successful (Status 200), but failed to get/load AudioClip from response (invalid MP3 or empty?).");
                        if (request.downloadHandler != null)
                        {
                            Debug.LogError($"DownloadHandler error: {request.downloadHandler.error}");
                            if (request.downloadHandler.data != null && request.downloadHandler.data.Length > 0)
                            {
                                int previewLength = Math.Min(200, request.downloadHandler.data.Length);
                                string byteString = "First bytes received (Hex): ";
                                for (int i = 0; i < previewLength; i++) byteString += request.downloadHandler.data[i].ToString("X2") + " ";
                                Debug.Log("[DEBUG] " + byteString);
                                try { Debug.Log($"[DEBUG] Full server response (text attempt): {System.Text.Encoding.UTF8.GetString(request.downloadHandler.data)}"); } catch { Debug.Log("[DEBUG] Response is not UTF8 text."); }
                            }
                            else
                            {
                                Debug.LogWarning("DownloadHandler has no data, despite successful request. Server might have sent empty body for 200 OK.");
                            }
                        }
                        if (responseTextUI != null) responseTextUI.text = $"Error: Invalid Audio Response. {emotionDisplayString}";
                        // Don't trigger fallback here if it's a content issue, server responded 200.
                        // Let OnTalkingStateChanged(false) be handled by Update if nothing plays.
                        OnTalkingStateChanged?.Invoke(false); // Explicitly set to false
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"\n--- [CRITICAL ERROR CAUGHT] An exception occurred during successful response processing: {e.Message} ---");
                    Debug.LogError($"[CRITICAL ERROR CAUGHT] Stack Trace: {e.StackTrace}");
                    TriggerFallbackLogic(); // Fallback for critical processing errors post-success
                }
            }
        } // UnityWebRequest is disposed here
    }


    private void HandleAudioStopImmediately()
    {
        if (audioSource != null)
        {
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
        }
        isAnyAudioPlaying = false;
        // Only invoke if it was previously true, or let Update handle it to avoid redundant calls
        // However, for immediate stop, it's better to be explicit.
        OnTalkingStateChanged?.Invoke(false);
    }

    void TriggerFallbackLogic()
    {
        HandleAudioStopImmediately();

        if (fallbackAudioClip != null && audioSource != null)
        {
            Debug.LogWarning("Attempting to play fallback audio.");
            audioSource.clip = fallbackAudioClip;
            audioSource.Play();
            isAnyAudioPlaying = true;
            OnTalkingStateChanged?.Invoke(true);
            if (responseTextUI != null) responseTextUI.text = "Communication Error. Playing fallback.";
        }
        else
        {
            Debug.LogError("Fallback audio not assigned or AudioSource missing.");
            if (responseTextUI != null) responseTextUI.text = "Critical Error: Fallback unavailable.";
            OnTalkingStateChanged?.Invoke(false); // Ensure state is false if no fallback
        }
        OnEmotionDetected?.Invoke("Neutro");
    }

    private string GetProminentEmotion(List<int> emotionCodes)
    {
        if (emotionCodes == null || emotionCodes.Count == 0) return "Neutro";

        Dictionary<int, int> emotionCounts = new Dictionary<int, int>();
        foreach (int code in emotionCodes)
        {
            if (emotionCodeMap.ContainsKey(code))
            {
                if (emotionCounts.ContainsKey(code)) emotionCounts[code]++;
                else emotionCounts[code] = 1;
            }
        }
        if (emotionCounts.Count == 0) return "Neutro"; // No known codes found

        // Prioritize Happy (1) or Sad (2) if they are the most frequent among all known codes
        int maxCount = 0;
        int prominentCode = 0; // Default to Neutral
        bool foundEmotional = false;

        foreach (var pair in emotionCounts.OrderByDescending(p => p.Value))
        {
            if (pair.Key == 1 || pair.Key == 2) // Happy or Sad
            {
                if (pair.Value > maxCount)
                {
                    maxCount = pair.Value;
                    prominentCode = pair.Key;
                    foundEmotional = true;
                }
            }
            else if (pair.Key == 0 && !foundEmotional)
            { // Neutral
                if (pair.Value > maxCount)
                { // Only consider neutral if it's more prominent than any emotional one found so far (which is none)
                    maxCount = pair.Value;
                    prominentCode = pair.Key;
                }
            }
        }
        // If after checking all, only neutral was considered (or nothing with higher count than initial neutral if it was first)
        // and an emotional candidate was found (even if not most frequent overall, but most frequent among emotional ones)
        // we might prefer the emotional one. The current logic takes the absolute most frequent of H/S, then N if H/S not dominant.

        // Simplified: get most frequent among Happy/Sad. If none, take most frequent Neutral. If none, default.
        var emotionalCandidates = emotionCounts.Where(p => p.Key == 1 || p.Key == 2).OrderByDescending(p => p.Value).ToList();
        if (emotionalCandidates.Any())
        {
            return emotionCodeMap[emotionalCandidates.First().Key];
        }
        else if (emotionCounts.ContainsKey(0))
        {
            return emotionCodeMap[0]; // Neutral
        }

        return "Neutro"; // Fallback
    }


    // WavUtil and SavWav classes remain unchanged.
    // Make sure they are included in your script if they are not in separate files.
    public static class WavUtil
    {
        public static byte[] FromAudioClip(AudioClip audioClip)
        {
            if (audioClip == null || audioClip.samples == 0)
            {
                Debug.LogWarning("WavUtil.FromAudioClip: AudioClip is null or has no samples.");
                return null;
            }

            string tempFilename = Guid.NewGuid().ToString() + ".wav";
            string filePath = Path.Combine(Application.temporaryCachePath, tempFilename);

            bool saveSuccess = SavWav.Save(filePath, audioClip);
            if (!saveSuccess || !File.Exists(filePath))
            {
                Debug.LogError($"Failed to save/find temp WAV file: {filePath}");
                return null;
            }

            byte[] fileData = null;
            try
            {
                fileData = File.ReadAllBytes(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading temp WAV file: {e.Message}");
                fileData = null;
            }
            finally
            {
                try { if (File.Exists(filePath)) File.Delete(filePath); }
                catch (Exception e) { Debug.LogWarning($"Failed to delete temp WAV: {e.Message}"); }
            }
            return fileData;
        }
    }

    public static class SavWav
    {
        private const int HEADER_SIZE = 44;

        public static bool Save(string filename, AudioClip clip)
        {
            if (clip == null || clip.samples == 0)
            {
                Debug.LogWarning($"SavWav.Save: AudioClip for '{filename}' is null or has no samples.");
                return false;
            }
            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogError("SavWav.Save: Filename is null or empty.");
                return false;
            }


            var directoryName = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                try { Directory.CreateDirectory(directoryName); }
                catch (Exception e)
                {
                    Debug.LogError($"SavWav.Save: Error creating directory '{directoryName}': {e.Message}");
                    return false;
                }
            }

            FileStream fileStream = null;
            try
            {
                fileStream = File.Create(filename);
                WriteHeader(fileStream, clip);
                WriteData(fileStream, clip);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving WAV file '{filename}': {e.Message}");
                try { if (File.Exists(filename)) File.Delete(filename); } catch { }
                return false;
            }
            finally
            {
                if (fileStream != null) fileStream.Dispose();
            }
        }

        static void WriteHeader(FileStream stream, AudioClip clip)
        {
            var hz = clip.frequency;
            var channels = clip.channels;
            var samples = clip.samples;

            stream.Write(Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            uint dataSize = (uint)samples * (uint)channels * 2;
            uint chunkSize = HEADER_SIZE - 8 + dataSize;
            stream.Write(BitConverter.GetBytes(chunkSize), 0, 4);
            stream.Write(Encoding.UTF8.GetBytes("WAVE"), 0, 4);
            stream.Write(Encoding.UTF8.GetBytes("fmt "), 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(hz), 0, 4);
            stream.Write(BitConverter.GetBytes(hz * channels * 2), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)(channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
            stream.Write(Encoding.UTF8.GetBytes("data"), 0, 4);
            stream.Write(BitConverter.GetBytes(dataSize), 0, 4);
        }

        static void WriteData(FileStream stream, AudioClip clip)
        {
            var floatData = new float[clip.samples * clip.channels];
            clip.GetData(floatData, 0);
            var intData = new short[floatData.Length];
            var bytes = new byte[floatData.Length * 2];
            for (int i = 0; i < floatData.Length; i++)
            {
                intData[i] = (short)Mathf.Clamp(floatData[i] * short.MaxValue, short.MinValue, short.MaxValue);
            }
            Buffer.BlockCopy(intData, 0, bytes, 0, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}