# SintonIA - Musicalia

This repository hosts **Musicalia**, an innovative AI-powered digital experience developed by **SintonIA**, a multidisciplinary team from the **University of Porto**.

Musicalia is a real-time, AI-driven avatar designed not just to speak, but to truly connect. She listens, detects emotion, and responds accordingly — both in speech and in animation.

In this MVP, tailored for our client, Gil ferreira, Musicalia helped bring the legacy of the fadista Amália Rodrigues into the digital age. Through real-time AI interactivity, emotion-aware responses, and dynamic animation, it supports Gil in redefining how we experience fado and cultural performance. 

Musicalia demonstrates what’s possible when technology doesn’t replace the artist, but rather performs **with** them.

While others offer static, pre-scripted videos or generic avatar models, we deliver:

- Real-time, adaptive avatars  
- Emotion & topic recognition  
- Live synchronization of animation and voice  
- Culturally grounded design  

The application runs seamlessly on a cloud-based server (currently [Render](https://render.com)), with support for **local execution** — enabling flexibility for development and testing.

![Animations](https://github.com/user-attachments/assets/ddaddb3c-9195-47a7-8d2c-b7e10ea6f3b4)

---

## Features (Use Cases)

Musicalia currently supports:

- Audio input via microphone  
- Emotion & topic analysis via AI  
- Text-To-Speech response generation  
- Real-time animation sync  
- Cloud deployment + local fallback  

---

## Running Musicalia

You have two main options for running Musicalia:  
1. **Using the standalone application (Windows & Mac)**  
2. **Running it locally for development**

#### Create Your OpenAI API Key

Before running Musicalia, you'll need an **OpenAI API key**. This key allows Musicalia to access OpenAI's powerful AI models for transcription, conversation, and more.

* Go to the [OpenAI API website](https://platform.openai.com/account/api-keys).
* **Sign up or log in** to your account.
* **First-time users**: If this is your first time using the OpenAI API, you might need to click **"Start building"** to initialize your account and add an Organization.
* Give your key a name (e.g., "Musicalia") and click "Generate API key."
* **Copy the key immediately!** You won't be able to see it again. Store it in a safe place.
* Once the key is generated, you'll need to **add API credits** to enable billing for API usage. Don't worry, the costs for an application like Musicalia are typically very low, often just a few dollars per month for usual consumption, well within the initial $5 minimum.

---

### Option 1: Standalone Application (Windows & Mac)


#### Download the Application

- **Windows**: Download [Musicalia_Windows.zip](https://github.com/marianaas8/SintonIA_Musicalia_MVP/releases/download/v1.0.0/Musicalia_Windows.zip)  
- **Mac**: Download [Musicalia_Mac.app.zip](https://github.com/marianaas8/SintonIA_Musicalia_MVP/releases/download/v1.0.0/Musicalia_Mac.app.zip) 

#### Extract the Files

-   **Windows**: Unzip to e.g., `C:\Musicalia`
-   **Mac**: Unzip the downloaded `.zip` file. This will create a `Musicalia.app` application file. You can then optionally drag and drop `Musicalia.app` to your Applications folder for easier access, or run it directly from where you unzipped it.

#### Add your OpenAI API key to your Environment

- **Windows**:
```cmd
setx OPENAI_API_KEY "your_api_key_here"
```

- **Mac**: 
```cmd
export OPENAI_API_KEY="your_api_key_here"
```

#### Run the Application

- **Windows**: Double-click `Musicalia Avatar.exe`  
- **Mac**: Double-click `Musicalia.app`

> **Note for macOS**: On first run, approve it under **System Settings > Privacy & Security** → "Open Anyway". Also grant **Microphone Access**.

#### Interact

- Allow a few initial seconds for the app to connect to Render.
- Press `Spacebar` to record audio.
- Press again to stop and send it to Musicalia.
- Observe real-time animated response.

> Be aware that there may be a short delay the first time Musicalia generates a response, as the "thinking" audio loads.

---

### Option 2: Run Locally for Development

This mode allows deeper customization and contributions.

**First, clone the repository:**
```bash
git clone [https://github.com/marianaas8/SintonIA_Musicalia_MVP.git](https://github.com/marianaas8/SintonIA_Musicalia_MVP.git)
cd SintonIA_Musicalia_MVP
```

### How it Works (Architecture Overview)

### Python Backend

Handles core AI logic, including:

- Speech-to-text transcription  
- AI response generation  
- Text-to-speech synthesis  
- Emotion detection

#### Key Components:

- **OpenAI API**:  
  - `Whisper-1` for transcription  
  - `GPT-4o Mini` for conversation

- **Edge TTS**:  
  Natural-sounding Portuguese voice output

- **Emotion Detection**:  
  Simple keyword-based (happy, sad, neutral)

- **Flask**:  
  Provides `/initialize_ai` and `/interact_audio` endpoints for Unity

- **Vector Store**:  
  Embeds and queries content from `Info.pdf` (about Fado & Amália Rodrigues)

#### Backend Flow

1. Unity sends the OpenAI API key to the `/initialize_ai` endpoint to set up AI components.
2. Unity sends recorded audio (WAV) to `/interact_audio`
3. `Whisper-1` transcribes it
4. `GPT-4o Mini` generates the reply
5. Text is analyzed for emotional tone
6. `Edge TTS` generates speech
7. Audio + emotion codes are returned to Unity

### Unity Frontend

Handles:

-   Audio recording and transmission
-   Playing AI-generated speech
-   Emotion-based real-time animation
-   Automated scene loading
-   Application exit functionality

#### Key Unity Components

-   **`AvatarAIAudioCommunicator.cs`**
    Sends/receives audio and emotion to/from Python backend, also responsible for initializing the AI system on the server.

-   **`AvatarAnimationController.cs`**
    Maps emotion codes to avatar animations.

-   **`LoadSceneAfterDelay.cs`**
    Loads a new Unity scene after a specified delay.

-   **`QuitOnEscape.cs`**
    Allows quitting the application or stopping Play mode in the Unity Editor by pressing the Escape key.

#### Unity Frontend Flow

1.  `LoadSceneAfterDelay.cs` transitions to the next scene after a set delay.
2.  `AvatarAIAudioCommunicator.cs` initializes AI by sending the API key to the backend.
3.  Upon AI readiness, Unity enables spacebar for audio recording.
4.  `AvatarAIAudioCommunicator.cs` records microphone input.
5.  Releasing spacebar sends recorded WAV to the backend's `/interact_audio` endpoint.
6.  "Thinking" audio plays while awaiting AI response.
7.  Backend returns MP3 audio and emotion codes to `AvatarAIAudioCommunicator.cs`.
8.  Received audio is played.
9.  Emotion codes are processed to determine the dominant emotion.
10. `OnEmotionDetected` event triggers `AvatarAnimationController.cs` for animation.
11. `OnTalkingStateChanged` event updates avatar's speaking/thinking state.
12. Communication errors trigger fallback audio.
13. `QuitOnEscape.cs` allows exiting the application with the Escape key.

### Setting Up the Python Backend

```bash
# Navigate to the Backend folder
cd Backend

# Install requirements
pip install -r requirements.txt
```

- Create a .env file in the Backend directory with your OpenAI API key:

```bash
# For macOS / Linux
export OPENAI_API_KEY="your_api_key_here"

# For Windows
setx OPENAI_API_KEY "your_api_key_here"

```

- Edit `avatar_ai_server.py` to run locally:

```python
# Uncomment the appropriate lines:

# For Windows:
# app.run(debug=False, port=5000, threaded=True)

# For macOS:
# app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)

# Comment the Render lines:

# port = int(os.environ.get("PORT", 5000))
# app.run(host="0.0.0.0", port=port)
```

- Run the server:

```bash
python avatar_ai_server.py
```

- The terminal will display output similar to `Running on http://0.0.0.0:5000/ (Press CTRL+C to quit)`, indicating the server is active. Keep this terminal window open. You'll also see the AI's responses and detected emotions printed here during interaction.

### Setting Up the Unity Frontend

1. **Install Unity Hub** + latest **Unity 2022.3 LTS or newer**
2.  **Open Project** via Unity Hub (select the `Frontend (Unity)` folder within your cloned repository).
3. **Inspect & Configure Scripts**

**AvatarAIAudioCommunicator.cs:**

- Located in: `Assets/Scripts`
- In the script file, uncomment the appropriate `pythonServerBaseUrl` line for your local setup and ensure only one is active:

        ```csharp
        public string pythonServerBaseUrl = "[https://musicalia-rtkk.onrender.com](https://musicalia-rtkk.onrender.com)"; // Default to Render
        // public string pythonServerBaseUrl = "http://localhost:5000"; // Uncomment for local Windows
        // public string pythonServerBaseUrl = "http://<your_ip>:5000"; // Uncomment for local macOS, replace <your_ip>
        ```  
- In Unity Inspector, set:

```plaintext
pythonServerBaseUrl:
- For Render:        https://musicalia-rtkk.onrender.com
- For Windows:       http://localhost:5000
- For macOS:         http://<your_ip>:5000
```

To get local IP on macOS:

```bash
ipconfig getifaddr en0
```

### Run in Unity

1. Press **Play** (`▶`) in Unity Editor  
2. Press **Spacebar** to record  
3. Press again to send  
4. Watch and listen to Musicalia’s reply and animation

---

## Conclusion

Musicalia stands as a unique fusion of tradition and technology. More than just a tool, she's a true creative partner, embodying our philosophy: **Humans Lead. Technology Follows.**

---

![Sintonia Logo](https://github.com/user-attachments/assets/387fb522-208e-48aa-a530-2248ce354432)

