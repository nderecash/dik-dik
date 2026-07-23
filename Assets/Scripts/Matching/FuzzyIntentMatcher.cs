using System;
using System.Collections.Generic;
using System.Text;
using Dikdik.Commands;

namespace Dikdik.Matching
{
    /// <summary>
    /// Turns whatever the player said into an <see cref="Intent"/>.
    ///
    /// This is the layer that makes speech recognition feel like it is listening
    /// rather than testing you. Whisper gives us a sentence, not a command. People
    /// say "okay go ahead now", "could you open the door", "uh, left I think".
    /// Matching only exact keywords would push the burden back onto the player,
    /// which is the behaviour this game exists to argue against.
    ///
    /// Deliberately plain and deterministic. Every decision it makes can be
    /// reproduced from the log, which matters when we tune it against real speech.
    /// </summary>
    public static class FuzzyIntentMatcher
    {
        /// <summary>Below this similarity we say we did not understand, rather than guess.</summary>
        public const float MinimumSimilarity = 0.75f;

        private const float ExactConfidence = 1.00f;
        private const float ContainsConfidence = 0.85f;

        public static Intent Match(string rawText, CommandSource source)
        {
            return Match(rawText, source, null);
        }

        /// <param name="allowed">
        /// Restrict matching to these intents. Levels use this so a command the
        /// rover cannot obey yet is reported as not understood rather than
        /// silently accepted and ignored.
        /// </param>
        public static Intent Match(string rawText, CommandSource source, IReadOnlyList<IntentId> allowed)
        {
            // Whisper narrates what it could not transcribe: [BLANK_AUDIO], [Music],
            // [ Silence ], [ Grunts ]. Those are the model describing an absence, not
            // the player saying anything, and they must never reach a player facing
            // panel. "I heard: [BLANK_AUDIO]" is the interface blaming someone for
            // its own silence.
            var speech = StripAnnotations(rawText);

            var normalised = Normalise(speech);
            if (normalised.Length == 0)
                return Intent.Unrecognised(source, rawText);

            // Doing the opposite of what someone asked is the worst failure available
            // to us. "don't stop" must never be heard as "stop". When the sentence is
            // negated we say we did not understand and let them try again.
            if (IsNegated(normalised))
                return Intent.Unrecognised(source, rawText);

            // "all right" is agreement, not a turn. Strip the polite scaffolding people
            // wrap commands in before we go looking for command words inside it.
            normalised = StripFiller(normalised);
            if (normalised.Length == 0)
                return Intent.Unrecognised(source, rawText);

            var bestIntent = IntentId.None;
            var bestConfidence = 0f;
            var bestPhraseLength = 0;

            foreach (var pair in IntentVocabulary.Phrases)
            {
                if (allowed != null && !Contains(allowed, pair.Key))
                    continue;

                foreach (var phrase in pair.Value)
                {
                    var confidence = Score(normalised, phrase);
                    if (confidence <= 0f)
                        continue;

                    // On a tie, the longer phrase wins, because it is the more specific
                    // reading. "Go right" contains both "go" and "right" at identical
                    // confidence; the speaker meant the direction, and the direction is
                    // the longer match. Previously whichever intent happened to be
                    // declared first in the dictionary won, which is not a decision so
                    // much as an accident of ordering.
                    var better = confidence > bestConfidence ||
                                 (confidence == bestConfidence && phrase.Length > bestPhraseLength);

                    if (!better)
                        continue;

                    bestConfidence = confidence;
                    bestPhraseLength = phrase.Length;
                    bestIntent = pair.Key;
                }
            }

            if (bestIntent == IntentId.None || bestConfidence < MinimumSimilarity)
                return Intent.Unrecognised(source, rawText);

            return new Intent(bestIntent, source, rawText, bestConfidence);
        }

        private static float Score(string normalised, string phrase)
        {
            if (normalised == phrase)
                return ExactConfidence;

            if (ContainsWholePhrase(normalised, phrase))
                return ContainsConfidence;

            // Edit distance is allowed only between two single words.
            //
            // Turned loose on whole sentences it produces confident nonsense. In a real
            // session "start moving" scored 0.750 against "stop moving" and the rover was
            // told to halt when the player had asked it to set off. Three edits apart,
            // exactly opposite meanings, and just over the threshold.
            //
            // Restricting it to single words keeps every case it earns its place on
            // ("wright" for right, "stopp" for stop) and removes the entire class of
            // error where a sentence lands on its own opposite.
            if (normalised.IndexOf(' ') >= 0 || phrase.IndexOf(' ') >= 0)
                return 0f;

            var similarity = Similarity(normalised, phrase);
            return similarity >= MinimumSimilarity ? similarity : 0f;
        }

