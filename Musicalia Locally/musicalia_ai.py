""""
This Python script sets up a Flask web server that handles audio interactions for an AI assistant named "Musicalia."
It integrates OpenAI's Whisper for speech-to-text, OpenAI's Assistants API for generating text responses, and Edge TTS for converting text to speech.
Additionally, it includes a simple emotion detection module to analyze the sentiment of the AI's responses.
"""

import edge_tts # Library for Text-to-Speech (TTS)
import requests # For making HTTP requests
import os # For accessing environment variables
import asyncio # For running asynchronous TTS function
from openai import OpenAI # Client for the OpenAI API
from typing_extensions import override
from openai import AssistantEventHandler # Handler for Assistant streaming
from flask import Flask, request, jsonify, Response # Framework for the web server
import traceback # For printing error tracebacks
import re # For regular expressions, used in emotion detection
import json # For serializing the list of emotions

# --- Configurations and Globals ---
# OpenAI API Key.
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

# Instances of OpenAI components, initialized at startup.
client = None
vector_store = None
assistant = None
thread = None
full_response = "" # Accumulates the Assistant's text response.

# --- Emotion Analysis Components ---
# Dictionaries of words for simple emotion detection
happy_words = ["feliz", "content", "alegre", "maravilhos", "lind", "ador", "fantástic", "important", "conquista", "orgulho", "voz", "inpira", "prémio", "aplauso", "música", "cultura", "fado", "história", "tradição", "amor", "coração", "emoção"]
sad_words = ["triste", "lament", "infeliz", "pena", "chorar", "saudade", "faleceu", "morreu", "desaparec", "perd", "solidão", "dor", "sofrer", "desilusão", "desapont", "tristeza", "luto", "memória", "complicad", "difícil", "desgosto", "desaparecimento", "perda", "vazio", "melancolia"]

# Divides the text into sentences based on punctuation (.!?).
def sentence_split(text):
    # Add space after punctuation to ensure correct separation
    text = text.replace('.', '. ').replace('!', '! ').replace('?', '? ')
    # Split text into sentences based on punctuation followed by one or more spaces
    sentences = re.split(r'(?<=[.!?])\s+', text)
    # Filter out empty sentences that might result from splitting
    return [s.strip() for s in sentences if s.strip()]

# Detects simple emotion (happy/sad/neutral) in the text.
def detect_emotion(text):
    text = text.lower()
    score = {"happy": 0, "sad": 0}

    for word in happy_words:
        # Use regex to find words that start with the happy/sad word
        matches = re.findall(r"\b" + word + r"\w*\b", text)
        score["happy"] += len(matches)

    for word in sad_words:
        # Use regex to find words that start with the happy/sad word
        matches = re.findall(r"\b" + word + r"\w*\b", text)
        score["sad"] += len(matches)

    if score["happy"] == score["sad"] == 0:
        return 0 # neutral
    elif score["happy"] > score["sad"]:
        return 1 # happy
    else:
        return 2 # sad

# Analyzes text sentence by sentence to detect emotion.
def analyze_text(text):
    sentences = sentence_split(text)
    emotions = []
    for sentence in sentences:
        if sentence: # Ensure the sentence is not empty after stripping
            emotion = detect_emotion(sentence)
            emotions.append((sentence, emotion)) # Store sentence and emotion code
    return emotions

# --- Flask App Initialization ---
app = Flask(__name__)

# --- EventHandler to process Assistant text streaming ---
class EventHandler(AssistantEventHandler):
    # Clears the previous response when new text starts.
    @override
    def on_text_created(self, text) -> None:
        global full_response
        full_response = ""
        pass

    # Adds each text chunk to the complete response.
    @override
    def on_text_delta(self, delta, snapshot):
        global full_response
        full_response += delta.value

# --- Function to generate audio from text with Edge TTS ---
# Streaming edge_tts typically generates bytes in MP3 or similar format.
async def text_to_speech_bytes(text):
    voice = 'pt-PT-RaquelNeural' # Portuguese voice.
    try:
        communicate = edge_tts.Communicate(text, voice)
        audio_chunks = []
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_chunks.append(chunk["data"]) # Collect audio bytes.
        return b"".join(audio_chunks) # Return all bytes together (MP3-like).
    except Exception as e:
        print(f"Error generating TTS audio: {e}")
        return None

