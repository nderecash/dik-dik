#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Generic;
using Dikdik.Commands;
using Dikdik.Producers;
using UnityEngine;

namespace Dikdik.Spike
{
    /// <summary>
    /// The go/no-go gate, as a scene.
    ///
    /// Version two. Version one listened continuously and advanced on anything it
    /// detected, so it burned through tasks recording silence while the tester was
    /// still reading the instructions. Eight of fifteen rows came back [BLANK_AUDIO].
    /// A measuring instrument that fails to explain itself, in a project about
    /// interfaces that fail to explain themselves.
    ///
    /// So now: nothing happens until you say it can. You read the task, you press a
    /// key when you are ready, and only then does the microphone open. Silence cannot
    /// cost you a task, and any task can be redone.
    ///
    /// Drawn with OnGUI deliberately. No canvas, no fonts, no prefabs, nothing that a
    /// batch mode script can generate wrongly. This is an instrument, not a screenshot.
    /// </summary>
    public class SpikeRunner : MonoBehaviour
    {
        private enum Phase { Intro, Ready, Listening, Result, Done }

        private struct Task
        {
            public string Instruction;
            public IntentId Expected;

            public Task(string instruction, IntentId expected)
            {
                Instruction = instruction;
                Expected = expected;
            }
        }

        [SerializeField] private SpikeLogger logger;
        [SerializeField] private VoiceCommandProducer voice;

        // Nothing here is on a timer. Every screen waits for a key.

        /// <summary>
        /// Three passes over the five spike intents, phrased differently each time.
        /// The instruction never contains the command word: if the game only works when
        /// you read our vocabulary back to us, we have built a keyword recogniser with
        /// extra steps and should just use the keyword recogniser.
        /// </summary>
        private readonly List<Task> _tasks = new List<Task>
        {
            // Second set of wordings, written after the first session.
            //
            // Two rules learned the hard way. One: never print a phrasing the player
            // might repeat back. Version one opened with "Tell the rover to start
            // moving", the tester said "Start moving", and the vocabulary had no entry
            // for it. The prompt handed them a failure.
            //
            // Two: these must differ from the first set, because a tester who half
            // remembers what worked last time inflates the number without meaning to.
            // Directions still name themselves, which is unavoidable and fine; what
            // matters is that no whole command phrase appears on screen.
            new Task("The way ahead is clear. Get it rolling.",        IntentId.Go),
            new Task("There is a drop just in front of it.",           IntentId.Stop),
            new Task("The corridor bends towards its left.",           IntentId.Left),
            new Task("The corridor bends towards its right.",          IntentId.Right),
            new Task("A sealed hatch is blocking the way.",            IntentId.Open),

            new Task("It has been sitting there long enough.",         IntentId.Go),
            new Task("Whatever it is doing, you want that to end.",    IntentId.Stop),
            new Task("Ninety degrees anticlockwise.",                  IntentId.Left),
            new Task("Ninety degrees clockwise.",                      IntentId.Right),
            new Task("There is a gate between it and the exit.",       IntentId.Open),

            new Task("You want it under way again.",                   IntentId.Go),
            new Task("It needs to be motionless, right now.",          IntentId.Stop),
            new Task("Its left flank is where you need it pointed.",   IntentId.Left),
            new Task("Its right flank is where you need it pointed.",  IntentId.Right),
            new Task("Something is shut that needs not to be.",        IntentId.Open)
        };

        private Phase _phase = Phase.Intro;
        private int _index;
        private string _transcript = "";
        private long _latencyMs;
        private IntentId _resolved = IntentId.None;
        private bool _speaking;

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued += OnCommand;
                CommandBus.Instance.CommandNotUnderstood += OnCommand;
            }

