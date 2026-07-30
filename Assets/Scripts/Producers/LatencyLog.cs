using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dikdik.Producers
{
    /// <summary>
    /// Times every stage between a player speaking and the rover moving, and writes it out.
    ///
    /// <para>This exists because a number was wrong for weeks. The project reported "1875 ms
    /// median latency" and that figure was measured from the moment the microphone closed. The
    /// microphone does not close when the player stops talking; it closes
    /// <c>vadStopTime</c> seconds later, which was 1.5. So the real wait was the silence
    /// timeout plus transcription plus the transport delay, and the headline number counted
    /// only the middle one.</para>
    ///
    /// <para>Nobody noticed because the stages were never timed separately. One number for a
    /// pipeline with four stages is not a measurement, it is a guess with a decimal point on
    /// it.</para>
    ///
    /// <para>Writes a CSV next to the player log, one row per utterance, so a run can be
    /// loaded into anything and summarised. Also keeps a rolling median in memory for the
    /// on-screen readout.</para>
    /// </summary>
    public static class LatencyLog
    {
        /// <summary>One utterance, all stages, in seconds.</summary>
        public struct Sample
        {
            /// <summary>Voice detection fired. The player had started speaking ~100ms before.</summary>
            public float SpeechStart;

            /// <summary>The microphone actually closed. Includes the vadStopTime silence wait.</summary>
            public float MicClosed;

            /// <summary>Transcription returned.</summary>
            public float Transcribed;

            /// <summary>The command reached the rover.</summary>
            public float Delivered;

            public string Transcript;
            public string Intent;

            /// <summary>Silence timeout plus however long VAD took to notice the quiet.</summary>
            public float SilenceWait => MicClosed - SpeechStart;

            /// <summary>Whisper's share.</summary>
            public float Transcription => Transcribed - MicClosed;

            /// <summary>The deliberate delay, and whatever compensation did to it.</summary>
            public float Transport => Delivered - Transcribed;

            /// <summary>
            /// What the player actually waited, from starting to speak to the rover acting.
            /// This is the only number worth putting in a README.
            /// </summary>
            public float EndToEnd => Delivered - SpeechStart;
        }

        private static readonly List<Sample> Samples = new List<Sample>();
        private static string _path;

        public static int Count => Samples.Count;

        /// <summary>Median end-to-end wait so far, or zero with nothing recorded.</summary>
        public static float MedianEndToEnd => Median(s => s.EndToEnd);

        public static float MedianSilenceWait => Median(s => s.SilenceWait);
        public static float MedianTranscription => Median(s => s.Transcription);

        private static float Median(System.Func<Sample, float> pick)
        {
            if (Samples.Count == 0)
                return 0f;

            var values = new List<float>(Samples.Count);
            foreach (var s in Samples)
                values.Add(pick(s));

            values.Sort();
            var mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) * 0.5f;
        }

        public static void Record(Sample sample)
        {
            Samples.Add(sample);
            Append(sample);

            // Logged individually as well as summarised. A median hides the tail, and the
            // tail is where a voice interface actually annoys people.
            Debug.Log($"[Latency] end-to-end {sample.EndToEnd:0.000}s " +
                      $"(silence {sample.SilenceWait:0.000} + whisper {sample.Transcription:0.000} " +
                      $"+ transport {sample.Transport:0.000})  \"{sample.Transcript}\" -> {sample.Intent}");
        }

        private static void Append(Sample s)
        {
            if (_path == null)
            {
                _path = Path.Combine(Application.persistentDataPath, "latency.csv");

                if (!File.Exists(_path))
                    File.WriteAllText(_path,
                        "speechStart,micClosed,transcribed,delivered,silenceWait," +
                        "transcription,transport,endToEnd,transcript,intent\n",
                        Encoding.UTF8);

                Debug.Log($"[Latency] logging to {_path}");
            }

            // Invariant culture, always. A machine set to comma decimals would write a CSV
            // that silently means something else.
            var line = string.Format(CultureInfo.InvariantCulture,
                "{0:0.000},{1:0.000},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},\"{8}\",{9}\n",
                s.SpeechStart, s.MicClosed, s.Transcribed, s.Delivered,
                s.SilenceWait, s.Transcription, s.Transport, s.EndToEnd,
                (s.Transcript ?? string.Empty).Replace("\"", "'"), s.Intent);

            File.AppendAllText(_path, line, Encoding.UTF8);
        }

        /// <summary>Human-readable summary, for the settings screen and the console.</summary>
        public static string Summary()
        {
            if (Samples.Count == 0)
                return "No utterances recorded yet.";

            return $"{Samples.Count} utterances.  Median end to end {MedianEndToEnd:0.00}s " +
                   $"= silence {MedianSilenceWait:0.00} + whisper {MedianTranscription:0.00} " +
                   $"+ transport {Median(s => s.Transport):0.00}";
        }

        public static void Clear()
        {
            Samples.Clear();
        }
    }
}