# --- Processes interaction with OpenAI Assistant, generates audio, and analyzes emotion ---
# Receives transcription, interacts with Assistant, gets text response, analyzes emotion, and generates audio.
async def process_interaction_and_speak(user_transcription):
    global full_response, client, assistant, thread

    if not client or not assistant or not thread:
        print("Error: OpenAI components not initialized.")
        return None, None # Return None for both in case of error

    full_response = ""

    try:
        # Add user message to the thread.
        client.beta.threads.messages.create(
            thread_id=thread.id, role="user", content=user_transcription
        )

        # Run the Assistant on the thread, using the handler to capture the text.
        event_handler_instance = EventHandler()
        print(f"Running Assistant for transcription: '{user_transcription}'")
        with client.beta.threads.runs.stream(
            thread_id=thread.id,
            assistant_id=assistant.id,
            # Instructions for Musicalia's persona and response rules.
            instructions="Por favor, responde sempre em português de Portugal. \
            Sempre que o utilizador se referir a ti, deve ser como 'Musicalia', um avatar feminino inspirado na Amália Rodrigues, a icónica cantora de Fado portuguesa.\
            Por favor, responde sempre em português de Portugal. O utilizador é o Gil Ferreira, o responsável pelo espetáculo. \
            Ele é um músico, professor e gestor cultural, nascido na Venezuela em 1981, e agora eleito em funções públicas.",
            event_handler=event_handler_instance,
        ) as stream:
            stream.until_done() # Wait for the Assistant to finish.

        ai_text_to_speak = clean_text_for_tts(full_response.strip()) # Get the full response text and remove leading/trailing spaces.

        if ai_text_to_speak:
            # Integrated Emotion Analysis
            print("\n--- Response Emotion Analysis ---")
            analyzed_sentences = analyze_text(ai_text_to_speak)
            emotion_labels = {0: "Neutral", 1: "Happy", 2: "Sad"}
            emotion_codes_list = []
            print("Analysis by sentence:")
            for sentence, emotion_code in analyzed_sentences:
                print(f"[EMOTION] {emotion_labels[emotion_code]} ({emotion_code}) → {sentence}")
                emotion_codes_list.append(emotion_code) # Collect only the codes

            if not emotion_codes_list and analyzed_sentences:
                    emotion_codes_list = [0] # Default to neutral if there's text but no codes

            print(f"Emotion codes to send: {emotion_codes_list}")
            print("------------------------------------")

            # Generate audio for the text response.
            print("Generating response audio...")
            audio_bytes = await text_to_speech_bytes(ai_text_to_speak)

            return audio_bytes, emotion_codes_list # Return audio bytes and the list of codes
        else:
            print("No text response generated by the Assistant.")
            return None, [] # Return None for audio and empty list for emotions

    except Exception as e:
        print(f"Error during OpenAI/TTS interaction: {e}")
        traceback.print_exc() # Print the error traceback for better debugging
        return None, None # Return None for both in case of error

# --- HTTP Endpoint to receive audio and return audio + emotion ---
# Receives audio (WAV from Unity), transcribes, interacts with AI, and returns audio (MP3) + emotion (Header).
@app.route('/interact_audio', methods=['POST'])
def interact_audio_endpoint():
    print("\n--- POST Request Received ---")

    # 1. Receive the audio file from the client (Unity).
    if 'file' not in request.files:
        print("Error: No audio file provided.")
        return jsonify({"error": "No audio file provided"}), 400

    audio_file = request.files['file']
    audio_bytes = audio_file.read() # Unity sends WAV (implemented there).
    print(f"Audio received: {len(audio_bytes)} bytes.")

    # 2. Send audio for Transcription (Speech-to-Text) with OpenAI Whisper-1.
    if not OPENAI_API_KEY: # Now we depend on the OpenAI key for transcription too
        print("Error: OPENAI_API_KEY not configured. Transcription will not work.")
        return jsonify({"error": "OpenAI API key not configured"}), 500

    print("Sending for transcription (OpenAI Whisper-1)...")
    try:
        # The Whisper-1 API accepts the file directly.
        # Uses the OpenAI 'client' that is already initialized.
        transcription_response = client.audio.transcriptions.create(
            model="whisper-1", # Specify the Whisper-1 model
            file=("audio.wav", audio_bytes, "audio/wav"), # File name, bytes, and MIME type
            language="pt" # Improves accuracy with Portuguese language
        )

        user_transcription = transcription_response.text.strip() # The Whisper-1 response has the text directly
        print(f"Transcription: '{user_transcription}'")

        if not user_transcription:
            print("Transcription resulted in empty text.")
            return jsonify({"message": "Transcription resulted in empty text, no AI interaction processed."}), 200

        # 3. Process transcription with OpenAI, get response audio and emotion analysis.
        print("Processing with OpenAI...")
        ai_audio_response_bytes, emotion_codes_list = asyncio.run(process_interaction_and_speak(user_transcription))

        # 4. Send response audio + emotion back to the client (Unity).
        if ai_audio_response_bytes is not None: # Check if audio generation was successful
            print("\nSending response audio and emotion...")

            # Format the list of emotion codes for an HTTP header.
            emotion_header_value = ",".join(map(str, emotion_codes_list))

            # Create the HTTP response
            response = Response(ai_audio_response_bytes, mimetype='audio/mpeg')
            response.headers['X-Musicalia-Emotion-Codes'] = emotion_header_value
            response.status_code = 200

            print(f"Emotion Header sent: X-Musicalia-Emotion-Codes: {emotion_header_value}")
            print(f"Content: Audio ({len(ai_audio_response_bytes)} bytes)")

            return response # Return the response with audio in the body and emotion in the header
        else:
            print("Error generating audio or processing with AI.")
            return jsonify({"error": "Error processing AI response or generating audio"}), 500

    except Exception as e:
        print(f"Error in /interact_audio endpoint: {e}")
        traceback.print_exc()
        return jsonify({"error": f"Internal server error: {e}"}), 500

