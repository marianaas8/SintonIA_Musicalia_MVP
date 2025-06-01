# SintonIA - Musicalia

This repository hosts **Musicalia**, an innovative AI-powered digital experience developed by **SintonIA**, a multidisciplinary team from the **University of Porto**.

Musicalia is a real-time, AI-driven avatar designed not just to speak, but to truly connect. She listens, detects emotion, and responds accordingly — both in speech and in animation. Whether on stage or in cultural spaces, Musicalia is more than a tool — she’s a creative partner.

Already tested in live performance, Musicalia demonstrates what’s possible when technology doesn’t replace the artist, but rather performs **with** them.

While others offer static, pre-scripted videos or generic avatar models, we deliver:

- Real-time, adaptive avatars  
- Emotion & topic recognition  
- Live synchronization of animation and voice  
- Culturally grounded design  

The application runs seamlessly on a cloud-based server (currently [Render](https://render.com)), with support for **local execution** — enabling flexibility for development and testing.

---

## Features

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

---

### Option 1: Standalone Application (Windows & Mac)

#### Download the Application

- **Windows**: Download `Musicalia_Windows.zip` from [link to download]  
- **Mac**: Download `Musicalia_Mac.zip` from [link to download]  

#### Extract the Files

- **Windows**: Unzip to e.g., `C:\Musicalia`  
- **Mac**: Unzip and optionally move `Musicalia.app` to Applications folder  

#### Run the Application

- **Windows**: Double-click `Musicalia.exe`  
- **Mac**: Double-click `Musicalia.app`

> **Note for macOS**: On first run, approve it under **System Settings > Privacy & Security** → "Open Anyway". Also grant **Microphone Access**.

#### Interact

- Press `Spacebar` to record audio  
- Press again to stop and send it to Musicalia  
- Observe real-time animated response  

---

### Option 2: Run Locally for Development

This mode allows deeper customization and contributions.

---

## How it Works (Architecture Overview)

### Python Backend

Handles core AI logic, including:

- Speech-to-text transcription  
- AI response generation  
- Text-to-speech synthesis  
- Emotion detection

**Key Components:**

- **OpenAI API**:  
  - `Whisper-1` for transcription  
  - `GPT-4o Mini` for conversation

- **Edge TTS**:  
  Natural-sounding Portuguese voice output

- **Emotion Detection**:  
  Simple keyword-based (happy, sad, neutral)

- **Flask**:  
  Provides `/interact_audio` endpoint for Unity

- **Vector Store**:  
  Embeds and queries content from `Info.pdf` (about Fado & Amália Rodrigues)

#### Backend Flow

1. Unity sends recorded audio (WAV) to `/interact_audio`
2. `Whisper-1` transcribes it
3. `GPT-4o Mini` generates the reply
4. Text is analyzed for emotional tone
5. `Edge TTS` generates speech
6. Audio + emotion codes are returned to Unity

---

### Unity Frontend

Handles:

- Audio recording and transmission  
- Playing AI-generated speech  
- Emotion-based real-time animation

#### Key Unity Components

- **`AvatarAIAudioCommunicator.cs`**  
  Sends/receives audio and emotion to/from Python backend

- **`AvatarAnimationController.cs`**  
  Maps emotion codes to avatar animations

---

## Setting Up the Python Backend

```bash
# Clone the repository
git clone [repository_url]

# Navigate to backend folder
cd Musicalia/Backend

# Install requirements
pip install -r requirements.txt
```

- Add your OpenAI API key to a `.env` file:

```bash
# In the same folder as avatar_ai_server.py
echo 'OPENAI_API_KEY="your_openai_api_key_here"' > .env
```

- Place `Info.pdf` (knowledge base) in the same directory as `avatar_ai_server.py`

- Edit `avatar_ai_server.py` to run locally:

```python
# Uncomment the appropriate lines:

# For Windows:
# app.run(debug=False, port=5000, threaded=True)

# For macOS:
# app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)
```

- Run the server:

```bash
python avatar_ai_server.py
```

---

## Setting Up the Unity Frontend

1. **Install Unity Hub** + latest **Unity 2022.3 LTS or newer**
2. **Open Project** via Unity Hub
3. **Inspect & Configure Scripts**

### AvatarAIAudioCommunicator.cs

- Located in: `Assets/Scripts`  
- In Unity Inspector, set:

```plaintext
pythonApiUrl:
- For Render:      https://musicalia-rtkk.onrender.com/interact_audio
- For Windows:     http://localhost:5000/interact_audio
- For macOS:       http://<your_ip>:5000/interact_audio
```

To get local IP on macOS:

```bash
ipconfig getifaddr en0
```

---

## Run in Unity

1. Press **Play** (`▶`) in Unity Editor  
2. Press **Spacebar** to record  
3. Press again to send  
4. Watch and listen to Musicalia’s reply and animation

---

## 🎤 Conclusion

Musicalia is a unique fusion of **tradition and technology**, bringing the legacy of **Amália Rodrigues** into the digital age. With real-time AI interactivity, emotion-aware responses, and dynamic animation, it redefines how we experience **Fado** and cultural performance.

---

**Humans Lead. Technology Follows.**