            if (voice != null)
            {
                voice.TranscriptReady += OnTranscript;
                voice.VoiceDetectedChanged += OnVoiceDetected;
            }
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued -= OnCommand;
                CommandBus.Instance.CommandNotUnderstood -= OnCommand;
            }

            if (voice != null)
            {
                voice.TranscriptReady -= OnTranscript;
                voice.VoiceDetectedChanged -= OnVoiceDetected;
            }
        }

        private void OnTranscript(string text, long latencyMs)
        {
            _transcript = text;
            _latencyMs = latencyMs;
        }

        private void OnVoiceDetected(bool speaking)
        {
            _speaking = speaking;
        }

        private void OnCommand(Intent intent)
        {
            // Only a command we actually asked for counts. Anything arriving in another
            // phase is stray and is ignored rather than silently scored.
            if (_phase != Phase.Listening)
                return;

            // This is a speech recognition measurement. Nothing typed may score.
            //
            // Run two was destroyed by the absence of this line. The arm key was SPACE,
            // SPACE is also the default binding for Stop, so every arm press fired a
            // Stop command that the runner logged and advanced on before the microphone
            // produced anything. Fifteen keyboard rows, zero speech, a meaningless 20%.
            // The arm key has moved too, but this check is the one that actually matters,
            // because it does not depend on me picking an unbound key correctly.
            if (intent.Source != CommandSource.Voice)
                return;

            _resolved = intent.Id;

            if (logger != null)
                logger.Log(_tasks[_index].Expected, intent, _latencyMs);

            _phase = Phase.Result;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Intro:
                    // Was Input.anyKeyDown, which invited a stray W or Space and made
                    // the first thing you touch a game command. One named key only.
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        _phase = Phase.Ready;
                    break;

                case Phase.Ready:
                    // ENTER, not SPACE. The default bindings occupy W, A, S, D, Space,
                    // E, F, Q, R and H, so any control key for this scene has to come
                    // from outside that set or it doubles as a game command.
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        BeginListening();
                    break;

                case Phase.Listening:
                    // Escape hatch if the microphone is not picking anything up.
                    if (Input.GetKeyDown(KeyCode.Escape))
                        Cancel();
                    break;

                case Phase.Result:
                    // TAB, not R. R is the default binding for Repeat.
                    if (Input.GetKeyDown(KeyCode.Tab))
                    {
                        Redo();
                        break;
                    }

                    // Never advance on a timer.
                    //
                    // A result that disappears before it has been read is a result
                    // nobody can check, and the tester ends up chasing the interface
                    // instead of testing it. It is also a strange thing to build into
                    // the measuring instrument for a game whose whole argument is that
                    // people should not have to keep up with software.
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        Advance();
                    break;
            }
        }

        private void BeginListening()
        {
            _transcript = "";
            _resolved = IntentId.None;
            _latencyMs = 0;
            _phase = Phase.Listening;

            if (voice != null)
                voice.StartListening();
        }

        private void Cancel()
        {
            if (voice != null)
                voice.StopListening();

            _phase = Phase.Ready;
        }

        /// <summary>
        /// Redo the current task. The previous attempt is already logged, so the log
        /// keeps an honest record, and the accuracy figure counts every attempt made.
        /// Nothing here quietly improves the number.
        /// </summary>
        private void Redo()
        {
            _phase = Phase.Ready;
            _transcript = "";
            _resolved = IntentId.None;
        }

        private void Advance()
        {
            _index++;

            if (_index >= _tasks.Count)
            {
                _phase = Phase.Done;
                if (voice != null)
                    voice.StopListening();
                return;
            }

            _phase = Phase.Ready;
        }

        private void OnGUI()
        {
            var area = new Rect(40, 40, Screen.width - 80, Screen.height - 80);
            GUILayout.BeginArea(area, GUI.skin.box);

            var head = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            var big = new GUIStyle(GUI.skin.label) { fontSize = 34, wordWrap = true };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };

            switch (_phase)
            {
                case Phase.Intro: DrawIntro(head, body); break;
                case Phase.Done: DrawDone(head, body); break;
                default: DrawTask(head, big, body); break;
            }

            GUILayout.EndArea();
        }

        private void DrawIntro(GUIStyle head, GUIStyle body)
        {
            GUILayout.Label("Dik-dik: speech recognition check", head);
            GUILayout.Space(14);

            GUILayout.Label(
                "Fifteen short tasks. Each one asks you to tell a rover to do something.\n\n" +
                "Nothing listens until you press SPACE. Read the task first. Take as long " +
                "as you like. Silence costs you nothing.", body);

            GUILayout.Space(16);
            GUILayout.Label("Three rules:", head);
            GUILayout.Space(8);

            GUILayout.Label(
                "1.  Speak normally. Normal pace, normal volume, your normal accent.\n" +
                "     Do not enunciate for the machine.\n\n" +
                "2.  Do not use the words on screen. The task says what you want to happen,\n" +
                "     not what to say. Say whatever you would actually say.\n\n" +
                "3.  Do not help it. If it mishears you, do not slow down or repeat more\n" +
                "     clearly. A misheard result is the result I need. If you adapt to the\n" +
                "     machine, we ship a game that only works for people who do that.", body);

            GUILayout.Space(20);
            GUILayout.Label("ENTER opens the microphone.   ESC cancels a recording.   TAB redoes a task.", body);
            GUILayout.Space(6);
            GUILayout.Label("Nothing you type can score. Only speech counts here.", body);
            GUILayout.Space(14);
            GUILayout.Label("Press ENTER to begin.", head);
        }

        private void DrawTask(GUIStyle head, GUIStyle big, GUIStyle body)
        {
            GUILayout.Label($"Task {_index + 1} of {_tasks.Count}", body);
            GUILayout.Space(8);
            GUILayout.Label(_tasks[_index].Instruction, big);
            GUILayout.Space(24);

            switch (_phase)
            {
                case Phase.Ready:
                    GUILayout.Label("Press ENTER when you are ready to speak.", head);
                    break;

                case Phase.Listening:
                    GUILayout.Label(_speaking ? "Listening: hearing you" : "Listening: go ahead", head);
                    GUILayout.Space(8);
                    GUILayout.Label("It stops on its own when you finish. ESC to cancel.", body);
                    break;

                case Phase.Result:
                    GUILayout.Label("I heard:", body);
                    GUILayout.Label(string.IsNullOrWhiteSpace(_transcript) ? "(nothing)" : _transcript, head);
                    GUILayout.Space(10);

                    var wanted = _tasks[_index].Expected;
                    GUILayout.Label(
                        _resolved == wanted
                            ? $"Understood as {_resolved}.  Correct."
                            : $"Understood as {_resolved}.  Expected {wanted}.", head);

                    GUILayout.Space(8);
                    GUILayout.Label($"Whisper took {_latencyMs} ms.", body);
                    GUILayout.Space(10);
                    GUILayout.Label("ENTER for the next task.   TAB to redo this one.", head);
                    break;
            }

            if (logger != null && logger.Total > 0)
            {
                GUILayout.Space(20);
                GUILayout.Label($"So far: {logger.Correct} of {logger.Total} " +
                                $"({logger.Accuracy * 100f:0}%)", body);
            }
        }

        private void DrawDone(GUIStyle head, GUIStyle body)
        {
            GUILayout.Label("Done. Thank you.", head);
            GUILayout.Space(12);

            if (logger != null)
            {
                GUILayout.Label($"Matched {logger.Correct} of {logger.Total}  " +
                                $"({logger.Accuracy * 100f:0}%)", head);
                GUILayout.Space(14);
                GUILayout.Label("Log written to:", body);
                GUILayout.Label(logger.Path, body);
            }

            GUILayout.Space(20);
            GUILayout.Label("Send me that percentage and the path. You can close the window.", body);
        }
    }
}
#endif
