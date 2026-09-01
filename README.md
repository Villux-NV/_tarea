# tarea

a retro terminal task manager built with rooms and cards.

![tarea screenshot](screenshots/hero.png)

tarea is a desktop kanban app with a crt aesthetic — monospace type, scanline overlays, typewriter effects, and a boot sequence. it runs on windows and macos.

tasks live inside **cards**, cards live inside **rooms**. a room is a project, a card is a task, and each card holds notes, a status, urgency, and a due date.

---

## notes

each card has its own note list. notes are more than plain text — they support selection, inline editing, and drag reordering.

**long-press** a note to cross it out without deleting it.  
**drag** notes between cards to reorganize across tasks.

<!-- clip: long-press a note to cross out, then drag a note from one card to another -->

![notes demo](screenshots/notes.gif)

---

## status cycling + auto-hide

click a card's status badge to cycle through **todo → wip → done**. when a card hits done, it can automatically hide with a green pulse animation — the completed card collapses to a small `[x]` icon in the status bar. click the icon to bring it back.

the hide delay, pulse animation, and auto-hide behavior are all configurable in settings.

<!-- clip: click status badge a few times to cycle through, show card pulse-hide on done, click [x] to restore -->

![status cycling](screenshots/status-cycling.gif)

---

## urgency tinting

cards have a four-level urgency system: **none, low, medium, high**. higher urgency adds a subtle background tint to the card face, making urgent tasks visible at a glance without breaking the aesthetic.

<!-- clip: cycle through urgency levels on a card, show the background tint changing -->

![urgency](screenshots/urgency.gif)

---

## ratio bar

the status bar at the bottom of each room includes an ascii progress bar that shows the proportion of todo, wip, and done cards at a glance.

```
[────────────│──────────│──────────────────────────]
```

<!-- clip: short clip of the ratio bar updating as you cycle a card's status -->

![ratio bar](screenshots/ratio-bar.gif)

---

## themes

three built-in presets plus a full custom palette editor.

**rose** — the default. rose text on near-black, warm and readable.  
**amber** — monochrome phosphor monitor. everything in amber and gold.  
**integrale** — inspired by the lancia delta hf. cream, navy, and martini racing colors.

the custom editor exposes all eight palette keys with live preview. save and name as many custom themes as you want.

<!-- clip: switch between rose, amber, integrale in settings, then tweak a custom color -->

![themes](screenshots/themes.gif)

---

## crt effects

the retro layer is built from individual effects, each with its own toggle:

- **scanlines** — horizontal line overlay
- **vignette** — darkened edges
- **boot sequence** — terminal startup animation on launch
- **typewriter footer** — text reveals character by character on navigation
- **hover scale** — cards grow slightly on mouseover
- **done pulse** — green flash when a card completes

there's a master animations toggle to turn everything off at once.

<!-- clip: toggle scanlines/vignette on and off, show boot sequence from cold start -->

![crt effects](screenshots/crt-effects.gif)

---

## marquee scroll

card and room titles that overflow their container scroll on hover — a back-and-forth marquee with eased timing. no tooltip needed, no truncation lost.

<!-- clip: hover a long card title, show it scrolling -->

![marquee](screenshots/marquee.gif)

---

## keyboard shortcuts

all shortcuts are rebindable in settings:

| default | action |
|---------|--------|
| `n` | quick add card/room |
| `s` | open settings |
| `esc` | go back |
| `/` | search |

<!-- clip: press N to open quick add, type a title, press enter -->

![shortcuts](screenshots/shortcuts.gif)

---

## export

export all rooms and cards to a structured markdown file. statuses map to checkboxes, crossed-out notes render as strikethrough, and metadata (due dates, urgency) is preserved.

```markdown
# project name

## TODO
- [ ] card title
  - note one
  - ~~crossed out note~~
  due: 2026-09-15 | urgency: high
```

---

## settings

card dimensions, font size, status labels, due date warning threshold, delete confirmations, and completion behavior are all configurable. everything persists to a local json file.

---

## install

download the latest release for your platform:

- **windows** — `tarea-win-x64.zip`
- **macos (intel)** — `tarea-osx-x64.zip`
- **macos (apple silicon)** — `tarea-osx-arm64.zip`

unzip and run. no installer needed. data is stored in your local app data folder.

---

## build from source

```
git clone https://github.com/Villux-NV/tarea
cd tarea
dotnet build
dotnet run
```

requires [.net 8 sdk](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## stack

c# / .net 8 / avalonia ui / communitytoolkit.mvvm

