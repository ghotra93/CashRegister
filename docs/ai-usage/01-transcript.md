# 1. Full prompt transcript

The complete conversation with the AI tool (Claude Code, model Claude Opus 5), exported from Claude Code's own session logs.

| File | What it is |
|---|---|
| [transcript/transcript.md](transcript/transcript.md) | Readable rendering: every entry in order, including prompts, replies, the assistant's reasoning where logged, and every tool call and result |
| [transcript/sessions/](transcript/sessions/) | The raw session logs (`.jsonl`), one file per session, as Claude Code recorded them |

## Sessions

| Session | When | Content |
|---|---|---|
| `0cfa1c3c…` | 2026-09-21, about 2 minutes | A single prompt about the spec commands, interrupted by the developer |
| `28cd750f…` | 2026-09-21 to 2026-09-22 | The whole project: README → spec → design → 18 TDD tasks → test hardening → harness → validation → review → ship plan → Docker, README and this documentation |

## Redaction

The only change to the logs: the author's email address, Windows user name and machine name are replaced with `<author-email>`, `<windows-user>` and `<machine-name>`. Nothing else was removed or edited.

## Re-exporting

The session was still running when this was exported, so run the export again as the very last step before submitting. That captures the end of the conversation:

```bash
AUTHOR_EMAIL="<your email>" node docs/ai-usage/tools/export-transcript.mjs
```

Claude Code's built-in `/export` command gives an alternative, plain-text export of the current conversation.
