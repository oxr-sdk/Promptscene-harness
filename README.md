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
>
> **Note (history):** studio previously pinned **`0.66.0`** because UnifiedXRMotion 1.8.5's MCP adapter referenced `…Runtime.Data.GameObjectRef`, a symbol that existed only in the **0.66.0–0.67.3** window (from `0.68.0` the namespace was renamed to `AIGD`). UnifiedXRMotion **1.9.0** tracks the current namespace, so `0.86.1` compiles and runs — the `0.66.0` constraint no longer applies. See `promptscene/docs/xumflow-migration.md` §7 for the measurement trail.

Other live Unity MCP servers you could substitute (not verified here):

- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) — bridge for managing assets, scenes, scripts; actively maintained (formerly `justinpbarnett/unity-mcp`, now transferred here).

---

## Quick Start

### Step 1: Install the Plugin

Official install (via the marketplace):

```
/plugin marketplace add oxr-sdk/Promptscene-harness
/plugin install promptscene@promptscene-harness
```

Dev / temporary load (this session only):

```
claude --plugin-dir .
```

After editing a skill, run `/reload-plugins` to pick up the changes.

### Step 2: Run a skill

Skills are invoked with the plugin namespace: `/promptscene:<skill>`.

| Skill | What it does |
|---|---|
| `/promptscene:assemble-room <RoomName>` | Assembles an empty ROOM **skeleton** and **live-proves it** — clones a sample room, registers it, builds the 5 layers (SYSTEMS + ENVIRONMENT + UI + empty FEATURES + empty COMPOSITIONS), then QuickTests the **§6.5** runtime signals (avatar spawns, RoomCore up with 4 services + empty registry, WASD-ready). Skeleton only — no feature content. |
| `/promptscene:add-component <request> [on <Room>]` | Puts a **component (a FEATURE or a COMPOSITION) onto a room** and completes the full cycle — consults (classifies FEATURE vs COMPOSITION, judges buildability), gets the component (reuse / AI-generate from the Ruler template / wire a human script), places it under the right layer with prefab wiring, then QuickTest-proves **§5** (self-registers + `SetEnabled` exception-free + valid meta) **and §6.5** (avatar still spawns = SYSTEMS unbroken). |

Run them in order: **assemble a room skeleton first**, then **add components** onto it.

---

## Learn More

- **Design & architecture:** [promptscene/docs/ARCHITECTURE.md](promptscene/docs/ARCHITECTURE.md)
- **Spec (SSOT):** [promptscene/docs/promptscene-content-contract.md](promptscene/docs/promptscene-content-contract.md)
- **한국어:** [promptscene/docs/KOR/README.MD](promptscene/docs/KOR/README.MD)
