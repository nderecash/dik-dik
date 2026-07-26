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
                // The plain agreements at the end are here because the rover asks
                // questions now. When it stops after a long stretch and says "keep
                // going?", the natural answer is "yes", and a rover that does not
                // understand the answer to its own question would be a poor advert
                // for this project.
                [IntentId.Go] = new[]
                {
                    "yes keep going", "start moving", "keep moving", "go forward",
                    "move forward", "lets continue", "keep going", "get going",
                    "carry on", "go ahead", "affirmative", "continue", "go on",
                    "correct", "start", "move", "forward", "go", "walk", "drive",
                    "yes", "yeah", "yep"
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
                    "wake them up", "wake up", "wake them", "wake"
                },

                // ----------------------------------------------------------
                // Delight. See IntentId for why these exist.
                //
                // "hello" and "hey" used to belong to Wake, back when the ending woke a
                // field of dormant rovers. That ending is gone and greeting the rover is
                // now just greeting the rover, which is what those words meant anyway.
                // ----------------------------------------------------------
                [IntentId.Jump] = new[]
                {
                    "can you jump", "do a jump", "jump up", "hop", "jump", "leap"
                },

                [IntentId.Spin] = new[]
                {
                    "spin around", "turn around in a circle", "do a spin",
                    "spin", "twirl", "pirouette"
                },

                [IntentId.Dance] = new[]
                {
                    "do a dance", "have a dance", "dance for me", "dance", "boogie", "wiggle"
                },

                [IntentId.Greet] = new[]
                {
                    "how are you", "good morning", "nice to meet you",
                    "hello there", "hello", "hi there", "hey", "hi"
                },

                [IntentId.Who] = new[]
                {
                    "who are you", "what is your name", "whats your name",
                    "who am i talking to", "who is this"
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

        // ------------------------------------------------------------------
        // Words the player taught the rover, on top of the built-in ones.
        //
        // This is the voice half of "allow controls to be remapped". A keyboard player
        // rebinds a key; a voice player teaches Salty a phrase, and from then on the
        // rover answers to their word. It is also the loop this whole project has been
        // running on the player's behalf, now handed to them: every built-in phrase
        // above got there because someone said it and was not understood. This lets the
        // player close that gap themselves.
        //
        // Kept pure in-memory here, with no PlayerPrefs, so this file stays free of
        // UnityEngine and can still be unit-tested with plain dotnet. Persistence lives
        // in a Unity-only wrapper that saves on Changed and loads via Deserialize.
        // ------------------------------------------------------------------
        private static readonly Dictionary<IntentId, List<string>> Taught =
            new Dictionary<IntentId, List<string>>();

        /// <summary>Raised whenever the taught set changes, so a persistence layer can save.</summary>
        public static event System.Action Changed;

        /// <summary>Teach the rover a new phrase for a command.</summary>
        public static void Teach(IntentId id, string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return;

            var clean = phrase.Trim().ToLowerInvariant();

            if (!Taught.TryGetValue(id, out var list))
            {
                list = new List<string>();
                Taught[id] = list;
            }

            if (!list.Contains(clean))
            {
                list.Add(clean);
                Changed?.Invoke();
            }
        }

        /// <summary>Phrases the player taught for a command. Empty if none.</summary>
        public static IReadOnlyList<string> TaughtFor(IntentId id)
        {
            return Taught.TryGetValue(id, out var list) ? list : System.Array.Empty<string>();
        }

        public static void ClearTaught()
        {
            Taught.Clear();
            Changed?.Invoke();
        }

        /// <summary>Serialize the taught set as "Intent=phrase" lines. Trivially inspectable.</summary>
        public static string Serialize()
        {
            var lines = new List<string>();
            foreach (var pair in Taught)
                foreach (var phrase in pair.Value)
                    lines.Add($"{pair.Key}={phrase}");

            return string.Join("\n", lines);
        }

        /// <summary>Replace the taught set from a serialized string. Raises Changed once.</summary>
        public static void Deserialize(string raw)
        {
            Taught.Clear();

            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var line in raw.Split('\n'))
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2)
                        continue;

                    if (System.Enum.TryParse(parts[0], out IntentId id) &&
                        !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        if (!Taught.TryGetValue(id, out var list))
                        {
                            list = new List<string>();
                            Taught[id] = list;
                        }

                        var clean = parts[1].Trim().ToLowerInvariant();
                        if (!list.Contains(clean))
                            list.Add(clean);
                    }
                }
            }

            Changed?.Invoke();
        }
    }
}