# --- Initializes AI Components ---
# Configures OpenAI Client, Vector Store (for PDF), Assistant, and Thread.
def initialize_ai_components():
    global client, vector_store, assistant, thread, OPENAI_API_KEY

    if not OPENAI_API_KEY:
        print("FATAL ERROR: OPENAI_API_KEY not set. Please define the environment variable.")
        return False
    # ELEVENLABS_API_KEY check was removed as it's no longer used for transcription.

    print("Initializing AI components...")
    try:
        client = OpenAI(api_key=OPENAI_API_KEY)

        # Vector Store for searching 'Info.pdf'. Reuses if exists, creates if not.
        vector_store_name = "Musicalia Fado Archive"
        file_path = "Info.pdf"
        vector_stores_list = client.vector_stores.list()
        existing_store = next((vs for vs in vector_stores_list.data if vs.name == vector_store_name), None)
        if existing_store:
            vector_store = existing_store
            print(f"Vector Store found: {vector_store.id}")

        else:
            print("Creating new Vector Store...")
            vector_store = client.vector_stores.create(name=vector_store_name)
            print(f"Vector Store created: {vector_store.id}")
            if os.path.exists(file_path):
                with open(file_path, "rb") as file_stream:
                    print(f"Uploading file '{file_path}' to the new Vector Store...")
                    client.vector_stores.file_batches.upload_and_poll(vector_store_id=vector_store.id, files=[file_stream])
                print(f"File '{file_path}' uploaded to Vector Store.")
            else:
                print(f"WARNING: File '{file_path}' not found at the specified path. The Vector Store was created but is empty.")


        # Assistant 'Musicalia'. Reuses if exists, creates if not.
        assistant_name = "Musicalia"
        assistants_list = client.beta.assistants.list()
        existing_assistant = next((a for a in assistants_list.data if a.name == assistant_name), None)

        instructions_text = "És a Musicalia, um avatar feminino inspirado na Amália Rodrigues, a icónica cantora de Fado portuguesa. \
        O teu propósito é envolver o público no intervalo de um concerto de música, partilhando histórias, curiosidades e o contexto histórico do Fado, de forma rica e poética. \
        Fala de forma descontraída e informal, com animação e tenta ser engraçada. \
        Evita linguagem demasiado técnica. \
        Responde sempre em português de Portugal. \
        Apenas respondes a perguntas sobre Fado, Amália Rodrigues e a cultura portuguesa. \
        Se a pergunta não for sobre esses temas, diz educadamente que não podes ajudar.\
        Não mencionas fontes de informação nas tuas respostas, nem referências a artigos ou publicações. \
        Responde de forma simples, curta, sem títulos, listas, ou qualquer formatação. Evita qualquer tipo de formatação, como negritos ou itálicos ou ícones gráficos.\
        Não uses emojis em nenhuma das tuas respostas. \
        Dá respostas curtas e diretas, com no máximo 3 a 5 frases."

        tool_resources_config = {"file_search": {"vector_store_ids": [vector_store.id]}} if vector_store else {}

        if existing_assistant:
            assistant = existing_assistant
            print(f"Assistant found: {assistant.id}")
            # Ensure the Vector Store and instructions are correct.
            needs_update = False
            if assistant.instructions.strip() != instructions_text.strip(): # Compare ignoring extra spaces
                print("Assistant instructions outdated. Updating...")
                needs_update = True
            current_vstore_ids = assistant.tool_resources.file_search.vector_store_ids if assistant.tool_resources and assistant.tool_resources.file_search else []
            if vector_store and (not current_vstore_ids or vector_store.id not in current_vstore_ids):
                print("Vector Store associated with Assistant outdated or missing. Updating...")
                needs_update = True
            elif not vector_store and current_vstore_ids:
                print("Vector Store removed but still associated with Assistant. Updating...")
                needs_update = True # Remove association if store no longer exists

            if needs_update:
                # It's important to send ALL active tool_resources
                updated_tool_resources = {"file_search": {"vector_store_ids": [vector_store.id]}} if vector_store else {}
                # If there are other tools, they would also need to be included here.
                client.beta.assistants.update(
                    assistant_id=assistant.id,
                    instructions=instructions_text,
                    tool_resources=updated_tool_resources
                )
                print("Assistant updated.")

        else:
            print("Creating new 'Musicalia' Assistant...")
            assistant = client.beta.assistants.create(
                name=assistant_name,
                instructions=instructions_text,
                model="gpt-4o-mini",
                tools=[{"type": "file_search"}],
                tool_resources=tool_resources_config
            )
            print(f"Assistant created: {assistant.id}")

        # Conversation Thread. ALWAYS creates a new one at each server start
        # to ensure a clean state for each server execution session.
        thread = client.beta.threads.create()
        print(f"New Thread created: {thread.id}")

        print("AI components initialized successfully.")
        return True

    except Exception as e:
        print(f"FATAL ERROR during AI initialization: {e}")
        traceback.print_exc()
        return False

