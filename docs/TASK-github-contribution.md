# TASK: file the whisper.unity issue and pull request

**Time:** about 20 minutes
**You need:** a browser, and `gh` already logged in as `nderecash` (it is)
**Result:** one issue and one pull request on a package with ~1.5k stars

Four stages. Do them in order. Each stage ends with a **CHECK** you must pass before moving on.

---

# STAGE 1 — Make sure nobody beat you to it

**Time: 2 minutes**

- [ ] Open https://github.com/Macoron/whisper.unity/issues?q=is%3Aissue+ugui
- [ ] Also search the closed ones: https://github.com/Macoron/whisper.unity/issues?q=ugui

### CHECK

**Nothing found?** → Go to Stage 2.

**Someone already reported it?** → Stop. Do this instead:
1. Add a comment to their issue with the reproduction from Stage 2's body text
2. Skip to Stage 3, and reference their issue number instead of your own

---

# STAGE 2 — File the issue

**Time: 5 minutes**

- [ ] Open https://github.com/Macoron/whisper.unity/issues/new

### Paste into the TITLE field

```
Package does not declare its dependency on com.unity.ugui
```

### Paste into the BODY field

Everything between the lines below. Do not edit it.

---BEGIN BODY---

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

1. Create an empty project: `Unity.exe -batchmode -quit -createProject C:\test`
2. Add to `Packages/manifest.json`:
   `"com.whisper.unity": "https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity"`
3. Open the project.

## Result

```
Library\PackageCache\com.whisper.unity@de9ce92ad4c5\Runtime\Utils\MicrophoneRecord.cs(6,19):
error CS0234: The type or namespace name 'UI' does not exist in the namespace 'UnityEngine'

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
"dependencies": {
  "com.unity.ugui": "1.0.0"
}
```

The UI usages are all optional conveniences, so an alternative would be to move them behind
an assembly definition that references uGUI, keeping the core package dependency-free.
Declaring the dependency is the smaller change.

## Environment

- Unity 6000.5.2f1, Windows 11
- whisper.unity at commit `de9ce92ad4c5` (1.4.0)
- Building StandaloneWindows64

---END BODY---

- [ ] Click **Submit new issue**

### CHECK

- [ ] **Write the issue number here → #______**

You need it in Stage 3. It is in the page title and the URL.

---

# STAGE 3 — Prepare the pull request

**Time: 5 minutes**

Open a terminal. Run these one at a time.

```bash
gh repo fork Macoron/whisper.unity --clone --remote
```

```bash
cd whisper.unity
```

```bash
git checkout -b declare-ugui-dependency
```

### Now edit one file

- [ ] Open `Packages/com.whisper.unity/package.json`
- [ ] Find the line `"license": "MIT",`
- [ ] Add these four lines **directly below it**

```json
  "dependencies": {
    "com.unity.ugui": "1.0.0"
  },
```

It should end up looking like this:

```json
  "unity": "2020.1",
  "license": "MIT",
  "dependencies": {
    "com.unity.ugui": "1.0.0"
  },
  "licensesUrl": "https://github.com/Macoron/whisper.unity/blob/master/LICENSE.MD",
```

> **Why 1.0.0 and not a current version:** it is a minimum, not a pin. uGUI has shipped with
> every Unity in the supported range. A higher number would lock out editors that work fine.

### CHECK

- [ ] The file still has valid JSON. Every line ends in a comma **except** the last one
      before a closing brace.
- [ ] Run this. If it prints the version, the JSON is fine. If it errors, you have a comma
      in the wrong place.

```bash
node -e "console.log(require('./Packages/com.whisper.unity/package.json').dependencies)"
```

---

# STAGE 4 — Send it

**Time: 5 minutes**

```bash
git commit -am "Declare dependency on com.unity.ugui"
```

```bash
git push -u origin declare-ugui-dependency
```

- [ ] Open https://github.com/Macoron/whisper.unity/compare
- [ ] GitHub should offer a **Compare & pull request** button. Click it.

### Paste into the PR TITLE

```
Declare dependency on com.unity.ugui
```

### Paste into the PR BODY

**Replace `NNN` with your issue number from Stage 2.**

---BEGIN BODY---

Fixes the compile failure described in #NNN.

`MicrophoneRecord.cs` and `UiUtils.cs` use `UnityEngine.UI` types, but `package.json`
declares no dependencies, so the package fails to compile in projects that do not already
include `com.unity.ugui`. Most projects do, via the default 3D template, which is why this
is easy to miss.

Version floor set at `1.0.0` rather than a current version, since uGUI has shipped with
every Unity release in the supported range.

Tested on Unity 6000.5.2f1, Windows, StandaloneWindows64.

---END BODY---

- [ ] Click **Create pull request**

### Last step

- [ ] Go back to your issue from Stage 2
- [ ] Add a comment: `PR opened at #<your PR number>.`

**Done.**

---

# If something goes wrong

| What happened | What to do |
|---|---|
| `gh repo fork` says you already have a fork | Fine. Add `--force` or just `cd` into your existing clone |
| The `node -e` check errors | You have a comma in the wrong place. Compare against the sample block above |
| No **Compare & pull request** button appears | Go to your fork on github.com; it will be at the top of the page |
| A maintainer suggests a different fix | Say so in the thread and agree with them if they are right. A PR closed after a good discussion is still a contribution, and the issue stands alone |
| You want to back out | Close both with a short comment. Nothing is lost |

---

# What this is worth

The fix is four lines. That is not the point.

It is a real bug in a real package, found by building something, reported with a
reproduction and exact line numbers, with the fix attached. That is what an actual
open-source contribution looks like, and it is more convincing than a manufactured one
because it came out of the work.
