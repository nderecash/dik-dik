using System.Collections;

namespace Dikdik.Game.Voice
{
    /// <summary>
    /// A receipt for something you asked to be said. Tells you when it finished, and
    /// whether it was actually spoken or dropped for something more important.
    ///
    /// <para>It implements <see cref="IEnumerator"/>, so a coroutine can simply write
    /// <c>yield return arbiter.Say(line)</c> and continue when the line is done. That one
    /// property deletes three separate hand-rolled wait loops that previously each
    /// reimplemented "sleep for roughly the length of the clip" and each got it slightly
    /// differently.</para>
    /// </summary>
    public class SpeechHandle : IEnumerator
    {
        /// <summary>Finished, one way or another. Spoken, skipped, preempted or dropped.</summary>
        public bool IsDone { get; private set; }

        /// <summary>Never made it to the speaker, because something better was talking.</summary>
        public bool WasDropped { get; private set; }

        /// <summary>Started and then cut off by a higher priority line, or by a skip.</summary>
        public bool WasInterrupted { get; private set; }

        internal void Complete() => IsDone = true;

        internal void Drop()
        {
            WasDropped = true;
            IsDone = true;
        }

        internal void Interrupt()
        {
            WasInterrupted = true;
            IsDone = true;
        }

        /// <summary>An already-finished handle, for callers that had nothing to say.</summary>
        public static SpeechHandle Finished()
        {
            var handle = new SpeechHandle();
            handle.Complete();
            return handle;
        }

        // IEnumerator: a coroutine keeps waiting while MoveNext returns true.
        bool IEnumerator.MoveNext() => !IsDone;
        object IEnumerator.Current => null;
        void IEnumerator.Reset() { }
    }
}
