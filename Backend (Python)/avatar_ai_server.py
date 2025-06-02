"""
This Python script sets up a Flask web server that handles audio interactions for an AI assistant named "Musicalia."
It integrates OpenAI's Whisper for speech-to-text, OpenAI's Assistants API for generating text responses, and Edge TTS for converting text to speech.
Additionally, it includes a simple emotion detection module to analyze the sentiment of the AI's responses.
"""

import edge_tts # Library for Text-to-Speech (TTS)
# import requests # For making HTTP requests - No longer explicitly used in this modified version
import os # For accessing environment variables (mainly for PORT now)
import asyncio # For running asynchronous TTS function
from openai import OpenAI # Client for the OpenAI API
from typing_extensions import override
from openai import AssistantEventHandler # Handler for Assistant streaming
from flask import Flask, request, jsonify, Response # Framework for the web server
import traceback # For printing error tracebacks
import re # For regular expressions, used in emotion detection
import json # For serializing the list of emotions


# Instances of OpenAI components, initialized after receiving API key.
client = None
vector_store = None
assistant = None
thread = None
full_response = "" # Accumulates the Assistant's text response.
ai_initialized_successfully = False # Flag to track AI initialization status

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
async def process_interaction_and_speak(user_transcription):
    global full_response, client, assistant, thread, ai_initialized_successfully

    if not ai_initialized_successfully or not client or not assistant or not thread:
        print("Error: OpenAI components not initialized or initialization failed.")
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

            if not emotion_codes_list and analyzed_sentences: # If there is text but no codes (e.g. all neutral sentences from a single word response)
                    emotion_codes_list = [0] # Default to neutral
            elif not analyzed_sentences: # No sentences, implies no text to analyze
                emotion_codes_list = [0] # Default to neutral if text was empty or whitespace

            print(f"Emotion codes to send: {emotion_codes_list}")
            print("------------------------------------")

            # Generate audio for the text response.
            print("Generating response audio...")
            audio_bytes = await text_to_speech_bytes(ai_text_to_speak)

            return audio_bytes, emotion_codes_list # Return audio bytes and the list of codes
        else:
            print("No text response generated by the Assistant.")
            return None, [0] # Return None for audio and default neutral emotion

    except Exception as e:
        print(f"Error during OpenAI/TTS interaction: {e}")
        traceback.print_exc() # Print the error traceback for better debugging
        return None, None # Return None for both in case of error

# --- HTTP Endpoint to receive API Key and Initialize AI Components ---
@app.route('/initialize_ai', methods=['POST'])
def initialize_ai_endpoint():
    global ai_initialized_successfully, client
    print("\n--- POST Request Received to /initialize_ai ---")

    if ai_initialized_successfully and client is not None:
        print("AI components already initialized.")
        return jsonify({"message": "AI components already initialized"}), 200

    data = request.get_json()
    if not data or 'api_key' not in data:
        print("Error: API key not provided in request.")
        return jsonify({"error": "API key not provided"}), 400

    api_key = data['api_key']
    if not api_key.strip():
        print("Error: Received empty API key.")
        return jsonify({"error": "Empty API key received"}), 400

    print("Attempting to initialize AI components with provided API key...")
    if initialize_ai_components(api_key):
        ai_initialized_successfully = True
        print("AI components initialized successfully via /initialize_ai.")
        return jsonify({"message": "AI initialized successfully"}), 200
    else:
        ai_initialized_successfully = False
        print("Failed to initialize AI components via /initialize_ai.")
        return jsonify({"error": "AI initialization failed on server"}), 500

