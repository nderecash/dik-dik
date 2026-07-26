using System.Collections;
using Dikdik.Commands;
using Dikdik.Game.Cable;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The end. The break in the relay line is found, the player patches it, the power
    /// comes back, and everyone goes home.
    ///
    /// <para>This replaces an ending that recorded every command the player gave across
    /// the whole game and played it back to them on the open loop. That version was built
    /// and it worked, and playing it felt like surveillance: a microphone does not record
    /// commands, it records a room. So the retention went with it. What is left is smaller
    /// and better, because it is about the thing the player actually did.</para>
    ///
    /// <para><b>The player still has to say it.</b> Control asks, and then waits, however
    /// long that takes. The rover has never once acted without being spoken to, and the
    /// last thing it does in the game is not going to be the exception. Any command works,
    /// so nobody has to guess a magic word at the emotional peak of the thing.</para>
    ///
    /// <para>The whole cable lights up as the power returns, which is the one moment the
    /// line is fully lit from end to end. Twenty scans across six sectors built that
    /// picture up a section at a time; this pays it off in one go.</para>
    /// </summary>
    public class RepairFinale : MonoBehaviour
    {
        private enum Phase { Waiting, Asking, AwaitingPlayer, Repairing, Done }

        [SerializeField] private MissionProgress progress;
        [SerializeField] private CableVisual cable;
        [SerializeField] private RoverController rover;
        [SerializeField] private RoverLight roverLight;
        [SerializeField] private LevelDirector director;

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip powerClip;

        [Tooltip("How long the patch takes, in real seconds. Long enough for 'Come on. " +
                 "Come on.' to be a real wait rather than a line read over nothing.")]
        [SerializeField] private float repairSeconds = 4.5f;

        private Phase _phase = Phase.Waiting;

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

        private void Update()
        {
            if (_phase != Phase.Waiting || progress == null)
                return;

            // Wait for the fault to be found. Polled rather than wired to an event,
            // because MissionProgress finds its checkpoints in Start and this component
            // has no business caring which order those two ran in.
            if (progress.TotalCheckpoints > 0 &&
                progress.ScannedCheckpoints >= progress.TotalCheckpoints)
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _phase = Phase.Asking;

            var arbiter = VoiceArbiter.Instance;
            if (arbiter == null)
            {
                Finish();
                yield break;
            }

            // sup_fix_01: "Go on then. Patch it."
            yield return arbiter.SayGroup("fix", SpeechPriority.Critical, Speaker.Control,
                                          essential: true);

            // And now we wait, for as long as it takes.
            _phase = Phase.AwaitingPlayer;

            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ShowPrompt(
                    "The break is right in front of it.",
                    "Tell Salty to patch it. Anything you like.");

            while (_phase == Phase.AwaitingPlayer)
                yield return null;

            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ClearPrompt();

            yield return Repair(arbiter);
        }

        private void OnCommand(Intent intent)
        {
            // Anything. They have been talking to this machine for six sectors and the
            // last thing the game does will not be to tell them they used the wrong word.
            if (_phase == Phase.AwaitingPlayer)
                _phase = Phase.Repairing;
        }

        private IEnumerator Repair(VoiceArbiter arbiter)
        {
            if (roverLight != null)
                roverLight.SignalScanning();

            // sup_fix_02: "Come on. Come on."
            arbiter.SayGroup("fix", SpeechPriority.Critical, Speaker.Control, essential: true);

            var elapsed = 0f;
            while (elapsed < repairSeconds)
            {
                if (!GamePause.IsPaused)
                    elapsed += Time.unscaledDeltaTime;

                yield return null;
            }

            // Power. The whole line at once, the only time it is lit end to end.
            if (cable != null)
                cable.MarkAllScanned();

            if (roverLight != null)
                roverLight.SignalScanComplete(fault: false);

            if (source != null && powerClip != null)
                source.PlayOneShot(powerClip, GameSettings.EffectsVolume);

            // sup_fix_03: "That's power. We've got power."
            yield return arbiter.SayGroup("fix", SpeechPriority.Critical, Speaker.Control,
                                          essential: true);

            // sup_fix_04: "You did it. We're going home."
            yield return arbiter.SayGroup("fix", SpeechPriority.Critical, Speaker.Control,
                                          essential: true);

            Finish();
        }

        private void Finish()
        {
            _phase = Phase.Done;

            if (director != null)
                director.Complete();
        }
    }
}
