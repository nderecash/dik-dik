using System;
using System.Globalization;
using System.IO;
using System.Text;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Spike
{
    /// <summary>
    /// Writes one line per utterance so the day 3 go/no-go gate is decided by data
    /// rather than by anyone's memory of how the session felt.
    ///
    /// The spike scene tells the player which command to give, so every row carries
    /// the intent we asked for next to the intent we resolved. Accuracy is then a
    /// division, not a judgement call, and nobody has to sit there ticking boxes.
    ///
    /// File lands in Application.persistentDataPath, which on Windows is
    /// %USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;\.
    /// </summary>
    public class SpikeLogger : MonoBehaviour
    {
        private const string FileName = "spike-log.csv";

        private string _path;
        private int _total;
        private int _correct;

        public string Path => _path;
        public int Total => _total;
        public int Correct => _correct;

        /// <summary>Accuracy so far, 0 to 1. Returns 0 before anything is logged.</summary>
        public float Accuracy => _total == 0 ? 0f : (float)_correct / _total;

        private void Awake()
        {
            _path = System.IO.Path.Combine(Application.persistentDataPath, FileName);

            if (!File.Exists(_path))
            {
                File.WriteAllText(_path,
                    "timestamp,expected,resolved,confidence,latency_ms,source,raw_text\n",
                    Encoding.UTF8);
            }

            Debug.Log($"[Spike] Logging to {_path}");
        }

        public void Log(IntentId expected, Intent resolved, long latencyMs)
        {
            _total++;
            if (resolved.Id == expected)
                _correct++;

            var row = string.Join(",",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                expected.ToString(),
                resolved.Id.ToString(),
                resolved.Confidence.ToString("0.000", CultureInfo.InvariantCulture),
                latencyMs.ToString(CultureInfo.InvariantCulture),
                resolved.Source.ToString(),
                Quote(resolved.RawText));

            try
            {
                File.AppendAllText(_path, row + "\n", Encoding.UTF8);
            }
            catch (Exception e)
            {
                // Never let logging break a playtest. A lost row is cheaper than a lost session.
                Debug.LogWarning($"[Spike] Could not write log row: {e.Message}");
            }
        }

        /// <summary>CSV quoting, because transcripts contain commas and the odd quote mark.</summary>
        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
