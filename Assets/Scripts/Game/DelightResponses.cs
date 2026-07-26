using System.Collections;
using Dikdik.Commands;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// What happens when the player tries something the game never asked for.
    ///
    /// <para>This game spends its opening line telling you to just talk to the rover, that
    /// plain speech is fine, that it is not fussy. Somebody who believes that will
    /// eventually ask it to jump. The worst available answer at that moment is "I did not
    /// understand", because it is the game admitting the invitation was marketing.</para>
    ///
    /// <para>So five things get real replies. None of them is required, none is hinted at,
    /// none appears in any objective or help text, and finding them is the entire reward.
    /// Four are answered in words only, which is deliberate: Control saying the rover
    /// cannot jump, because it has six wheels and no legs, is funnier and more in character
    /// than a rover that hops.</para>
    ///
    /// <para>Spin is the exception and does something, because the script promised it
    /// would: "Now that it can actually do. After a fashion." A rover that can only turn
    /// on the spot doing a full rotation is exactly the joke that line is making.</para>
    ///
    /// <para>Priority is Reactive, so a scan report, a briefing or a stuck warning all
    /// outrank it. A joke is the one thing in this game that is genuinely fine to drop.</para>
    /// </summary>
    public class DelightResponses : MonoBehaviour
    {
        [SerializeField] private RoverController rover;

        [Tooltip("Degrees the rover turns for a spin. A full circle, in one go.")]
        [SerializeField] private float spinDegrees = 360f;

        [SerializeField] private float spinSeconds = 2.2f;

        private Coroutine _spinning;

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

        private void OnCommand(Intent intent)
        {
            var arbiter = VoiceArbiter.Instance;
            if (arbiter == null)
                return;

            // One named clip each, not a cycling group. These are jokes with specific
            // answers, and hearing "It doesn't jump. Six wheels, no legs" in reply to
            // "hello" would be worse than saying nothing at all.
            switch (intent.Id)
            {
                case IntentId.Jump:
                    arbiter.SayClip("sup_fun_01", SpeechPriority.Reactive, Speaker.Control);
                    break;

                case IntentId.Spin:
                    arbiter.SayClip("sup_fun_02", SpeechPriority.Reactive, Speaker.Control);
                    StartSpin();
                    break;

                case IntentId.Greet:
                    arbiter.SayClip("sup_fun_03", SpeechPriority.Reactive, Speaker.Control);
                    break;

                case IntentId.Dance:
                    arbiter.SayClip("sup_fun_04", SpeechPriority.Reactive, Speaker.Control);
                    StartSpin();
                    break;

                case IntentId.Who:
                    arbiter.SayClip("sup_fun_05", SpeechPriority.Reactive, Speaker.Control);
                    break;
            }
        }

        private void StartSpin()
        {
            if (rover == null || _spinning != null)
                return;

            _spinning = StartCoroutine(Spin());
        }

        private IEnumerator Spin()
        {
            var start = rover.transform.eulerAngles.y;
            var elapsed = 0f;

            while (elapsed < spinSeconds)
            {
                if (GamePause.IsPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime * GameSettings.GameSpeed;
                var t = Mathf.Clamp01(elapsed / spinSeconds);

                // Eased, so it winds up and settles rather than snapping through a circle.
                var eased = t * t * (3f - 2f * t);

                rover.transform.rotation = Quaternion.Euler(0f, start + spinDegrees * eased, 0f);
                yield return null;
            }

            // Put the heading back exactly where it started. The rover has just done a
            // full circle for fun; it must not also have quietly lost the way it was
            // facing, because the player did not ask for that and would not connect the
            // two.
            rover.transform.rotation = Quaternion.Euler(0f, start, 0f);
            rover.SnapHeadingToTransform();

            _spinning = null;
        }
    }
}