# --- HTTP Endpoint to receive audio and return audio + emotion ---
# Receives audio (WAV from Unity), transcribes, interacts with AI, and returns audio (MP3) + emotion (Header).
@app.route('/interact_audio', methods=['POST'])
def interact_audio_endpoint():
    global ai_initialized_successfully, client
    print("\n--- POST Request Received to /interact_audio ---")

    if not ai_initialized_successfully or not client:
        print("Error: AI components not initialized. Please send API key to /initialize_ai first.")
        return jsonify({"error": "AI not initialized. Send API key to /initialize_ai first."}), 403 # Forbidden

    # 1. Receive the audio file from the client (Unity).
    if 'file' not in request.files:
        print("Error: No audio file provided.")
        return jsonify({"error": "No audio file provided"}), 400

    audio_file = request.files['file']
    audio_bytes = audio_file.read() # Unity sends WAV (implemented there).
    print(f"Audio received: {len(audio_bytes)} bytes.")

    # 2. Send audio for Transcription (Speech-to-Text) with OpenAI Whisper-1.
    # client is already initialized with the API key here
    print("Sending for transcription (OpenAI Whisper-1)...")
    try:
        # The Whisper-1 API accepts the file directly.
        transcription_response = client.audio.transcriptions.create(
            model="whisper-1", # Specify the Whisper-1 model
            file=("audio.wav", audio_bytes, "audio/wav"), # File name, bytes, and MIME type
            language="pt" # Improves accuracy with Portuguese language
        )

        user_transcription = transcription_response.text.strip() # The Whisper-1 response has the text directly
        print(f"Transcription: '{user_transcription}'")

        if not user_transcription:
            print("Transcription resulted in empty text.")
            empty_response = Response(mimetype='audio/mpeg') # Empty audio
            empty_response.headers['X-Musicalia-Emotion-Codes'] = "0" # Neutral for empty
            empty_response.status_code = 200 # OK, but effectively no content
            print("Sending empty audio response for empty transcription.")
            return empty_response


        # 3. Process transcription with OpenAI, get response audio and emotion analysis.
        print("Processing with OpenAI...")
        ai_audio_response_bytes, emotion_codes_list = asyncio.run(process_interaction_and_speak(user_transcription))

        # 4. Send response audio + emotion back to the client (Unity).
        if ai_audio_response_bytes is not None: # Check if audio generation was successful
            print("\nSending response audio and emotion...")

            # Format the list of emotion codes for an HTTP header.
            emotion_header_value = ",".join(map(str, emotion_codes_list if emotion_codes_list else [0])) # Default to neutral if list is empty

            # Create the HTTP response
            response = Response(ai_audio_response_bytes, mimetype='audio/mpeg')
            response.headers['X-Musicalia-Emotion-Codes'] = emotion_header_value
            response.status_code = 200

            print(f"Emotion Header sent: X-Musicalia-Emotion-Codes: {emotion_header_value}")
            print(f"Content: Audio ({len(ai_audio_response_bytes)} bytes)")

            return response # Return the response with audio in the body and emotion in the header
        else:
            print("Error generating audio or processing with AI.")
            # Send a specific error code or fallback audio if designed
            error_response = Response(mimetype='audio/mpeg') # Could be silent or a pre-recorded error message
            error_response.headers['X-Musicalia-Emotion-Codes'] = "0" # Neutral on error
            error_response.status_code = 500 # Internal Server Error
            return error_response


    except Exception as e:
        print(f"Error in /interact_audio endpoint: {e}")
        traceback.print_exc()
        return jsonify({"error": f"Internal server error: {e}"}), 500

