# AGENTS.md

This file is the agent-instruction entry point recognized by Codex, GitHub Copilot, Cursor, Aider, and other AI coding tools.

**Source of truth: [CLAUDE.md](./CLAUDE.md)** — read it first.

Before touching Terminal.Gui UI code, also read:

- [docs/tui/README.md](./docs/tui/README.md) — Terminal.Gui v2 reference index
- [.claude/rules/terminal-gui-v2-patterns.md](./.claude/rules/terminal-gui-v2-patterns.md) — preferred v2 patterns
- [.claude/rules/async-tui-safety.md](./.claude/rules/async-tui-safety.md) — Application.Invoke / timer safety
- [.claude/rules/code-review-checklist.md](./.claude/rules/code-review-checklist.md) — pre-merge checklist
- [.claude/rules/file-size-limits.md](./.claude/rules/file-size-limits.md) — 300-line cap per .cs file

## Critical reminders

- **Terminal.Gui v2 is a complete rewrite.** Most pre-2025 training data is wrong. Verify every TG API against `docs/tui/` or current source before using.
- **Tests use TUnit, not xUnit.** Filter via `dotnet run --treenode-filter "/*/*/*ClassName/*"`, not `dotnet test --filter`.
- **No Co-Authored-By lines** in commits.
- **Conventional Commits** required — see `.claude/skills/smart-commit` (or scope table in CLAUDE.md).
