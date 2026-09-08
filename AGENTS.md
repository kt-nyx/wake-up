# Repository operation boundaries

Read [current state](docs/current-state.md) before project work and [development](docs/development.md) before building or operating the fixture. Those documents describe the current product; archived plans are historical evidence, not additional approval gates.

Use the single checkout at `Z:\Development\Large Projects\RimWorld Loading Optimizer`. Start implementation from current `main` on a `codex/` branch unless the owner directs otherwise. Do not create another checkout or worktree. One task owns checkout changes, builds, deployment and live launches at a time; read-only reviewers may work alongside it without disruptive scans. Preserve unrelated edits and coordinate ownership before mutation.

Keep project output, decompiled research and raw local evidence under ignored `artifacts/`. The only game fixture is `.rlo-test-instance` in this checkout. Its game, mods, profiles, saves, caches, manifests and results are private ignored data. Never force-add fixture content. Use `git ls-files` or `rg --files` with ignore rules; do not recursively enumerate the repository with PowerShell or use `rg --no-ignore` / `rg -uuu` at its root. Address a specific ignored directory when needed.

Use `scripts/op7_fixture.py` for fixture operations. Never modify normal Steam, Workshop, profile, save or cache state. Live testing needs owner authorization; build, deployment or a metadata exception does not grant it. Session-specific overnight workload permission does not establish permanent permission for disruptive medium/full runs. Follow current owner scope and workload limits. Do not use UI automation, interfere with other applications or change security settings.

Keep the independent menu observer and normal automatic exit for routine comparisons. Use `menuReadyObservation.elapsedSeconds`, wait for actual process exit and capture, and check `automaticTestPassed`. Never start a second test while the first process remains alive. Necessary forced closure is limited to a verified fixture process after preserving failure evidence; it is not a successful test. Manual inspection requires explicit selection and a normal-exit report.

For DLL inspection prefer the existing read-only ILSpy CLI under `artifacts/`. Do not run inline PowerShell assembly-loading or reflective inspection commands, retry permission-blocked CPU profiling, or change security settings to enable them. Use the existing build tool for its approved package identity checks.

Use `git -c core.hooksPath=.githooks commit ...` for every commit. The tracked guard rejects fixture content. Do not push, publish or launch beyond the owner's authorization.

# Product scope and communication

Follow the [development policy](docs/development.md#development-policy). Prioritize shared RimWorld loading work across modlists. The owner also permits targeted improvements for individual mods when they remain soft dependencies: absent, changed or unsupported suppliers must preserve ordinary behavior, with exact identity checks and focused compatibility evidence. The fixture remains a representative workload. Preserve optional dependencies and ordinary fallback behavior.

Check recorded results before pursuing a related experiment. Do not repeat confirmed failures under the same relevant conditions. Reopening a route requires a materially different, evidence-backed premise that addresses its recorded failure; state what changed. Inconclusive tests and untested conditions are not confirmed failures of an entire technique. Outside these restrictions and the operational boundaries above, methods and architecture remain open-ended; current code and suggested next steps are not a fixed roadmap. Make routine decisions within existing task authorization without repeatedly asking permission.

Start explanations with what changed and why it matters. Explain unfamiliar technical terms when needed. Separate source, built package, deployed fixture and captured run facts; menu startup, semantic correctness, gameplay compatibility and speed are different claims. Do not apply the historical combined percentage to the reduced runtime, add overlapping timings or turn a few runs into a universal average.

Prefer the smallest reliable evidence for a product decision. Trust adequate existing validation. Do not expand harnesses, profiling or process documents without a concrete risk or improvement question. Record negative results and stop routes that fail to produce repeatable useful loading gains.
