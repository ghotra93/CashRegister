// Builds AC -> tests traceability from [Trait("AC", ...)] (C#) and "[AC-NNN]" test names (TS).
// Invoked by .github/scripts/traceability-dotnet.sh.
import fs from 'node:fs';
import path from 'node:path';

const featureId = process.argv[2];
const specDir = `.specs/${featureId}`;
const spec = fs.readFileSync(`${specDir}/01-spec.md`, 'utf8');
const tasks = JSON.parse(fs.readFileSync(`${specDir}/.tdd-state.json`, 'utf8')).tasks;

const acTexts = new Map();
for (const m of spec.matchAll(/^- (AC-\d{3}): (.+)$/gm)) acTexts.set(m[1], m[2]);

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (['bin', 'obj', 'node_modules', 'dist'].includes(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

const testsByAc = new Map([...acTexts.keys()].map((ac) => [ac, []]));
const orphans = [];

// C#: attributes above a method; collect AC traits until the method signature.
for (const file of walk('tests').filter((f) => f.endsWith('.cs'))) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  let pending = [];
  let isTest = false;
  for (const line of lines) {
    if (/\[(Fact|Theory)\b/.test(line)) isTest = true;
    const ac = line.match(/\[Trait\("AC", "(AC-\d{3})"\)\]/);
    if (ac) pending.push(ac[1]);
    const method = line.match(/public (?:async )?(?:Task|void|ValueTask) (\w+)\(/);
    if (method && isTest) {
      const name = `${path.basename(file, '.cs')}.${method[1]}`;
      for (const id of pending) {
        if (testsByAc.has(id)) testsByAc.get(id).push(name);
        else orphans.push(`${name} → ${id} (not an active AC)`);
      }
      pending = [];
      isTest = false;
    }
  }
}

// TypeScript: it('[AC-NNN] ...').
for (const file of walk('web/src').filter((f) => /\.test\.tsx?$/.test(f))) {
  const text = fs.readFileSync(file, 'utf8');
  for (const m of text.matchAll(/it\('\[(AC-\d{3})\] ([^']+)'/g)) {
    const name = `${path.basename(file)} › ${m[2]}`;
    if (testsByAc.has(m[1])) testsByAc.get(m[1]).push(name);
    else orphans.push(`${name} → ${m[1]} (not an active AC)`);
  }
}

const tasksByAc = new Map();
for (const [id, t] of Object.entries(tasks)) for (const ac of t.acs_covered) tasksByAc.set(ac, [...(tasksByAc.get(ac) ?? []), id]);

const uncovered = [...testsByAc].filter(([, t]) => t.length === 0).map(([ac]) => ac);

const rows = [...testsByAc].map(([ac, names]) => {
  const shown = names.slice(0, 4).map((n) => `\`${n}\``).join('<br>');
  const more = names.length > 4 ? `<br>… +${String(names.length - 4)} more` : '';
  return `| ${ac} | ${(tasksByAc.get(ac) ?? []).join(', ') || '—'} | ${String(names.length)} | ${shown}${more} |`;
});

const md = `# Traceability: ${featureId}

> Generated ${new Date().toISOString().slice(0, 10)} by \`.github/scripts/traceability-dotnet.sh\`.
> Sources: \`[Trait("AC", "AC-NNN")]\` on xUnit tests; \`[AC-NNN]\` prefixes on Vitest test names; \`acs_covered\` in \`.tdd-state.json\`.

## Summary

- Active ACs: ${String(testsByAc.size)} (AC-014 withdrawn → DC-001)
- ACs with at least one tagged test: ${String(testsByAc.size - uncovered.length)}
- Uncovered ACs: ${uncovered.length ? uncovered.join(', ') : 'none'}
- Orphaned tags (tag names a non-active AC): ${orphans.length ? orphans.length : 'none'}

## Matrix

| AC | Tasks | Tests | Tagged tests (first 4) |
|---|---|---|---|
${rows.join('\n')}
${orphans.length ? `\n## Orphans\n\n${orphans.map((o) => `- ${o}`).join('\n')}\n` : ''}`;

fs.writeFileSync(`${specDir}/07a-traceability.md`, md);
process.exitCode = uncovered.length === 0 ? 0 : 1;
console.log(`ACs ${String(testsByAc.size)}, uncovered: ${uncovered.join(',') || 'none'}, orphans: ${String(orphans.length)}`);
for (const [ac, n] of testsByAc) console.log(ac, n.length);
