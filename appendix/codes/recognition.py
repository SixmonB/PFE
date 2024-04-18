import pyaudio
import wave
import subprocess
import re
import os
import sys

# Parameters for audio recording
FORMAT = pyaudio.paInt16
CHANNELS = 1
RATE = 44100  # Sample rate (samples per second)
RECORD_SECONDS = 2  # Duration of the recording in seconds
OUTPUT_FILENAME = "recorded_audio.wav"

# Get the directory of the Python script
script_directory = os.path.dirname(os.path.abspath(__file__))

# Define the output file path based on the script directory
output_file_path = os.path.join(script_directory, OUTPUT_FILENAME)

# Initialize the audio stream
audio = pyaudio.PyAudio()

# Open a new audio stream for recording
stream = audio.open(format=FORMAT, channels=CHANNELS,
                    rate=RATE, input=True,
                    frames_per_buffer=1024)

frames = []

# Record audio data in chunks and save to frames list
for _ in range(0, int(RATE / 1024 * RECORD_SECONDS)):
    data = stream.read(1024)
    frames.append(data)

# Close and terminate the audio stream
stream.stop_stream()
stream.close()
audio.terminate()

# Save the recorded audio to a WAV file
with wave.open(output_file_path, 'wb') as wf:
    wf.setnchannels(CHANNELS)
    wf.setsampwidth(audio.get_sample_size(FORMAT))
    wf.setframerate(RATE)
    wf.writeframes(b''.join(frames))

# Run Whisper ASR processing on the recorded audio
whisper_command = f"whisper {output_file_path} --model base --language English"
process = subprocess.Popen(whisper_command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
stdout, stderr = process.communicate()

# Print the output from Whisper ASR
whisper_output = stdout.decode()
timestamp_pattern = r'\[\d+:\d+\.\d+ --> \d+:\d+\.\d+\]'
# Use regular expressions to remove timestamps
message = re.sub(timestamp_pattern, '', whisper_output).strip()
message = message.replace(".", "")

# Print the extracted message
print(message)
# Pass the transcribed message back to Unity
#sys.stdout.write(message)
#sys.stdout.flush()