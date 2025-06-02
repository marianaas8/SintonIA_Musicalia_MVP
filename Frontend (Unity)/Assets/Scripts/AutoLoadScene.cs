using UnityEngine;
using UnityEngine.SceneManagement;

/*
 This script is responsible for loading a new Unity scene after a specified delay.
 It's commonly used for splash screens, introductory sequences, or timed transitions.
 */
public class LoadSceneAfterDelay : MonoBehaviour
{
    [Tooltip("The name of the scene to load. Make sure this scene is added to your Build Settings.")]
    public string sceneToLoad = "Scene1"; // Name of your target scene
    [Tooltip("The time in seconds to wait before loading the new scene.")]
    public float delay = 5f; // Seconds to wait before loading

    /*
     Called on the frame when a script is enabled just before any Update methods are called the first time.
     It schedules the `LoadScene` method to be called after the `delay` duration.
     */
    void Start()
    {
        // Invokes the "LoadScene" method after the specified 'delay' seconds.
        // This is a simple way to introduce a pause before scene transition.
        Invoke("LoadScene", delay);
    }

    /*
     Loads the scene specified by `sceneToLoad`.
     This method is called by the `Invoke` function after the delay.
     */
    void LoadScene()
    {
        // Uses Unity's SceneManager to load the target scene by its name.
        // Ensure that 'sceneToLoad' is correctly spelled and present in File > Build Settings.
        SceneManager.LoadScene(sceneToLoad);
    }
}