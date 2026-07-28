using System;
using Dikdik.Commands;
using Dikdik.Matching;

// Regression check for the delight vocabulary added in Phase 4.
//
// The real risk of adding jump, spin, dance, hello and who is not that they fail to
// match. It is that they steal something. "hi" is two characters and edit distance is
// generous; "go" appears inside "boogie"; "move" was already a Go phrase and the puzzle
// reads it as push. Every case below is a driving command that must still resolve to the
// same thing it resolved to before, plus the new phrases resolving to themselves.

static class Tests
{
    static int failures = 0;
    static int checks = 0;

    static void Expect(string said, IntentId want)
    {
        checks++;
        var got = FuzzyIntentMatcher.Match(said, CommandSource.Voice);
        if (got.Id == want)
            return;

        failures++;
        Console.WriteLine($"  FAIL  \"{said}\"  expected {want}, got {got.Id}");
    }

    static void Main()
    {
        Console.WriteLine("Core driving commands must be unchanged:");
        Expect("go", IntentId.Go);
        Expect("stop", IntentId.Stop);
        Expect("left", IntentId.Left);
        Expect("right", IntentId.Right);
        Expect("turn left", IntentId.Left);
        Expect("turn right", IntentId.Right);
        Expect("go left", IntentId.Left);
        Expect("go right", IntentId.Right);
        Expect("back", IntentId.Back);
        Expect("back up", IntentId.Back);
        Expect("keep going", IntentId.Go);
        Expect("carry on", IntentId.Go);
        Expect("start moving", IntentId.Go);
        Expect("stop moving", IntentId.Stop);
        Expect("whoa", IntentId.Stop);
        Expect("hold on", IntentId.Stop);
        Expect("move forward", IntentId.Go);
        Expect("forward", IntentId.Go);
        Expect("yes", IntentId.Go);
        Expect("open", IntentId.Open);
        Expect("open the door", IntentId.Open);
        Expect("light", IntentId.Light);
        Expect("lights on", IntentId.Light);
        Expect("help", IntentId.Help);
        Expect("reset", IntentId.Restart);
        Expect("say that again", IntentId.Repeat);

        Console.WriteLine("New delight commands must resolve to themselves:");
        Expect("jump", IntentId.Jump);
        Expect("can you jump", IntentId.Jump);
        Expect("spin", IntentId.Spin);
        Expect("spin around", IntentId.Spin);
        Expect("dance", IntentId.Dance);
        Expect("do a dance", IntentId.Dance);
        Expect("hello", IntentId.Greet);
        Expect("hi", IntentId.Greet);
        Expect("hey", IntentId.Greet);
        Expect("who are you", IntentId.Who);
        Expect("whats your name", IntentId.Who);

        Console.WriteLine("Wake keeps its own words:");
        Expect("wake up", IntentId.Wake);
        Expect("wake them", IntentId.Wake);

        // The blockage answers.
        //
        // Every one of these returned None before the fix, because the puzzle matched
        // substrings against the raw transcript and nobody had put the words into the
        // vocabulary. The prompt named three answers out loud and all three failed. That
        // is precisely the bug this file exists to catch, and it shipped anyway, because
        // the file did not know these words were meant to mean anything.
        Console.WriteLine("Blockage answers must resolve, a prompt names them:");
        Expect("cut it", IntentId.Cut);
        Expect("cut", IntentId.Cut);
        Expect("cut through it", IntentId.Cut);
        Expect("saw", IntentId.Cut);
        Expect("dissolve it", IntentId.Dissolve);
        Expect("dissolve", IntentId.Dissolve);
        Expect("melt it", IntentId.Dissolve);
        Expect("push it", IntentId.Push);
        Expect("push", IntentId.Push);
        Expect("shove it", IntentId.Push);

        // The repair words.
        //
        // The last command in the game, and none of these existed. The prompt said "patch
        // it" and the matcher returned None for every word a player could reach for, so
        // the game could not be finished by anybody. Second time the same mistake shipped:
        // a prompt naming words the vocabulary did not contain.
        Console.WriteLine("Repair words, the ending depends on them:");
        Expect("fix it", IntentId.Repair);
        Expect("fix", IntentId.Repair);
        Expect("patch it", IntentId.Repair);
        Expect("patch", IntentId.Repair);
        Expect("repair it", IntentId.Repair);
        Expect("mend it", IntentId.Repair);
        Expect("seal it", IntentId.Repair);
        Expect("fix the line", IntentId.Repair);

        // And they must steal nothing. "push" and "go" both end with the rover moving, so
        // either quietly becoming the other would be invisible until it mattered.
        Console.WriteLine("And they steal nothing:");
        Expect("go", IntentId.Go);
        Expect("stop", IntentId.Stop);
        Expect("back", IntentId.Back);
        Expect("left", IntentId.Left);
        Expect("right", IntentId.Right);
        Expect("move forward", IntentId.Go);

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"PASS  {checks} checks, 0 failures"
            : $"FAIL  {checks} checks, {failures} failures");

        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
