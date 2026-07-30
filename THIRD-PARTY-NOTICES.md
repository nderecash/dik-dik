# Third-party notices

This project is MIT licensed. See [LICENSE](LICENSE).

The components below are not mine and keep their own terms. All of them are permissive, and
none of them required a negotiation, an account or a fee, which was a hard constraint from
the start.

## Speech recognition

**whisper.unity**, by Macoron. MIT.
<https://github.com/Macoron/whisper.unity>

Unity bindings for whisper.cpp. Embedded in this repository at
`Packages/com.whisper.unity`, pinned to commit `de9ce92ad4c5`, rather than referenced as a
git dependency. That was not a preference: its assembly definition declared no platform
constraints, which Unity reads as every platform, so its native imports were compiled into
the WebGL build with no library to bind to and the build failed at link time. The package
cache is immutable, so the only way to set `excludePlatforms` was to embed it.

Everything difficult about running Whisper locally is theirs.

**whisper.cpp**, by Georgi Gerganov. MIT.
<https://github.com/ggerganov/whisper.cpp>

**Whisper model weights**, by OpenAI. MIT.
Not included in this repository, because a 74 MB binary does not belong in git history. The
setup script fetches them into `StreamingAssets`.

## Art and audio

**Kenney**, CC0 1.0 Universal.
<https://kenney.nl>

The rover, rocks, meteors, craters, hangars and satellite dishes come from Kenney's space
kit. CC0 requires no attribution. It is here because the work deserves it.

## Not third party

Two things that look like they might be, and are not:

**Every sound effect** in `Assets/Audio` is synthesised by `tools/make-sfx.ps1` using ffmpeg.
Tire roll, wind, scan tones, brake tick. Nothing is sampled from anywhere, which means the
whole soundtrack is reproducible from one script and none of it needs a licence.

**Every voice line** in `Assets/Resources/Voice` is performed by the author, then
radio-filtered by `tools/split-takes.ps1`.

Both are covered by the project licence.

## Tools used in the build, not shipped

**ffmpeg** for audio generation and processing, and **.NET** for the matcher test project.
Neither is redistributed here.
