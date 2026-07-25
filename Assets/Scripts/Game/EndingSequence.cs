using System.Collections;
using System.Collections.Generic;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The ending. The rover reaches the rim, Control hands you the open loop, and what
    /// goes out is your own voice: every command you gave across the whole game, in the
    /// order you gave it, unedited. The other rovers wake to it.
    ///
    /// The argument does not get stated because it does not need to be. The thing that
    /// reaches everyone else was never translated into anything. It is the player,
    /// played back.
    ///
    /// Lives on a trigger at the rim. Reaching it starts the sequence.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EndingSequence : MonoBehaviour
    {
        private enum Phase { Approaching, RimSpeech, AwaitingBroadcast, Broadcasting, Done }

        [SerializeField] private LevelDirector director;
        [SerializeField] private AudioSource broadcastSource;
        [SerializeField] private DormantRover[] dormant;

        [Tooltip("Fallback broadcast length when the journal is empty, e.g. jumping " +
                 "straight to this level in testing.")]
        [SerializeField] private float emptyBroadcastSeconds = 4f;

        private Phase _phase = Phase.Approaching;
        private SupervisorVoice _supervisor;
        private VoiceJournal _journal;
        private CommsDisplay _comms;

        private void Start()
        {
            GetComponent<Collider>().isTrigger = true;

            _supervisor = FindAnyObjectByType<SupervisorVoice>();
            _journal = Bootstrap.Instance != null
                ? Bootstrap.Instance.Journal
                : FindAnyObjectByType<VoiceJournal>();
            _comms = Bootstrap.Instance != null ? Bootstrap.Instance.Comms : FindAnyObjectByType<CommsDisplay>();
        }

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued += OnCommand;
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued -= OnCommand;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_phase != Phase.Approaching)
                return;

            if (other.GetComponentInParent<RoverController>() == null)
                return;

            StartCoroutine(RimSpeech());
        }

        private IEnumerator RimSpeech()
        {
            _phase = Phase.RimSpeech;

            // final_01, final_02, final_03, in order. PlayOne cycles a group in order,
            // and "final" is played nowhere else, so the first three calls land on the
            // first three lines. The fourth is saved for after the broadcast.
            yield return PlayFinalLine();   // "There are others out there. Dormant."
            yield return PlayFinalLine();   // "You are on the open loop now. Everything hears you."
            yield return PlayFinalLine();   // "Say it once."

            _phase = Phase.AwaitingBroadcast;
        }

        private IEnumerator PlayFinalLine()
        {
            var arbiter = Dikdik.Game.Voice.VoiceArbiter.Instance;
            if (arbiter == null)
                yield break;

            // Was: fire the clip, then poll a global "is the mic blocked" flag as a proxy
            // for "has it finished". Any unrelated speech extended that flag and a skip
            // zeroed it, so this waited for the wrong thing in both directions. The
            // handle knows when its own line is done.
            yield return arbiter.SayGroup("final", Dikdik.Game.Voice.SpeechPriority.Critical,
                                          Dikdik.Game.Voice.Speaker.Control, essential: true);
        }

        private void OnCommand(Intent intent)
        {
            // "Say it once." Any command the player gives on the open loop sends the
            // broadcast; Wake is the natural one, but we do not make them guess the word.
            if (_phase != Phase.AwaitingBroadcast)
                return;

            StartCoroutine(Broadcast());
        }

        private IEnumerator Broadcast()
        {
            _phase = Phase.Broadcasting;

            var clip = _journal != null ? _journal.BuildBroadcast("your-broadcast") : null;
            var transcript = _journal != null ? _journal.BroadcastTranscript() : new List<string>();

            float duration;
            if (clip != null && broadcastSource != null)
            {
                broadcastSource.clip = clip;
                broadcastSource.Play();
                duration = clip.length;
            }
            else
            {
                // Empty journal: nothing recorded to send. Still wake them, and say why.
                duration = emptyBroadcastSeconds;
                if (_comms != null)
                    _comms.ShowBroadcastLine("(Nothing recorded yet. Play from the first level " +
                                             "and your own voice broadcasts here.)");
            }

            var count = dormant != null ? dormant.Length : 0;
            var start = Time.realtimeSinceStartup;
            var wokenSoFar = 0;
            var lastCaption = -1;

            while (Time.realtimeSinceStartup - start < duration)
            {
                var t = duration <= 0f ? 1f : (Time.realtimeSinceStartup - start) / duration;

                // Wake the rovers steadily across the broadcast, so by the end they are
                // all lit and "all of them" is literally true.
                var shouldBeAwake = Mathf.CeilToInt(Mathf.Clamp01(t) * count);
                while (wokenSoFar < shouldBeAwake && wokenSoFar < count)
                {
                    dormant[wokenSoFar].Wake();
                    wokenSoFar++;
                }

                // Show the player's own words going out, in step.
                if (transcript.Count > 0 && _comms != null)
                {
                    var idx = Mathf.Min(transcript.Count - 1, Mathf.FloorToInt(t * transcript.Count));
                    if (idx != lastCaption)
                    {
                        _comms.ShowBroadcastLine(transcript[idx]);
                        lastCaption = idx;
                    }
                }

                yield return null;
            }

            for (; wokenSoFar < count; wokenSoFar++)
                dormant[wokenSoFar].Wake();

            yield return new WaitForSecondsRealtime(0.8f);

            // "All of them. One transmission."
            yield return PlayFinalLine();

            _phase = Phase.Done;

            if (director != null)
                director.Complete();
        }
    }
}
