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

        public Intent(IntentId id, CommandSource source, string rawText, float confidence = 1f)
        {
            Id = id;
            Source = source;
            RawText = string.IsNullOrEmpty(rawText) ? string.Empty : rawText;
            Confidence = confidence;
        }

        /// <summary>True when we worked out what the player wanted.</summary>
        public bool IsRecognised => Id != IntentId.None;

        /// <summary>An unrecognised command. We still keep the raw text so we can show it back.</summary>
        public static Intent Unrecognised(CommandSource source, string rawText) =>
            new Intent(IntentId.None, source, rawText, 0f);

        public override string ToString() =>
            $"{Id} (from {Source}, \"{RawText}\", confidence {Confidence:0.00})";
    }
}
