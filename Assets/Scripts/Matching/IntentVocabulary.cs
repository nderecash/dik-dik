using System.Collections.Generic;
using Dikdik.Commands;

namespace Dikdik.Matching
{
    /// <summary>
    /// The words people actually use for each command.
    ///
    /// This is deliberately a plain C# file rather than a ScriptableObject.
    /// A ScriptableObject would be a binary-ish asset that reviewers cannot read
    /// in a diff. Every time someone says something the rover should have
    /// understood, the fix is one line here, and the commit shows exactly which
    /// human phrasing the game learned. That history is part of the point.
    /// </summary>
    public static class IntentVocabulary
    {
        /// <summary>
        /// Longest phrases first inside each list. The matcher checks phrases before
        /// single words, so "turn left" beats a stray "turn" hiding in the sentence.
        /// </summary>
        public static readonly IReadOnlyDictionary<IntentId, string[]> Phrases =
            new Dictionary<IntentId, string[]>
            {
                // "start moving", "lets continue" and "keep moving" came from a real
                // session. Their absence was not a small gap: without "start", the
                // sentence "start moving" fell through to edit distance and landed on
                // "stop moving", so asking the rover to set off halted it instead.
                [IntentId.Go] = new[]
                {
                    "start moving", "keep moving", "go forward", "move forward",
                    "lets continue", "keep going", "get going", "carry on",
                    "go ahead", "continue", "go on", "start", "move",
                    "forward", "go", "walk", "drive"
                },

                // "whoa" is how people actually stop a thing that is moving. It was
                // said five times in one breath during testing and understood as nothing.
                [IntentId.Stop] = new[]
                {
                    "stop moving", "hold still", "stay there", "stay put",
                    "hold on", "hold it", "hold up", "stop", "wait",
                    "halt", "freeze", "stay", "whoa", "woah"
                },

                // "go left" and "go right" must be listed here rather than left to the
                // tie-breaker alone. Both belt and braces: the phrase is longer, and it
                // is explicitly a direction, so neither ordering nor scoring can hand
                // "go right" to Go.
                [IntentId.Left] = new[]
                {
                    "turn to the left", "go to the left", "head to the left",
                    "on the left", "to the left", "turn left", "go left",
                    "head left", "look left", "left turn", "left"
                },

                [IntentId.Right] = new[]
                {
                    "turn to the right", "go to the right", "head to the right",
                    "on the right", "to the right", "turn right", "go right",
                    "head right", "look right", "right turn", "right"
                },

                [IntentId.Back] = new[]
                {
                    "go backwards", "go back", "back up", "reverse",
                    "turn around", "backwards", "back"
                },

                // "break the door" came from a session where whisper heard it as
                // "break the dough". The transcription was wrong, but the phrasing was
                // perfectly reasonable and we had no entry for it either way.
                [IntentId.Open] = new[]
                {
                    "open the door", "open the gate", "break the door",
                    "force the door", "break it open", "get it open",
                    "open it up", "open up", "open", "unlock"
                },

                [IntentId.Light] = new[]
                {
                    "turn on the light", "turn the light on", "switch on the light",
                    "lights on", "light up", "lamp", "light", "lights"
                },

                [IntentId.Wake] = new[]
                {
                    "wake them up", "wake up", "wake them", "hey", "hello", "wake"
                },

                // Repeat asks the rover to re-send its last message.
                // Restart runs the whole sim again. Bare "again" belongs to Restart,
                // so Repeat keeps only phrasings that clearly mean "say that once more".
                [IntentId.Repeat] = new[]
                {
                    "say that again", "say again", "one more time", "come again",
                    "repeat that", "repeat", "what was that", "what"
                },

                [IntentId.Restart] = new[]
                {
                    "run it again from the top", "lets run that again", "run that again",
                    "run it again", "start over", "from the top", "try again",
                    "reset the sim", "reset", "again"
                },

                [IntentId.Help] = new[]
                {
                    "what can i say", "what do i do", "i am stuck",
                    "help me", "help", "hint"
                }
            };

        /// <summary>
        /// The five intents the day 3 spike tests. Kept small on purpose:
        /// if recognition fails on five, it will not be rescued by adding more.
        /// </summary>
        public static readonly IntentId[] SpikeIntents =
        {
            IntentId.Go,
            IntentId.Stop,
            IntentId.Left,
            IntentId.Right,
            IntentId.Open
        };
    }
}
