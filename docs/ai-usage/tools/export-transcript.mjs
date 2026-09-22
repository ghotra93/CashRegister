// Exports the Claude Code session logs for this project into docs/ai-usage/transcript/:
//   - sessions/<id>.jsonl   raw logs, every entry, with personal identifiers redacted
//   - transcript.md         the same entries rendered as readable Markdown, in order
//
// Run from the repository root (re-run as the last step before submitting, so the export
// includes the latest conversation), passing the email address to redact:
//   AUTHOR_EMAIL="you@example.com" node docs/ai-usage/tools/export-transcript.mjs
//
// Redaction (the only change made to the logs): the author's email address, Windows user
// name and machine name are replaced with placeholders. Nothing else is removed or edited.
// The values are read at run time (environment variable, OS user, host name), so this file
// never contains them.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const projectLogs = path.join(os.homedir(), '.claude', 'projects', 'c--dev-CashRegister');
const outDir = path.join('docs', 'ai-usage', 'transcript');

const email = process.env.AUTHOR_EMAIL;
if (!email) {
  console.error('Set AUTHOR_EMAIL to the email address that must be redacted.');
  process.exit(2);
}

const escape = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const redactions = [
  [new RegExp(escape(email), 'gi'), '<author-email>'],
  [new RegExp(escape(os.hostname()), 'gi'), '<machine-name>'],
  [new RegExp(escape(os.userInfo().username), 'g'), '<windows-user>'],
];
const redact = (text) => redactions.reduce((acc, [pattern, placeholder]) => acc.replace(pattern, placeholder), text);

const sessionFiles = fs
  .readdirSync(projectLogs)
  .filter((f) => f.endsWith('.jsonl'))
  .map((f) => path.join(projectLogs, f))
  .sort((a, b) => fs.statSync(a).birthtimeMs - fs.statSync(b).birthtimeMs);

fs.mkdirSync(path.join(outDir, 'sessions'), { recursive: true });

const fence = (text) => {
  const body = String(text ?? '');
  const ticks = body.includes('```') ? '````' : '```';
  return `${ticks}\n${body}\n${ticks}`;
};

const md = [];
const summary = [];
for (const file of sessionFiles) {
  const raw = redact(fs.readFileSync(file, 'utf8'));
  const id = path.basename(file, '.jsonl');
  fs.writeFileSync(path.join(outDir, 'sessions', `${id}.jsonl`), raw);

  const entries = raw
    .split('\n')
    .filter(Boolean)
    .map((line) => {
      try {
        return JSON.parse(line);
      } catch {
        return { type: 'unparseable', raw: line };
      }
    });
  const stamps = entries.map((e) => e.timestamp).filter(Boolean);
  summary.push(`| \`${id}\` | ${stamps[0] ?? '?'} | ${stamps.at(-1) ?? '?'} | ${entries.length} |`);
  md.push(`\n# Session ${id}\n\n${entries.length} log entries.\n`);

  for (const e of entries) {
    const when = e.timestamp ? ` · ${e.timestamp}` : '';
    if (e.type === 'user' || e.type === 'assistant') {
      const content = e.message?.content;
      const blocks = typeof content === 'string' ? [{ type: 'text', text: content }] : (content ?? []);
      for (const b of blocks) {
        if (b.type === 'text') md.push(`\n## ${e.type === 'user' ? 'User' : 'Assistant'}${when}\n\n${b.text}\n`);
        else if (b.type === 'thinking') md.push(`\n### Assistant (reasoning)${when}\n\n${fence(b.thinking)}\n`);
        else if (b.type === 'redacted_thinking') md.push(`\n### Assistant (reasoning, redacted by the platform)${when}\n`);
        else if (b.type === 'tool_use') md.push(`\n### Tool call: ${b.name}${when}\n\n${fence(JSON.stringify(b.input, null, 2))}\n`);
        else if (b.type === 'tool_result') {
          const text = Array.isArray(b.content)
            ? b.content.map((c) => (c.type === 'text' ? c.text : `[${c.type}]`)).join('\n')
            : b.content;
          md.push(`\n### Tool result${b.is_error ? ' (error)' : ''}${when}\n\n${fence(text)}\n`);
        } else md.push(`\n### ${e.type} block: ${b.type}${when}\n\n${fence(JSON.stringify(b, null, 2))}\n`);
      }
    } else {
      md.push(`\n### Log entry: ${e.type}${when}\n\n${fence(JSON.stringify(e))}\n`);
    }
  }
}

const header = `# Full AI session transcript

Rendered from the raw Claude Code session logs in \`sessions/\`: every entry, in order. That includes
user prompts, assistant replies, the assistant's reasoning where the log contains it, every tool call and
tool result, and the platform's own log entries.

**Redaction:** the author's email address, Windows user name and machine name are replaced with
\`<author-email>\`, \`<windows-user>\` and \`<machine-name>\`. Nothing else was removed or edited.

Exported ${new Date().toISOString()} with \`node docs/ai-usage/tools/export-transcript.mjs\`.

| Session | First entry | Last entry | Entries |
|---|---|---|---|
${summary.join('\n')}
`;

fs.writeFileSync(path.join(outDir, 'transcript.md'), header + md.join(''));
console.log(`exported ${sessionFiles.length} session(s) to ${outDir}`);
