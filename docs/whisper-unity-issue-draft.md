# Draft: issue for Macoron/whisper.unity

Post at https://github.com/Macoron/whisper.unity/issues/new

Your account, your contribution. I have not filed anything.

Everything below was reproduced on this machine today. The error text is copied from a real
build log, not paraphrased.

---

**Title:**

```
Package does not declare its dependency on com.unity.ugui
```

**Body:**

```markdown
## Summary

`com.whisper.unity` uses `UnityEngine.UI` types at runtime but its `package.json` has no
`dependencies` block, so the package fails to compile in any project that does not already
happen to include `com.unity.ugui`.

This is easy to miss because Unity's default 3D template includes uGUI, so most projects
never hit it. A minimal project created with `-createProject` does not.

## Affected files

- `Runtime/Utils/MicrophoneRecord.cs` uses `UnityEngine.UI.Image` (line 57) and
  `UnityEngine.UI.Dropdown` (line 69)
- `Runtime/Utils/UiUtils.cs` uses `UnityEngine.UI.ScrollRect` (line 12)

## Reproduction

1. Create an empty project:
   `Unity.exe -batchmode -quit -createProject C:\test`
2. Add to `Packages/manifest.json`:
   `"com.whisper.unity": "https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity"`
3. Open the project.

## Result

```
Library\PackageCache\com.whisper.unity@de9ce92ad4c5\Runtime\Utils\MicrophoneRecord.cs(6,19):
error CS0234: The type or namespace name 'UI' does not exist in the namespace 'UnityEngine'
(are you missing an assembly reference?)

Library\PackageCache\com.whisper.unity@de9ce92ad4c5\Runtime\Utils\UiUtils.cs(12,45):
error CS0246: The type or namespace name 'ScrollRect' could not be found

Library\PackageCache\com.whisper.unity@de9ce92ad4c5\Runtime\Utils\MicrophoneRecord.cs(57,28):
error CS0246: The type or namespace name 'Image' could not be found

Library\PackageCache\com.whisper.unity@de9ce92ad4c5\Runtime\Utils\MicrophoneRecord.cs(69,28):
error CS0246: The type or namespace name 'Dropdown' could not be found
```

## Workaround

Add `"com.unity.ugui": "2.0.0"` to the consuming project's manifest. Everything then
compiles and builds cleanly.

## Suggested fix

Declare it in `Packages/com.whisper.unity/package.json`:

```json
{
  "name": "com.whisper.unity",
  "version": "1.4.0",
  "dependencies": {
    "com.unity.ugui": "1.0.0"
  }
}
```

The UI usages are all optional conveniences (`[CanBeNull]` inspector fields and a helper),
so an alternative would be to move them behind an assembly definition that references uGUI,
keeping the core package dependency-free. Declaring the dependency is the smaller change.

## Environment

- Unity 6000.5.2f1, Windows 11
- whisper.unity at commit `de9ce92ad4c5` (1.4.0)
- Building StandaloneWindows64

Happy to open a PR for the `package.json` change if that is useful.
```

---

## Then the pull request

You chose to send both together, so change the last line of the issue body from an offer to
a statement: **"PR opened at #<number>."** Fill that in once the PR exists, and link the
issue from the PR too so the pair is obvious.

### Commands

```bash
gh repo fork Macoron/whisper.unity --clone --remote
cd whisper.unity
git checkout -b declare-ugui-dependency
```

Edit `Packages/com.whisper.unity/package.json`. Add a `dependencies` block after `"license"`:

```diff
   "unity": "2020.1",
   "license": "MIT",
+  "dependencies": {
+    "com.unity.ugui": "1.0.0"
+  },
   "licensesUrl": "https://github.com/Macoron/whisper.unity/blob/master/LICENSE.MD",
```

`1.0.0` rather than a current version on purpose: it is a floor, and uGUI has shipped with
every Unity since. Pinning higher would exclude editors that work perfectly well.

```bash
git commit -am "Declare dependency on com.unity.ugui"
git push -u origin declare-ugui-dependency
gh pr create --title "Declare dependency on com.unity.ugui" --body "Fixes the compile failure described in #<issue-number>.

MicrophoneRecord.cs and UiUtils.cs use UnityEngine.UI types, but package.json declares no dependencies, so the package fails to compile in projects that do not already include com.unity.ugui. Most projects do, via the default 3D template, which is why this is easy to miss.

Version floor set at 1.0.0 rather than a current version, since uGUI has shipped with every Unity release in the supported range.

Tested on Unity 6000.5.2f1, Windows, StandaloneWindows64."
```

## Notes for you

**Search the existing issues first.** If someone has already reported it, add your
reproduction to that thread and open the PR referencing theirs. Still a contribution, and
better manners.

**Tone.** Short, specific, reproducible, no complaint. Maintainers receive a lot of "doesn't
work" reports; one with exact line numbers, a reproduction and a fix attached is a gift.

**If they prefer a different fix,** for example moving the UI code behind its own assembly
definition so the core package stays dependency-free, that is a reasonable position and
worth saying so in the thread. A PR that gets closed after a good discussion is still a
contribution, and the issue stands on its own either way.
