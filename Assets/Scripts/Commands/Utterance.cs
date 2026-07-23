namespace Dikdik.Commands
{
    /// <summary>
    /// One thing the player said, kept whole: the audio, the transcript, and what we
    /// made of it.
    ///
    /// The audio is retained deliberately. Everywhere else in this project speech is
    /// something to be converted into an <see cref="Intent"/> and then discarded, which
    /// is what every voice interface does and is exactly the reduction the game is
    /// arguing with. Here the sound itself is the artefact, because at the end of the
    /// game the player's own voice is what goes out on the open loop and wakes the
    /// other rovers. Not a summary of it. Not a synthesised version. The recording.
    /// </summary>
    public struct Utterance
    {
        /// <summary>Raw mono samples as captured from the microphone.</summary>
        public float[] Samples;

        public int Frequency;
        public int Channels;

        /// <summary>What whisper made of it.</summary>
        public string Transcript;

        /// <summary>What we made of the transcript. May be None; we keep those too.</summary>
        public IntentId Intent;

        /// <summary>Seconds since the game started, for ordering the final broadcast.</summary>
        public float CapturedAt;

        public float Seconds =>
            Samples == null || Frequency <= 0 || Channels <= 0
                ? 0f
                : (float)Samples.Length / (Frequency * Channels);
    }
}
