using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

/*
 This script manages the animation states of an avatar based on its audio communication
 and detected emotions. It controls parameters on the Unity Animator component
 to synchronize avatar movements and expressions with speech and emotional states.
 */
public class AvatarAnimationController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The AudioSource component used for AI speech playback. (Though not directly used for animation timing here, often relevant for related logic)")]
    public AudioSource audioSource; // AI speech audio source
    [Tooltip("The Animator component controlling the avatar's animations.")]
    public Animator avatarAnimator; // Avatar's Animator component

    [Header("Animator Parameters")]
    [Tooltip("Name of the boolean parameter in the Animator that controls talking animation. (e.g., 'isTalking')")]
    public string isTalkingParameterName = "isTalking"; // Bool: is the avatar talking?
    [Tooltip("Name of the integer parameter in the Animator that controls emotion. (e.g., 'Emotion': 0=Neutral, 1=Happy, 2=Sad)")]
    public string emotionParameterName = "Emotion";      // Int: emotion (0=Neutral, 1=Happy, 2=Sad)
    [Tooltip("Name of the integer parameter in the Animator that controls different talk variants. (e.g., 'talkVariant')")]
    public string talkVariantParameterName = "talkVariant"; // Int: talk variant

    [Header("Animation Settings")]
    [Tooltip("Minimum time in seconds between changes of talk variant animation.")]
    public float minTalkVariantCycleTime = 4.0f; // Minimum time between talk variant changes
    [Tooltip("Maximum time in seconds between changes of talk variant animation.")]
    public float maxTalkVariantCycleTime = 8.0f; // Maximum time between talk variant changes
    [Tooltip("Maximum time in seconds that an emotion (Happy/Sad) will remain active before reverting to Neutral.")]
    public float maxEmotionDuration = 10.0f;

    // A map to convert emotion names (strings) received from the communicator to integer codes for the Animator.
    private Dictionary<string, int> emotionNameToCodeMap = new Dictionary<string, int>
    {
        { "Pensar", -2 },  // 'Thinking' state, often used for AI processing
        { "Neutro", 0 },   // Neutral emotion code
        { "Feliz", 1 },    // Happy emotion code
        { "Triste", 2 }    // Sad emotion code
    };

    // Dictionary for mapping emotions to a list of available speech variants.
    private Dictionary<string, List<int>> talkVariantsByEmotion = new Dictionary<string, List<int>>
    {
        { "Neutro", new List<int> { 1, 2, 3, 4 } },
        { "Feliz",  new List<int> { 1, 2, 3 } },
        { "Triste", new List<int> { 1, 2, 3 } }
    };

    // List of idle variants (not talking).
    private List<int> idleVariants = new List<int> { 0, 1, 2, 3 };

    // Cached Animator parameter hashes for performance
    private int isTalkingHash, emotionHash, talkVariantHash;

    // Coroutine references to control and stop them when needed
    private Coroutine cycleTalkCoroutine;
    private Coroutine emotionTimerCoroutine;
    private string currentEmotion = "Neutro"; // Tracks the currently active emotion

    /*
     Called when the script instance is being loaded.
     Caches the hash values for Animator parameters for efficient access.
     */
    void Awake()
    {
        isTalkingHash = Animator.StringToHash(isTalkingParameterName);
        emotionHash = Animator.StringToHash(emotionParameterName);
        talkVariantHash = Animator.StringToHash(talkVariantParameterName);
    }

    /*
     Called when the object becomes enabled and active.
     Subscribes to events from the AvatarAIAudioCommunicator to receive updates
     on emotion detection and talking state.
     */
    void OnEnable()
    {
        AvatarAIAudioCommunicator.OnEmotionDetected += HandleEmotionDetected;
        AvatarAIAudioCommunicator.OnTalkingStateChanged += SetIsTalking;
    }

    /*
     Called when the object becomes disabled or inactive.
     Unsubscribes from events to prevent memory leaks and ensure proper cleanup.
     */
    void OnDisable()
    {
        AvatarAIAudioCommunicator.OnEmotionDetected -= HandleEmotionDetected;
        AvatarAIAudioCommunicator.OnTalkingStateChanged -= SetIsTalking;
    }

    /*
     Called on the frame when a script is enabled just before any Update methods are called the first time.
     Performs initial setup and checks for required components.
     */
    void Start()
    {
        if (!audioSource || !avatarAnimator)
        {
            Debug.LogError("AudioSource or Animator not assigned! This script will be disabled.");
            enabled = false; // Disable the script if essential components are missing
            return;
        }

        // Initialize Animator parameters to their default states
        avatarAnimator.SetBool(isTalkingHash, false);
        avatarAnimator.SetInteger(emotionHash, 0); // Neutral emotion
        avatarAnimator.SetInteger(talkVariantHash, -1); // No specific talk variant active initially
    }

    /*
     Event handler for when an emotion is detected by the AvatarAIAudioCommunicator.
     Updates the avatar's emotion and starts/resets a timer for emotion duration.
     */
    private void HandleEmotionDetected(string emotionName)
    {
        currentEmotion = emotionName; // Store the newly detected emotion
        SetEmotion(currentEmotion);   // Apply the emotion to the Animator

        // If a non-neutral emotion is detected, start or reset the emotion timer
        if (currentEmotion != "Neutro" && currentEmotion != "Pensar")
        {
            if (emotionTimerCoroutine != null)
            {
                StopCoroutine(emotionTimerCoroutine); // Stop any existing timer
            }
            // Start a new coroutine to reset the emotion after a delay
            emotionTimerCoroutine = StartCoroutine(ResetEmotionAfterDelay(maxEmotionDuration));
        }
        else // If emotion is neutral, stop any active emotion timer
        {
            if (emotionTimerCoroutine != null)
            {
                StopCoroutine(emotionTimerCoroutine);
                emotionTimerCoroutine = null;
            }
        }
    }

    /*
     Event handler for when the avatar's talking state changes (starts or stops talking/thinking).
     Updates the Animator's 'isTalking' parameter and manages the talk variant cycling coroutine.
     Also resets emotion to Neutral when the avatar stops talking.
     */
    private void SetIsTalking(bool isTalking)
    {
        avatarAnimator.SetBool(isTalkingHash, isTalking); // Set the 'isTalking' boolean parameter

        // Stop any currently running talk variant cycling coroutine
        if (cycleTalkCoroutine != null)
            StopCoroutine(cycleTalkCoroutine);

        // Start a new talk variant cycling coroutine immediately
        cycleTalkCoroutine = StartCoroutine(CycleTalkVariant(isTalking));

        // Logic to reset emotion to Neutral when talking stops
        if (!isTalking)
        {
            if (emotionTimerCoroutine != null)
            {
                StopCoroutine(emotionTimerCoroutine);
                emotionTimerCoroutine = null;
            }
            SetEmotion("Neutro"); // Force emotion back to Neutral
            currentEmotion = "Neutro";
            Debug.Log("Stopped talking. Emotion reset to Neutral.");
        }
    }

    /*
     Sets the avatar's emotion in the Animator based on the provided emotion name.
     Uses the internal `emotionNameToCodeMap` to find the corresponding integer code.
     */
    private void SetEmotion(string emotionName)
    {
        // Try to get the integer code for the given emotion name
        if (emotionNameToCodeMap.TryGetValue(emotionName, out int emotionCode))
        {
            avatarAnimator.SetInteger(emotionHash, emotionCode); // Set the 'Emotion' integer parameter
            Debug.Log($"Emotion: {emotionName} ({emotionCode}) applied to Animator.");
        }
        else
        {
            Debug.LogWarning($"Emotion '{emotionName}' not found in map. Using Neutral.");
            avatarAnimator.SetInteger(emotionHash, emotionNameToCodeMap["Neutro"]); // Default to Neutral
        }
    }

    /*
     Coroutine that waits for a specified delay and then resets the avatar's emotion to Neutral,
     primarily for "Happy" or "Sad" emotions that should not persist indefinitely.
     */
    IEnumerator ResetEmotionAfterDelay(float delay)
    {
        // Log when the emotion timeout timer starts
        Debug.Log($"[Emotion Timer] Starting {delay} second timer for emotion: {currentEmotion}");

        // Store the time when the timer started
        float startTime = Time.time;

        // Wait for the specified duration
        yield return new WaitForSeconds(delay);

        // Log the elapsed time when the coroutine resumes
        float elapsedTime = Time.time - startTime;
        Debug.Log($"[Emotion Timer] Timer for {currentEmotion} (started at {startTime:F2}s) finished after {elapsedTime:F2}s.");


        // Only reset if the current emotion is still Happy or Sad (to prevent overriding a new emotion)
        if (currentEmotion == "Feliz" || currentEmotion == "Triste")
        {
            SetEmotion("Neutro"); // Reset to Neutral
            currentEmotion = "Neutro"; // Update the class variable too
            Debug.Log($"[Emotion Timer] Emotion timeout for {currentEmotion} reached. Emotion reset to Neutral.");
        }
        else
        {
            // This log will help you understand why it didn't reset, e.g., if another emotion took over
            Debug.Log($"[Emotion Timer] Emotion was already changed from {currentEmotion} (or became Pensar). No reset needed.");
        }

        emotionTimerCoroutine = null; // Clear the coroutine reference
        Debug.Log($"[Emotion Timer] emotionTimerCoroutine reference cleared.");
    }

    /*
    Coroutine that continuously cycles through different talk variant animations.
    If the avatar is talking, it alternates between variants based on the current emotion.
    If not talking (idle), it randomly selects between idle variants, ensuring it's not the same as the current one.
    */
    IEnumerator CycleTalkVariant(bool isTalking)
    {
        int currentVariant = -1; // Initialize current variant to an invalid value.
        List<int> availableVariants; // List to hold the available animation variants.

        while (true) // Loop indefinitely to continuously cycle variants.
        {
            // If the current emotion is "Pensar" (Thinking), we don't cycle variants.
            // We set the variant to -1 (neutral/none) and wait.
            if (currentEmotion == "Pensar")
            {
                if (avatarAnimator.GetInteger(talkVariantHash) != -1)
                {
                    avatarAnimator.SetInteger(talkVariantHash, -1); // Set animator parameter to -1.
                }
                yield return null; // Wait for the next frame.
                continue; // Skip the rest of the loop and start over.
            }

            // Determine which set of variants to use based on whether the avatar is talking.
            if (isTalking)
            {
                // Try to get talk variants specific to the current emotion.
                if (!talkVariantsByEmotion.TryGetValue(currentEmotion, out availableVariants))
                {
                    // If no specific variants for the emotion, fall back to "Neutro" (Neutral).
                    availableVariants = talkVariantsByEmotion["Neutro"];
                }
            }
            else
            {
                // If not talking, use the predefined idle variants.
                availableVariants = idleVariants;
            }

            int nextVariant; // Variable to store the chosen next variant.

            // Logic to select the next variant.
            if (availableVariants.Count > 1)
            {
                // If more than one variant is available, select a random one that is not the current variant.
                nextVariant = availableVariants.Where(v => v != currentVariant)
                        .OrderBy(v => Random.value) // Randomly order the filtered list.
                                             .First(); // Take the first element from the randomly ordered list.
            }
            else if (availableVariants.Count == 1)
            {
                // If only one variant is available, use that one.
                nextVariant = availableVariants[0];
            }
            else
            {
                // If no variants are available, log a warning and stop the coroutine.
                Debug.LogWarning($"No available variants for state (isTalking: {isTalking}, emotion: {currentEmotion}). Stopping cycle.");
                yield break; // Exit the coroutine.
            }

            currentVariant = nextVariant; // Update the current variant.
            avatarAnimator.SetInteger(talkVariantHash, currentVariant); // Set the animator parameter.
            Debug.Log($"Switching variant to: {currentVariant} (Talking: {isTalking}, Emotion: {currentEmotion})"); // Log the change.

            // Wait for a random duration before cycling to the next variant.
            yield return new WaitForSeconds(Random.Range(minTalkVariantCycleTime, maxTalkVariantCycleTime));
        }
    }
}