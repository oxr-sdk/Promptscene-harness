# promptscene-harness

A Claude Code plugin for **synthesizing and verifying rooms from natural-language prompts** in XRCollabDemo. It's a bundle of validated docs (the spec) and skills; the Unity project itself (`XRCollabDemo`) is **not** included in this repo (bring your own).

---

## Requirements

- **Claude Code** — the CLI/IDE agent (not claude.ai), with plugin support.
- **XRCollabDemo** — a local Unity project, **Unity 6** (verified on `6000.3.11f1`). The skills drive it as their target.
- **`ai-game-developer` MCP server** — connected to the running Unity Editor. The skills operate the Editor through it (scene build, play mode, reflection checks), so it must be live before you run a skill.
- **For device deploys (`deploy-client`)** — Unity **Android Build Support** (NDK/SDK/OpenJDK/IL2CPP) + `adb` for Quest/XReal. VisionOS additionally needs macOS + Xcode + PolySpatial.

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
| `/promptscene:assemble-room <RoomName>` | Assembles a ROOM scene and **live-proves it end-to-end** — applies the C1–C4 invariants, rebuilds `Room.exe`, starts the Master + Room servers, joins from an editor client, and verifies the §6.5 runtime signals (avatar spawns, lobby unloads, WASD-ready). |
| `/promptscene:deploy-client [Meta\|XReal\|Tablet\|Vision]` | Builds and deploys the client app for the target platform — applies the device preset, bundles the room scene, bakes in the master IP, builds via `BuildPipeline`, and (Android) installs + launches over `adb` and verifies the master connection. |

Run them in order: **assemble a room first**, then **deploy** that room to a device.

> Synthesizing an entire room from a single natural-language prompt — `/promptscene:compose-room` — is planned for roadmap Phase 5 (not yet implemented).

---

## Learn More

- **Design & architecture:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **Spec (SSOT):** [docs/promptscene-content-contract.md](docs/promptscene-content-contract.md)
