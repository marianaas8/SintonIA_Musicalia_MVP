using UnityEngine;

/*
 This script provides a simple way to quit a Unity application
 or stop Play mode in the Unity Editor by pressing the Escape key.
 It's useful for quick exiting during development and in standalone builds.
 */
public class QuitOnEscape : MonoBehaviour
{
    /*
     Update is called once per frame.
     It continuously checks for user input, specifically if the Escape key is pressed.
     */
    void Update()
    {
        // Checks if the Escape key was pressed down in the current frame.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitApp(); // Call the method to quit the application/editor
        }
    }

    /*
     Handles the logic for quitting the application.
     It behaves differently depending on whether the code is running in the Unity Editor
     or in a standalone build.
     */
    void QuitApp()
    {
#if UNITY_EDITOR
        // This code block will only be compiled and executed when running inside the Unity Editor.
        // It sets the `isPlaying` flag of the Unity Editor to false, effectively stopping Play mode.
        Debug.Log("Quitting application from Unity Editor...");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // This code block will only be compiled and executed when running in a standalone build
        // (e.g., Windows executable, macOS app, Android app).
        // `Application.Quit()` terminates the application.
        Debug.Log("Quitting application...");
        Application.Quit();
#endif
    }
}