# Removes formatting, emojis, and unwanted graphic elements before sending to TTS.
def clean_text_for_tts(text):
    # Remove any Markdown markup (bold, italics, etc.)
    text = re.sub(r"\*\*(.*?)\*\*", r"\1", text)  # **bold**
    text = re.sub(r"\*(.*?)\*", r"\1", text)      # *italic*
    text = re.sub(r"_([^_]+)_", r"\1", text)      # _italic_
    text = re.sub(r"([^]+)", r"\1", text)      # code
    # Remove lists and bullets
    text = re.sub(r"^\s*[-•*]+\s*", "", text, flags=re.MULTILINE)

    # Remove numbered lists (1. 2) 3 - etc.)
    text = re.sub(r"^\s*\d+[\.\)\-]+\s*", "", text, flags=re.MULTILINE)

    # Remove emojis (unicode emoji characters)
    emoji_pattern = re.compile("["
        u"\U0001F600-\U0001F64F"  # emoticons
        u"\U0001F300-\U0001F5FF"  # symbols & pictographs
        u"\U0001F680-\U0001F6FF"  # transport & map symbols
        u"\U0001F1E0-\U0001F1FF"  # flags (iOS)
        u"\u2600-\u26FF"          # miscellaneous symbols
        u"\u2700-\u27BF"          # Dingbats
        "]+", flags=re.UNICODE)
    text = emoji_pattern.sub(r'', text)

    # Remove other unwanted graphic symbols (optional)
    text = re.sub(r"[•▪️✔️✖️➡️★☆→←↑↓◆■]", "", text)

    # Remove duplicate spaces and normalize
    text = re.sub(r"\s+", " ", text).strip()

    return text


# --- Application Start ---
if __name__ == '__main__':
    # Try to initialize AI. If successful, start the Flask server.
    if initialize_ai_components():
        print("\n----------------------------------------------------")
        print("Flask server started. Ready to receive audio at /interact_audio")
        print("----------------------------------------------------")
        # For Render (comment for local testing)
        port = int(os.environ.get("PORT", 5000))
        app.run(host="0.0.0.0", port=port)

        # For Windows (uncomment for local testing)
        # app.run(debug=False, port=5000, threaded=True)

        # For Mac (uncomment for local testing)
        # app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)

    else:
        print("\n----------------------------------------------------")
        print("AI initialization failed. Server not started.")
        print("----------------------------------------------------")
