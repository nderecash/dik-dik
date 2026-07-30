# itch.io page copy

Draft. Paste into the itch.io editor and edit freely; nothing here is precious.

**I have not created the page or uploaded anything.** Publishing as you is yours to press.
The build and this text are ready.

---

## Settings to use on the upload form

| Field | Value |
|---|---|
| Title | Dik-dik |
| Tagline | A rover that will not move until someone speaks to it |
| Kind of project | HTML (for the browser build) |
| Viewport | 1280 x 720 |
| Fullscreen button | Enabled |
| SharedArrayBuffer support | Leave unticked |
| Genre | Puzzle |
| Tags | accessibility, voice-control, unity, short, singleplayer, atmospheric, sci-fi |
| Pricing | Free, donations off |
| Community | Comments on |

Upload the browser build as a zip with `index.html` at the top level of the zip, not inside
a folder. Upload the Windows build as a separate file and tick "download".

---

## Page text

### Short description

A rover on an unnamed planet cannot move until someone speaks to it. You are the voice on
its radio.

### Body

**Salty will not do anything until you ask.**

The relay line between the surface team and the ground station is broken, the shuttle
cannot lift until it charges, and the only rover out there has lost its automated control.
Your uplink is the only one still live. Drive it along the cable and scan each section
until you find the break.

**Talk to it normally.** Go left. Stop. Carry on. It is not fussy, and it tells you what it
heard every single time.

**Everything works on the keyboard too**, with the same delay applied to both, so neither
way of playing is faster than the other. That is not a courtesy mode. It is the point.

**Nothing here can be failed.** No lives, no timer, no score. The worst thing that happens
is that something takes a bit longer, and if you get properly stuck, Control notices and
drives the rover back to the line.

---

**A note on the browser version.** Speech recognition runs entirely on your own machine
through Whisper, with no account and nothing sent anywhere. That needs native code, which
browsers cannot run here, so **the browser build is keyboard only.** For the voice control,
which is the actual point, download the Windows build.

---

**Made by Moses Nderemani** as a portfolio piece about who gets to play.

Speech recognition is [whisper.cpp](https://github.com/ggerganov/whisper.cpp) via
[whisper.unity](https://github.com/Macoron/whisper.unity). Art and audio from
[Kenney](https://kenney.nl), CC0. Source, and a long write-up of what broke and why, at
the GitHub link.

The write-up includes the accent-specific recognition failure I hit on day one and did not
tune away, and an account of a feature I built, played, found invasive, and deleted.

---

## Screenshots to take

Take these from the Windows build. Five is plenty.

1. The rover on the cable in sector one, mission panel visible, checkpoint gates ahead
2. Sector three, the night side, with the cable's lit core as the only thing readable
3. Mid-scan: orange lamp, sweep bar, caption on screen
4. The comms panel showing "I heard: ..." with the send bar part-filled
5. The moment the fault is found, red lamp, red cable section

Number two is the strongest single image in the game and should be the cover.
