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

        /// <summary>
        /// When the player <em>started</em> giving this command.
        ///
        /// <para>The second timestamp, and the one latency compensation runs on. For a key
        /// press it is the same instant as <see cref="CreatedAt"/>. For speech it is when
        /// voice detection first fired, roughly a tenth of a second after they opened their
        /// mouth, and typically half a second before they closed it.</para>
        ///
        /// <para><b>Why the difference matters.</b> Measuring from speech end quietly taxes
        /// voice by the length of the utterance. Say "stop" starting at T, finish at T+0.5,
        /// and the command lands at T+3.1 while a key pressed at T lands at T+2.6. The
        /// decision was made at T in both cases; only one of them was charged for the time
        /// it took to express it.</para>
        ///
        /// <para>The 2.6 second transport delay is deliberate and stays. The extra half
        /// second was an artefact of measuring from the wrong end.</para>
        /// </summary>
        public readonly float StartedAt;

        public Intent(IntentId id, CommandSource source, string rawText,
                      float confidence = 1f, float createdAt = 0f, float startedAt = -1f)
        {
            Id = id;
            Source = source;
            RawText = string.IsNullOrEmpty(rawText) ? string.Empty : rawText;
            Confidence = confidence;
            CreatedAt = createdAt;

            // Default to the end stamp when no onset is known, so anything that has not
            // been taught about onsets behaves exactly as it did before.
            StartedAt = startedAt < 0f ? createdAt : startedAt;
        }

        /// <summary>
        /// How long the player spent giving this command. Zero for a key press.
        ///
        /// <para>Written without Mathf on purpose. This file is linked into the matcher test
        /// project, which compiles with plain dotnet and has no UnityEngine reference, and
        /// that is the whole reason the matcher can be tested in a second.</para>
        /// </summary>
        public float ExpressionSeconds => CreatedAt > StartedAt ? CreatedAt - StartedAt : 0f;

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
            new Intent(Id, Source, RawText, Confidence, createdAt, createdAt);

        /// <summary>
        /// The same command, stamped with when the player started and finished giving it.
        /// </summary>
        public Intent At(float startedAt, float createdAt) =>
            new Intent(Id, Source, RawText, Confidence, createdAt, startedAt);

        /// <summary>An unrecognised command. We still keep the raw text so we can show it back.</summary>
        public static Intent Unrecognised(CommandSource source, string rawText, float createdAt = 0f,
                                          float startedAt = -1f) =>
            new Intent(IntentId.None, source, rawText, 0f, createdAt, startedAt);

        public override string ToString() =>
            $"{Id} (from {Source}, \"{RawText}\", confidence {Confidence:0.00})";
    }
}
