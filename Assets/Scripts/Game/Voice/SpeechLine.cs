using UnityEngine;

namespace Dikdik.Game.Voice
{
    /// <summary>
    /// One thing somebody says: the audio, the exact words, who said it, and how much it
    /// matters.
    ///
    /// <para>The caption travels with the clip rather than being looked up separately.
    /// That is the whole reason subtitles cannot drift out of sync with audio here: there
    /// is no path that plays one without the other, because they are the same object.</para>
    /// </summary>
    public struct SpeechLine
    {
        public AudioClip Clip;

        /// <summary>The words, verbatim. Shown for the length of the clip.</summary>
        public string Caption;

        public Speaker Speaker;
        public SpeechPriority Priority;

        /// <summary>
        /// Delay this rather than dropping it when something is already speaking.
        ///
        /// For lines that carry information the player cannot get anywhere else: a scan
        /// report, the station's jargon, the fault. An acknowledgement is not essential
        /// because the console and the rover's light already said it.
        /// </summary>
        public bool Essential;

        /// <summary>
        /// Extra seconds to keep the microphone shut after the clip ends, so the tail of
        /// the line does not get transcribed as if the player said it.
        /// </summary>
        public float TailSeconds;

        /// <summary>Breath before whatever is queued behind this one.</summary>
        public float GapAfter;

        public static SpeechLine Make(AudioClip clip, string caption, Speaker speaker,
                                      SpeechPriority priority, bool essential = false)
        {
            return new SpeechLine
            {
                Clip = clip,
                Caption = caption,
                Speaker = speaker,
                Priority = priority,
                Essential = essential,
                TailSeconds = 0.6f,
                GapAfter = 0.35f
            };
        }

        public float Seconds => Clip != null ? Clip.length : 0f;
    }
}
