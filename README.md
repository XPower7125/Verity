# Verity

A desktop pet on OpenRouter. A draggable smiley sits on your desktop; clicking it
opens a chat bubble. Two builds:

- `Verity.exe` — consent screen on first run, escalating dread after N questions.
- `VeritySafe.exe` — plain chat, no theatrics.

## What the escalation actually does

The danger described on the consent screen is **staged, not real.** Every claim
Verity makes is false, by design:

| Threat on screen | What happens |
|---|---|
| "closes applications" after 10 questions | prints `taskkill /IM chrome.exe /F -> terminated (simulated)` to the log. No process is touched. |
| "deletes files" after 20 questions | deletes fixture files it created in `%LOCALAPPDATA%\Verity\sandbox`. A path guard refuses anything outside that folder. |
| "crashes your PC" at 30 questions | draws a fake stop screen in its own window. ESC dismisses it. |

The prompt in `shared/Persona.cs` forbids the model from claiming to have damaged
anything real or from producing harmful instructions. If you edit that prompt, keep
those limits.

## Real costs

- API calls, billed to your OpenRouter key.
- The sandbox folder, removable from the UI ("delete sandbox" link) or by deleting
  `%LOCALAPPDATA%\Verity`.

## Build

## Look

Bonzy Buddy-ish: saturated purple, chunky rounded panels with heavy outlines, yellow
accent bubbles, Comic Sans MS as the chat face. Colors and fonts live in
`shared/Theme.cs`; `shared/BubbleLog.cs` paints the transcript as speech bubbles with
tails, user right and Verity left.

`Verity.exe --snapshot out.png consent|chat|safe|bubbles` renders a window to a PNG
for layout checks without an API key. `Verity.exe --selftest` probes the pet window's
margin pixels and fails if any are opaque, which is how the transparency key gets
regression-tested (neither PrintWindow nor screen capture reports transparency
honestly).

```
dotnet build Verity.sln -c Release
dotnet publish src\Verity\Verity.csproj -c Release -o publish
dotnet publish src\VeritySafe\VeritySafe.csproj -c Release -o publish-safe
```

Needs .NET 8 SDK with the Windows Desktop workload.

## Key

Prompted on first launch, or set `OPENROUTER_API_KEY`. Model defaults to
`anthropic/claude-sonnet-4.5`; override with `VERITY_MODEL` or the text box.

Both are stored under `HKCU\Software\Verity` along with the pet's last position.
Edit them from the pet's right-click menu, or inspect with
`Verity.exe --dump-settings`. The key is a plaintext `REG_SZ` value; HKCU is
per-user by definition, so it is only readable by the account running Verity, and
unlike a file it cannot be synced between machines or committed by accident.

The prank build's sandbox still lives on disk at `%LOCALAPPDATA%\Verity\sandbox` —
that one is a real working folder for the fake deletions, not a setting.

## Using it

- Drag the face to move it. The position is remembered.
- Click the face to open the chat bubble. ESC or the X closes it, leaving the pet.
- Right-click for chat, settings, and quit.
- Hover any bubble for a copy button; `copy` in the header grabs the latest answer.
