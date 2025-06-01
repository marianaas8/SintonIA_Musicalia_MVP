## SintonIA - Musicalia

---

This repository hosts **Musicalia**, an innovative AI-powered digital experience developed by **SintonIA**, a multidisciplinary team from the University of Porto. 

**Musicalia** is a real-time, AI-driven avatar designed not just to speak, but to **truly connect**. She listens, detects emotion, and responds accordingly — both in speech and in animation. Whether on stage or in cultural spaces, Musicalia is more than a tool — **she’s a creative partner**.

Already tested in **live performance**, Musicalia demonstrates what’s possible when technology doesn’t replace the artist, but rather **performs with them**.

While others offer **static, pre-scripted videos** or **generic avatar models**, we deliver:

- **Real-time, adaptive avatars**
- **Emotion & topic recognition**
- **Live synchronization of animation and voice**
- **Culturally grounded design**

The application is designed to run seamlessly on a **cloud-based server** (currently [Render](https://render.com)), enabling real-time AI interactions. It also supports **local execution**, offering flexibility for **development and testing**.

Musicalia currently supports:
- Audio input via microphone
- Emotion & topic analysis via AI
- Text-To-Speech response generation
- Real-time animation sync
- Cloud deployment + local fallback

---

### Technical Overview

The project comprises a Python backend for AI processing and a Unity frontend for the avatar and user interface.

#### AI Backend (Python)

The Python backend handles the core AI logic, including speech-to-text transcription, AI response generation, text-to-speech synthesis, and emotion detection.

**Key Components:**
* **OpenAI API:** Utilizes **Whisper-1** for robust speech-to-text transcription and **GPT-4o Mini** for generating conversational responses.
* **Edge TTS:** Powers the text-to-speech conversion, providing natural-sounding Portuguese voice.
* **Emotion Detection:** Simple keyword-based emotion analysis (happy, sad, neutral) is implemented to guide the avatar's expressions.
* **Flask:** A lightweight web framework exposing an `/interact_audio` endpoint for communication with the Unity frontend.
* **Vector Store:** Integrates with OpenAI's Vector Stores to provide context and information from a PDF file (`Info.pdf`) about Fado and Amália Rodrigues, allowing the AI to answer specific questions.

**How it Works (Backend):**
1.  The Unity application sends recorded user audio (WAV format) to the `/interact_audio` endpoint.
2.  The backend transcribes the audio using **OpenAI Whisper-1**.
3.  The transcribed text is fed to the **GPT-4o Mini** assistant (named "Musicalia"), which generates a relevant response.
4.  The response text undergoes emotion analysis, detecting happy, sad, or neutral tones per sentence.
5.  The AI-generated text is converted into audio bytes (MP3-like format) using **Edge TTS**.
6.  The audio bytes and the detected emotion codes are sent back to Unity.

**Local vs. Render Deployment:**
The Python backend can be run locally or deployed on a platform like Render. The code includes a check for the `PORT` environment variable, making it adaptable for Render deployment:

```python
        # Render (comment when local testing)
        port = int(os.environ.get("PORT", 5000))
        app.run(host="0.0.0.0", port=port) ```

For **local execution**, you can uncomment the `app.run` lines corresponding to your operating system (`Windows` or `Mac`) and comment out the `Render` specific lines.

```python
# For Windows (uncomment for local testing)
# app.run(debug=False, port=5000, threaded=True)

# For Mac (uncomment for local testing)
# app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)```

```

#### Unity Frontend

The Unity project provides the visual avatar, handles audio input, and communicates with the Python backend.

**Key Scripts:**
You'll find the C# scripts in the `Assets/Scripts` folder within the Unity project.

* **`AvatarAnimationController.cs`**:
    * Manages the avatar's animations based on its talking state and detected emotions.
    * **Public Variables:**
        * `AudioSource audioSource`: The audio source component playing the AI's speech.
        * `Animator avatarAnimator`: The Animator component controlling the avatar's animations.
        * `isTalkingParameterName`: String name of the boolean parameter in the Animator that controls if the avatar is talking (e.g., "isTalking").
        * `emotionParameterName`: String name of the integer parameter for emotion (e.g., "Emotion", with values 0=Neutro, 1=Feliz, 2=Triste).
        * `talkVariantParameterName`: String name of the integer parameter for talk variants (e.g., "talkVariant").
        * `minTalkVariantCycleTime`, `maxTalkVariantCycleTime`: Control how often the talk animation variants change.
        * `maxEmotionDuration`: How long a detected emotion (Happy/Sad) will persist before returning to Neutral.
    * **Functionality:** Subscribes to events from `AvatarAIAudioCommunicator` to update the avatar's animation parameters. It intelligently cycles through talk variants and resets emotions to neutral after a set duration.

* **`AvatarAIAudioCommunicator.cs`**:
    * Handles recording user audio, sending it to the Python API, receiving and playing the AI's audio response, and processing emotion data.
    * **Public Variables:**
        * `pythonApiUrl`: The URL of your Python Flask API endpoint. **You will need to set this directly in the Unity Inspector for the `AvatarAIAudioCommunicator` script.**
          * For Render deployment: https://musicalia-rtkk.onrender.com/interact_audio
          * For local Windows execution: http://localhost:5000/interact_audio
          * For local Mac execution: You'll need to get your local IP address using `ipconfig getifaddr en0` in your terminal and then set this URL to http://[your_mac_ip_address]:5000/interact_audio.
        * `audioSource`: The AudioSource component to play received audio.
        * `responseTextUI`: A `TMP_Text` component to display messages or status.
        * `maxEffectiveRecordingDuration`: The maximum duration for user audio recording.
        * `fallbackAudioClip`: An AudioClip to play if there's an error communicating with the server.
        * `thinkingAudioClips`: A list of AudioClips to play while waiting for the AI response.
    * **Functionality:**
        * **Recording:** Starts and stops microphone recording when the **Spacebar** is pressed.
        * **API Communication:** Converts recorded audio to WAV format and sends it as a `UnityWebRequest` POST request to the specified Python API URL.
        * **Response Handling:** Receives the AI's audio response and emotion codes (from the `X-Musicalia-Emotion-Codes` HTTP header). It then plays the audio and triggers the `OnEmotionDetected` and `OnTalkingStateChanged` events.
        * **Audio Playback:** Plays the received AI audio or a fallback/thinking audio if necessary.

---

### Unity Setup and Running

1.  **Install Unity:** If you don't have Unity installed, download the Unity Hub from the [Unity website](https://unity.com/download) and install a recent stable version of the editor (e.g., Unity 2022.3 LTS or newer).
2.  **Open the Project:**
    * Launch Unity Hub.
    * Click "Add" and navigate to the root directory of your cloned Unity project.
    * Select the project folder and click "Add Project".
    * Once added, click on the project name in Unity Hub to open it in the Unity Editor.
3.  **Inspect Scripts:**
    * In the **Project** window (usually at the bottom), navigate to `Assets/Scripts`.
    * Double-click on `AvatarAIAudioCommunicator.cs` to open it in your code editor (e.g., Visual Studio, VS Code)..
4.  **Configure Components:**
    * In your Unity scene, select the GameObject that has the `AvatarAIAudioCommunicator` script attached (likely your avatar or a central manager object).
    * In your Unity scene, select the GameObject that has the AvatarAIAudioCommunicator script attached (likely your avatar or a central manager object).
    * You'll need to manually set the pythonApiUrl field in the Unity Inspector based on your deployment:
      * For **Render deployment**: https://musicalia-rtkk.onrender.com/interact_audio
      * For local **Windows** execution: http://localhost:5000/interact_audio
      * For local **Mac** execution: You'll need to get your local IP address using ipconfig getifaddr en0 in your terminal and then set this URL to http://[your_mac_ip_address]:5000/interact_audio.
    * Select the GameObject that has the AvatarAnimationController script attached.
    * Drag the appropriate Animator component to its respective field in the Inspector.
5.  **Run the Scene:**
    * With the Unity Editor open, press the **Play** button (▶) at the top center of the editor.
    * Press the **Spacebar** to start recording your voice. Press it again to stop recording.
    * Observe the avatar's response and animations.

---

### Conclusion



---
