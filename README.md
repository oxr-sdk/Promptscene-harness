# promptscene-harness

A Claude Code plugin for **synthesizing and verifying rooms from natural-language prompts** in **XumFlow:studio**. It's a bundle of validated docs (the spec) and skills; the Unity project itself is **not** included in this repo (bring your own).

---

## Requirements

- **Claude Code** — the CLI/IDE agent (not claude.ai), with plugin support.
- **XumFlow:studio** — a local Unity content-authoring project, **Unity 6** (verified on `6000.3.11f1`). The skills drive it as their target.
- **A Unity MCP server** — connected to the running Unity Editor. The skills operate the Editor through it (scene build, play mode, reflection checks), so it must be live before you run a skill. Any Unity MCP server works; the choice below is what we actually use.

### Unity MCP server

We use and have live-verified **[IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)** ⭐ (`com.ivanmurzak.unity.mcp`).

> **Version pin (recommended for IvanMurzak/Unity-MCP):** pin to **`0.86.1`** (the current `latest`) — now live-verified in studio against **UnifiedXRMotion 1.9.0**. Verification covered the full path: MCP reinstall + compile check (incl. NuGet DLL sync) + 5-layer QuickTest, all green.

Other live Unity MCP servers you could substitute (not verified here):

- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) — bridge for managing assets, scenes, scripts; actively maintained (formerly `justinpbarnett/unity-mcp`, now transferred here).

---

## Quick Start

### Step 1: Install the Plugin

There are two ways to load this plugin. Pick **A** if you just want to *use* the skills, **B** if you're *editing* them.

#### A. Official install — via the marketplace (persistent)

Run these two slash commands inside Claude Code:

```
/plugin marketplace add oxr-sdk/Promptscene-harness
/plugin install promptscene@promptscene-harness
```

| Command | What it actually does |
|---|---|
| `/plugin marketplace add oxr-sdk/Promptscene-harness` | Registers a **plugin source**. `oxr-sdk/Promptscene-harness` is GitHub `owner/repo` shorthand — Claude Code fetches that repo and reads its [`.claude-plugin/marketplace.json`](.claude-plugin/marketplace.json), a catalog declaring which plugins the repo publishes (here: one, named `promptscene`). This step **installs nothing** — it only makes the catalog known. |
| `/plugin install promptscene@promptscene-harness` | Installs the plugin itself. The syntax is `<plugin>@<marketplace>`: `promptscene` is the plugin name from the catalog, `promptscene-harness` is the marketplace name you just added (it's the `name` field in `marketplace.json`, **not** the repo name's casing). Once installed, its skills are callable as `/promptscene:<skill>`. |

Installed plugins persist across sessions. `/plugin` on its own opens the interactive manager (browse / enable / disable / uninstall).

#### B. Dev / temporary load — straight from a local checkout (this session only)

From the repo root:

```
claude --plugin-dir .
```

This points Claude Code at a **local directory** as a plugin source: no marketplace, no install step, no git round-trip. It lasts only for that session — quit and it's gone. Use it when you're authoring or modifying skills, since your edits on disk are what gets loaded.

After editing a skill, run `/reload-plugins` to pick up the changes without restarting.

> **Which one?** A = consuming the released skills. B = developing them. Don't run both at once — you'd load two copies of the same plugin.

### Step 2: Run a skill

Skills are invoked with the plugin namespace: `/promptscene:<skill>`.

| Skill | What it does |
|---|---|
| `/promptscene:assemble-room <RoomName>` | Assembles an empty ROOM **skeleton** and **live-proves it** — clones a sample room, registers it, builds the 5 layers (SYSTEMS + ENVIRONMENT + UI + empty FEATURES + empty COMPOSITIONS), then QuickTests the **§6.5** runtime signals (avatar spawns, RoomCore up with 4 services + empty registry, WASD-ready). Skeleton only — no feature content. |
| `/promptscene:add-component <request> [on <Room>]` | Puts a **component (a FEATURE or a COMPOSITION) onto a room** and completes the full cycle — consults (classifies FEATURE vs COMPOSITION, judges buildability), gets the component (reuse / AI-generate from the Ruler template / wire a human script), places it under the right layer with prefab wiring, then QuickTest-proves **§5** (self-registers + `SetEnabled` exception-free + valid meta) **and §6.5** (avatar still spawns = SYSTEMS unbroken). |

Run them in order: **assemble a room skeleton first**, then **add components** onto it.

### Demo

[**▶ `/promptscene:add-component` — a full run**](promptscene/docs/videos/screencast-2026-07-27-8x.mp4) · 2 min · 8× speed · no audio

A screen recording of the whole cycle in XumFlow:studio: putting a chat feature — including a Meta system-keyboard binding — onto a room called `AssembleRoom`. It covers the prompt, the slice-by-slice buildability grading, authoring the VR chat surface, placing it under `FEATURES`, the live QuickTest in Play mode, and the closing handoff back to the human.

---

## Learn More

- **Design & architecture:** [promptscene/docs/ARCHITECTURE.md](promptscene/docs/ARCHITECTURE.md)
- **Spec (SSOT):** [promptscene/docs/promptscene-content-contract.md](promptscene/docs/promptscene-content-contract.md)
- **한국어:** [promptscene/docs/KOR/README.MD](promptscene/docs/KOR/README.MD)
