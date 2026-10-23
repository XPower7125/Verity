# Verity

A desktop pet on OpenRouter. A draggable smiley sits on your desktop; clicking it
opens a chat bubble. Ask questions, or paste a screenshot straight into the
composer with `Ctrl+V`.

![Verity sitting on a desktop](images.jpg)

## What it does

- Text chat through OpenRouter.
- Optional web search through Exa. Click `web` in the chat header to turn it on;
  Verity searches the web before answering and cites the sources it used.
- Paste images from the clipboard (`Ctrl+V`) alongside a question. A thumbnail
  appears in the composer; press the small `x` to remove it. An image with no
  caption is sent as "What's in this image?".
- Remembers the pet's position and your settings between launches.

The only real costs are the API calls billed to your OpenRouter and Exa keys.

## Build

```
dotnet build Verity.sln -c Release
dotnet publish src\Verity\Verity.csproj -c Release -o publish
```

The published `publish\Verity.exe` is the app. Needs the .NET 8 SDK with the
Windows Desktop workload.

## Look

Bonzy Buddy-ish: saturated purple, chunky rounded panels with heavy outlines, yellow
accent bubbles, Comic Sans MS as the chat face. Colors and fonts live in
`shared/Theme.cs`; `shared/BubbleLog.cs` paints the transcript as speech bubbles with
tails, user right and Verity left, and renders pasted images inline.

`Verity.exe --snapshot out.png chat|prompt|bubbles|pet|bubble` renders a window to a
PNG for layout checks without an API key. `Verity.exe --selftest` probes the pet
window's margin pixels and fails if any are opaque, which is how the transparency key
gets regression-tested (neither PrintWindow nor screen capture reports transparency
honestly).

## Key

Prompted on first launch, or set `OPENROUTER_API_KEY`. Model defaults to
`anthropic/claude-sonnet-4.5`; override with `VERITY_MODEL` or the text box. Web
search is optional: add an Exa key in the same dialog or set `EXA_API_KEY`.

The OpenRouter key, Exa key, model, and pet position are stored under
`HKCU\Software\Verity`. Edit them from the pet's right-click menu, or inspect
with `Verity.exe --dump-settings`. The keys are plaintext `REG_SZ` values; HKCU is
per-user by definition, so they are only readable by the account running Verity,
and unlike a file they cannot be synced between machines or committed by accident.

## Using it

- Drag the face to move it. The position is remembered.
- Click the face to open the chat bubble. ESC or the X closes it, leaving the pet.
- Right-click for chat, settings, and quit.
- Add an Exa API key to enable the `web` toggle, then click it before asking a
  question. The model gets the search results and is told to cite their URLs.
- Paste an image with `Ctrl+V` in the chat box, then send it with or without text.
- Hover any bubble for a copy button; `copy` in the header grabs the latest answer.