# --- Initializes AI Components ---
# Configures OpenAI Client, Vector Store (for PDF), Assistant, and Thread.
def initialize_ai_components(api_key):
    global client, vector_store, assistant, thread

    if not api_key:
        print("FATAL ERROR: API Key not provided for initialization.")
        return False

    print("Initializing AI components with provided API key...")
    try:
        client = OpenAI(api_key=api_key) # Use the provided API key

        # Verify API key validity by making a simple request (e.g., listing models)
        try:
            client.models.list()
            print("OpenAI API key verified.")
        except Exception as api_auth_error:
            print(f"FATAL ERROR: OpenAI API key seems invalid or connectivity issue: {api_auth_error}")
            client = None # Ensure client is not set if key is bad
            return False

        # Vector Store for searching 'Info.pdf'. Reuses if exists, creates if not.
        vector_store_name = "Musicalia Fado Archive"
        file_path = "Info.pdf" # Ensure this file is in the same directory as your script or provide full path

        # Check if file_path exists before attempting to use it
        if not os.path.exists(file_path):
            print(f"WARNING: File '{file_path}' not found.")

        vector_stores_list = client.vector_stores.list() # Use client.beta.vector_stores
        existing_store = next((vs for vs in vector_stores_list.data if vs.name == vector_store_name), None)

        if existing_store:
            vector_store = existing_store
            print(f"Vector Store found: {vector_store.id}")
            # Optional: Check if file needs re-uploading or updating (more complex logic)
        else:
            print("Creating new Vector Store...")
            vector_store_payload = {"name": vector_store_name}
            vector_store = client.vector_stores.create(**vector_store_payload) # Use client.beta.vector_stores
            print(f"Vector Store created: {vector_store.id}")

            if os.path.exists(file_path):
                try:
                    with open(file_path, "rb") as file_stream:
                        print(f"Uploading file '{file_path}' to Vector Store '{vector_store.id}'...")
                        # Correct way to upload file to a vector store
                        file_batch = client.vector_stores.file_batches.upload_and_poll(
                            vector_store_id=vector_store.id, files=[file_stream]
                        )
                        print(f"File batch status: {file_batch.status}")
                        if file_batch.status == "completed":
                             print(f"File '{file_path}' uploaded and processed for Vector Store.")
                        else:
                            print(f"File '{file_path}' upload to Vector Store failed or in unexpected state: {file_batch.status}")
                            # Potentially delete the vector store or handle this error appropriately
                except Exception as e:
                    print(f"Error uploading file to vector store: {e}")
                    # Decide on error handling, e.g., delete the newly created vector store
            else:
                print(f"WARNING: File '{file_path}' not found. Vector Store '{vector_store_name}' is empty.")


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

        tool_resources_config = {}
        if vector_store: # Only include file_search if vector_store was successfully created/found
            tool_resources_config = {"file_search": {"vector_store_ids": [vector_store.id]}}


        if existing_assistant:
            assistant = existing_assistant
            print(f"Assistant found: {assistant.id}")
            # Ensure the Vector Store and instructions are correct.
            needs_update = False
            if assistant.instructions.strip() != instructions_text.strip(): # Compare ignoring extra spaces
                print("Assistant instructions outdated. Updating...")
                needs_update = True

            current_vstore_ids = []
            if assistant.tool_resources and assistant.tool_resources.file_search:
                 current_vstore_ids = assistant.tool_resources.file_search.vector_store_ids or []

            if vector_store and vector_store.id not in current_vstore_ids:
                print("Vector Store associated with Assistant outdated or missing. Updating...")
                needs_update = True
            elif not vector_store and current_vstore_ids: # If we expect no vector store but one is associated
                print("Vector Store should not be associated with Assistant but is. Updating to remove.")
                needs_update = True
            elif vector_store and not current_vstore_ids and tool_resources_config: # Vector store exists, should be associated, but isn't
                 print("Vector Store should be associated with Assistant but is not. Updating.")
                 needs_update = True


            if needs_update:
                print("Updating assistant with new instructions and/or tool resources...")
                client.beta.assistants.update(
                    assistant_id=assistant.id,
                    instructions=instructions_text,
                    model="gpt-4o-mini", # Ensure model is specified during update if it could change
                    tools=[{"type": "file_search"}] if vector_store else [], # Only add file_search tool if VS exists
                    tool_resources=tool_resources_config if vector_store else {} # Pass empty if no VS
                )
                print("Assistant updated.")
        else:
            print("Creating new 'Musicalia' Assistant...")
            assistant = client.beta.assistants.create(
                name=assistant_name,
                instructions=instructions_text,
                model="gpt-4o-mini",
                tools=[{"type": "file_search"}] if vector_store else [], # Only add file_search tool if VS exists
                tool_resources=tool_resources_config if vector_store else {} # Pass empty if no VS
            )
            print(f"Assistant created: {assistant.id}")

        # Conversation Thread. ALWAYS creates a new one at each server start
        # to ensure a clean state for each server execution session. (Or upon successful initialization)
        if thread:
            print(f"Existing thread found: {thread.id}. Creating a new one for a clean session.")

        thread = client.beta.threads.create()
        print(f"New Thread created: {thread.id}")

        print("AI components initialized successfully.")
        return True

    except Exception as e:
        print(f"FATAL ERROR during AI initialization: {e}")
        traceback.print_exc()
        # Clean up partial initializations if necessary
        client = None
        vector_store = None
        assistant = None
        thread = None
        return False

# Removes formatting, emojis, and unwanted graphic elements before sending to TTS.
def clean_text_for_tts(text):
    # Remove any Markdown markup (bold, italics, etc.)
    text = re.sub(r"\*\*(.*?)\*\*", r"\1", text)  # **bold**
    text = re.sub(r"\*(.*?)\*", r"\1", text)      # *italic*
    text = re.sub(r"_([^_]+)_", r"\1", text)      # _italic_
    # text = re.sub(r"`([^`]+)`", r"\1", text) # Fixed: code backticks, ensure it's not greedy
    text = re.sub(r"`(.*?)`", r"\1", text)

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
        u"\uFE0F"                 # Variation selector, often part of emoji sequences
        "]+", flags=re.UNICODE)
    text = emoji_pattern.sub(r'', text)

    # Remove other unwanted graphic symbols (optional)
    text = re.sub(r"[•▪️✔️✖️➡️★☆→←↑↓◆■«»“”]", "", text) # Added more common symbols like quotes

    # Remove multiple newlines and replace with a single space
    text = re.sub(r"\n+", " ", text)

    # Remove duplicate spaces and normalize
    text = re.sub(r"\s+", " ", text).strip()

    return text


# --- Application Start ---
if __name__ == '__main__':
    # Initialization will be triggered by Unity via the /initialize_ai endpoint.
    print("\n----------------------------------------------------")
    print("Flask server started.")
    print("Waiting for API key from Unity at /initialize_ai to initialize AI components.")
    print("Once initialized, ready to receive audio at /interact_audio")
    print("----------------------------------------------------")

    # For Render (comment for local testing)
    port = int(os.environ.get("PORT", 5000))
    app.run(host="0.0.0.0", port=port) 

    # For Windows (uncomment for local testing)
    # app.run(debug=False, port=5000, threaded=True)

    # For Mac (uncomment for local testing)
    # app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)
