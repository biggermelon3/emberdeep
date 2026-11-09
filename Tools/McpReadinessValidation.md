# Startup repair validation

Recorded 2026-10-18. Entry point:

```powershell
powershell -NoProfile -File E:/Unity/Tools/Complete-McpSetup.ps1
```

Use `-CheckOnly` for diagnosis without starting the Unity server/editor.
Use `-RequireDevice -DeviceSerial <serial>` for device work.
Exit zero means external checks passed; the startup skill also requires
read-only calls through the active Codex connections.

## Successfully verified

- Installed Meta CLI reports `metavr 1.6.0.14.0`.
- Updated Quest launcher completes MCP initialize, tools/list, device discovery,
  and an information query for authorized Quest 3 `2G0YC5ZG5V04ZW`.
- Active Codex `metavr_device` calls for list and explicit-target info succeed.
- Skill-creator `quick_validate.py` reports `Skill is valid!`.
- Check-only returns failure when Unity MCP is unreachable, rather than
  declaring readiness after the Quest checks succeed.
- Five unit tests pass (including simulated absent/unauthorized/multiple-device
  selection, wrong/ambiguous Unity instances, MCP error payloads, launch denial,
  nonzero exits, and bounded timeout termination). These are simulated cases,
  not live hardware validation.
- External CheckOnly and repeated full startup pass, including MCP handshake,
  explicit selection of `MetaHackthone@4635608ab537c79c`, exact project root and
  Unity `6000.3.21f1`, ready editor state, and active scene `NarrowGap`.
- Repeated startup with the editor open preserves the observed process IDs:
  server 39520 and Unity processes 41120, 42556, 43648. No duplicate process was
  observed. Logs: `E:/Unity/Tools/mcp-readiness-check.log` and
  `E:/Unity/Tools/mcp-readiness-repeat.log`.
- Live forced script compilation/reload passes: reload timestamp advances,
  expected editor reconnects and becomes ready, active scene and project info
  remain identical. Script: `E:/Unity/Tools/test_mcp_reload.py`; output:
  `E:/Unity/Tools/mcp-reload-test.log`.

## Blockers and outstanding validation

- Follow-up session verification passed on 2026-10-18: active Codex Unity tools
  are available. Instances discovery, explicit selection, exact project info,
  ready editor state, and active-scene retrieval all succeed through Codex.
  Android remains the build target and NarrowGap the active clean scene.
- Windows denied CIM process enumeration. Startup now uses Get-Process and a
  bounded TCP probe instead; it does not require CIM.
- Windows subsequently denied new shell launches with
  `CreateProcessAsUserW failed: 5 (Access is denied.)`. One later launch allowed
  `E:/Unity/Tools/test_mcp_readiness.py` to pass, but launch denial recurred on
  the following process/log inspection. Shell execution remains intermittent.
- A full startup diagnostic was interrupted after exceeding the intended wait.
  Inspection found inherited output pipes could retain background-child handles.
  The runner now redirects output to temporary files and bounds process waits;
  its timeout behavior passes the simulated test. Live cold startup is unverified.
- Clean cold-start completion, normal Hub-open startup,
  and build-target preservation need live confirmation. The interrupted first
  run did start the server/editor, but is not recorded as a successful cold run.
- No-headset, unauthorized-headset, and multiple-headset scenarios need live
  confirmation. Only one authorized Quest was available for the successful check.

After restoring shell launch access, run the unit tests with the pinned Python,
run startup twice and compare server/editor process IDs, run CheckOnly, and
verify the current project, scene, and platform through MCP. Exercise editor
reload without scene changes and repeat readiness. Do not close an editor with
unsaved work to manufacture a cold-start test.

In the desktop app choose Settings > MCP servers > Restart, then open a fresh
project session and perform the skill's read-only calls. In CLI exit/relaunch
codex and use `/mcp`; in the IDE use MCP servers > Restart extension.

The required current-session readiness gate now passes. Remaining live scenario
coverage above is not claimed complete. Work on pinch-only release has resumed.