        /// <summary>
        /// Lowercase, drop punctuation, collapse runs of whitespace.
        /// Whisper punctuates its output, so "Open the door." must equal "open the door".
        /// </summary>
        public static string Normalise(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            var lastWasSpace = true;

            foreach (var raw in text)
            {
                var c = char.ToLowerInvariant(raw);

                // Apostrophes vanish rather than becoming spaces, so "don't" is one word.
                // Whisper writes contractions constantly and usually with a curly quote,
                // and splitting them broke negation detection on the commonest phrasing.
                if (IsApostrophe(c))
                    continue;

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Remove whisper's non-speech annotations: anything inside square brackets,
        /// parentheses, or asterisks. Observed in real transcripts as [BLANK_AUDIO],
        /// [Music], [MUSIC PLAYING], [ Silence ] and [ Grunts ].
        ///
        /// Note this keeps any real speech alongside them, so a transcript reading
        /// "[ Silence ] left" still resolves to Left. Only the annotation goes.
        /// </summary>
        public static string StripAnnotations(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            var depth = 0;
            var inAsterisks = false;

            foreach (var c in text)
            {
                // Asterisks come in pairs around a described action: *sighs*, *coughs*.
                // Toggling means an unmatched asterisk swallows the rest of the line,
                // which errs toward "I did not understand" rather than acting on a
                // half-read sentence. That is the safe direction to fail in.
                if (c == '*')
                {
                    inAsterisks = !inAsterisks;
                    continue;
                }

                if (c == '[' || c == '(')
                {
                    depth++;
                    continue;
                }

                if (c == ']' || c == ')')
                {
                    if (depth > 0) depth--;
                    continue;
                }

                if (depth == 0 && !inAsterisks)
                    builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// True when the transcript contains no actual speech, only whisper describing
        /// what it heard instead of words.
        ///
        /// The voice producer checks this and stays completely silent rather than
        /// reporting a failure. A player who has not spoken has not failed at anything,
        /// and telling them otherwise trains them to distrust the microphone.
        /// </summary>
        public static bool IsNonSpeech(string rawText)
        {
            return Normalise(StripAnnotations(rawText)).Length == 0;
        }

        /// <summary>
        /// True for the straight apostrophe and the typographic variants whisper emits.
        ///
        /// Compared by code point rather than written as literal characters, so this
        /// file stays pure ASCII and cannot be quietly corrupted by a toolchain that
        /// guesses the wrong encoding. Unity and dotnet do not always agree on that.
        /// </summary>
        public static bool IsApostrophe(char c)
        {
            return c == '\''             // U+0027 apostrophe
                || c == (char)0x2019     // right single quotation mark, whisper's usual choice
                || c == (char)0x2018     // left single quotation mark
                || c == (char)0x02BC;    // modifier letter apostrophe
        }

        /// <summary>
        /// Whole word containment, so "left" does not match inside "leftover"
        /// and "go" does not match inside "gone".
        /// </summary>
        private static bool ContainsWholePhrase(string haystack, string needle)
        {
            if (needle.Length == 0 || needle.Length > haystack.Length)
                return false;

            var index = 0;
            while (true)
            {
                index = haystack.IndexOf(needle, index, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                var startsClean = index == 0 || haystack[index - 1] == ' ';
                var endIndex = index + needle.Length;
                var endsClean = endIndex == haystack.Length || haystack[endIndex] == ' ';

                if (startsClean && endsClean)
                    return true;

                index += 1;
            }
        }

        /// <summary>1 means identical, 0 means nothing in common.</summary>
        public static float Similarity(string a, string b)
        {
            var longest = Math.Max(a.Length, b.Length);
            if (longest == 0)
                return 1f;

            return 1f - (float)LevenshteinDistance(a, b) / longest;
        }

        /// <summary>Standard two row edit distance. Kept here so the repo has no extra dependency.</summary>
        public static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;

                for (var j = 1; j <= b.Length; j++)
                {
                    var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    var insertion = current[j - 1] + 1;
                    var deletion = previous[j] + 1;

                    current[j] = Math.Min(substitution, Math.Min(insertion, deletion));
                }

                Array.Copy(current, previous, current.Length);
            }

            return previous[b.Length];
        }

        /// <summary>
        /// Words that flip the meaning of whatever follows them.
        ///
        /// "no" is deliberately not on this list. "No, stop" is an ordinary thing to
        /// shout at a rover heading for a hole, and treating it as negation would
        /// throw away the one command that matters most.
        /// </summary>
        private static readonly string[] Negations =
        {
            "not", "dont", "doesnt", "didnt", "cant", "cannot", "wont", "never"
        };

        /// <summary>
        /// Politeness and hesitation. People do not speak in command words, they say
        /// "could you please open the door" and "okay, go now". Longest first, so
        /// "could you please" is removed before "could you" can half-match it.
        /// </summary>
        private static readonly string[] Filler =
        {
            "i would like you to", "i want you to", "could you please", "would you please",
            "can you please", "could you", "would you", "can you",
            "all right", "alright", "please", "okay", "ok", "um", "uh", "erm",
            "now", "then", "just"
        };

        private static bool IsNegated(string normalised)
        {
            foreach (var negation in Negations)
                if (ContainsWholePhrase(normalised, negation))
                    return true;

            return false;
        }

        private static string StripFiller(string normalised)
        {
            var result = normalised;

            foreach (var filler in Filler)
            {
                result = RemoveWholePhrase(result, filler);
                if (result.Length == 0)
                    return string.Empty;
            }

            return result;
        }

        /// <summary>Remove every whole word occurrence of a phrase, then tidy the spaces.</summary>
        private static string RemoveWholePhrase(string haystack, string needle)
        {
            if (needle.Length == 0 || needle.Length > haystack.Length)
                return haystack;

            var index = 0;
            while (true)
            {
                index = haystack.IndexOf(needle, index, StringComparison.Ordinal);
                if (index < 0)
                    return haystack;

                var startsClean = index == 0 || haystack[index - 1] == ' ';
                var endIndex = index + needle.Length;
                var endsClean = endIndex == haystack.Length || haystack[endIndex] == ' ';

                if (startsClean && endsClean)
                {
                    // Normalise collapses the double space the removal leaves behind.
                    haystack = Normalise(haystack.Substring(0, index) + " " + haystack.Substring(endIndex));
                    if (haystack.Length == 0)
                        return string.Empty;

                    index = 0;
                }
                else
                {
                    index += 1;
                }
            }
        }

        private static bool Contains(IReadOnlyList<IntentId> list, IntentId id)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] == id)
                    return true;

            return false;
        }
    }
}
