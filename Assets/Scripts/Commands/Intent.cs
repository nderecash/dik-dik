namespace Dikdik.Commands
{
    /// <summary>
    /// One resolved command, plus enough context to explain it back to the player.
    ///
    /// RawText is what the player actually said, or the key they actually pressed.
    /// The feedback panel shows that, not our internal label, so the player always
    /// sees their own words reflected rather than our interpretation of them.
    /// </summary>
    public readonly struct Intent
    {
        public readonly IntentId Id;
        public readonly CommandSource Source;
        public readonly string RawText;
        public readonly float Confidence;

        /// <summary>
        /// When the player <em>finished giving</em> this command, in Time.time.
        ///
        /// Not when we worked out what it meant. For a key press those are the same
        /// instant. For speech they are nearly two seconds apart, because whisper has
        /// to run first, and that gap is exactly what the transport delay has to
        /// account for. Measuring from here is what lets voice and keyboard arrive at
        /// the rover at the same moment relative to the person, rather than the
        /// keyboard silently winning every race.
        /// </summary>
        public readonly float CreatedAt;

        public Intent(IntentId id, CommandSource source, string rawText,
                      float confidence = 1f, float createdAt = 0f)
        {
            Id = id;
            Source = source;
            RawText = string.IsNullOrEmpty(rawText) ? string.Empty : rawText;
            Confidence = confidence;
            CreatedAt = createdAt;
        }

        /// <summary>True when we worked out what the player wanted.</summary>
        public bool IsRecognised => Id != IntentId.None;

        /// <summary>
        /// The same command, stamped with when the player finished giving it.
        ///
        /// The matcher deliberately knows nothing about time; it is pure text in,
        /// meaning out, which is why it can be tested without Unity. Producers stamp
        /// the result on the way out, because only they know when the microphone
        /// actually closed or the key actually went down.
        /// </summary>
        public Intent At(float createdAt) =>
            new Intent(Id, Source, RawText, Confidence, createdAt);

        /// <summary>An unrecognised command. We still keep the raw text so we can show it back.</summary>
        public static Intent Unrecognised(CommandSource source, string rawText, float createdAt = 0f) =>
            new Intent(IntentId.None, source, rawText, 0f, createdAt);

        public override string ToString() =>
            $"{Id} (from {Source}, \"{RawText}\", confidence {Confidence:0.00})";
    }
}
