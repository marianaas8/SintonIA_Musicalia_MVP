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
        if (currentEmotion != "Neutro")
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
        yield return new WaitForSeconds(delay); // Wait for the specified duration

        // Only reset if the current emotion is still Happy or Sad (to prevent overriding a new emotion)
        if (currentEmotion == "Feliz" || currentEmotion == "Triste")
        {
            SetEmotion("Neutro"); // Reset to Neutral
            Debug.Log($"Emotion timeout for {currentEmotion} reached. Emotion reset to Neutral.");
        }
        emotionTimerCoroutine = null; // Clear the coroutine reference
    }

    /*
     Coroutine that continuously cycles through different talk variant animations.
     If the avatar is talking, it alternates between variants 1 and 2.
     If not talking (idle), it randomly selects between variants 0, 1, and 2, ensuring it's not the same as the current one.
     */
    IEnumerator CycleTalkVariant(bool isTalking)
    {
        int currentVariant;

        // Immediately set an initial variant based on the talking state
        if (isTalking)
        {
            currentVariant = Random.Range(1, 3); // Randomly choose between 1 and 2
            avatarAnimator.SetInteger(talkVariantHash, currentVariant);
            Debug.Log($"Switching talk variant to: {currentVariant}");
        }
        else
        {
            // If not talking, choose an initial idle variant (0, 1, or 2)
            currentVariant = Random.Range(0, 3);
            avatarAnimator.SetInteger(talkVariantHash, currentVariant);
            Debug.Log($"Switching idle variant to: {currentVariant}");
        }

        // Loop indefinitely to keep cycling variants
        while (true)
        {
            // Wait for a random duration before changing the variant again
            yield return new WaitForSeconds(Random.Range(minTalkVariantCycleTime, maxTalkVariantCycleTime));

            if (isTalking)
            {
                // If talking, simply alternate between 1 and 2
                currentVariant = (currentVariant == 1) ? 2 : 1;
            }
            else
            {
                // If not talking, choose a new random variant from 0, 1, or 2,
                // making sure it's different from the current one to avoid redundant changes.
                currentVariant = new[] { 0, 1, 2 }
                    .Where(x => x != currentVariant) // Filter out the current variant
                    .OrderBy(x => Random.value)      // Randomize the order of remaining options
                    .First();                        // Take the first one
            }

            avatarAnimator.SetInteger(talkVariantHash, currentVariant); // Apply the new variant to the Animator
            Debug.Log($"Switching variant to: {currentVariant}");
        }
    }
}