// New-code coverage: of the src/**/*.cs lines added or changed since BASE_REF, how many that
// the coverage report counts as executable were hit by the unit run.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import { readMerged } from './cobertura.mjs';

const repoRoot = process.cwd();
const baseRef = process.env.BASE_REF ?? 'origin/main';
const threshold = Number(process.env.NEW_CODE_THRESHOLD ?? '0.95');
const report = 'artifacts/coverage/cobertura.xml';

if (!fs.existsSync(report)) {
  console.error(`missing ${report}; run ./.github/scripts/harness-dotnet.sh first`);
  process.exit(2);
}

const diff = spawnSync('git', ['diff', '--unified=0', `${baseRef}...HEAD`, '--', 'src/**/*.cs', 'src/*.cs'], {
  encoding: 'utf8',
  maxBuffer: 64 * 1024 * 1024,
});
if (diff.status !== 0) {
  console.error(diff.stderr);
  process.exit(2);
}

// file -> set of changed line numbers (new side of the diff)
const changed = new Map();
let current = null;
for (const line of diff.stdout.split('\n')) {
  const file = line.match(/^\+\+\+ b\/(.+)$/);
  if (file) {
    current = file[1];
    changed.set(current, changed.get(current) ?? new Set());
    continue;
  }
  const hunk = line.match(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/);
  if (hunk && current) {
    const start = Number(hunk[1]);
    const count = hunk[2] === undefined ? 1 : Number(hunk[2]);
    for (let n = start; n < start + count; n++) changed.get(current).add(n);
  }
}

const coverage = readMerged([report], repoRoot);
let executable = 0;
let covered = 0;
const uncovered = [];
for (const [file, lines] of changed) {
  const fileCoverage = coverage.get(file);
  if (!fileCoverage) continue;
  for (const n of lines) {
    const entry = fileCoverage.get(n);
    if (!entry) continue; // not an executable line
    executable++;
    if (entry.hits > 0) covered++;
    else uncovered.push(`${file}:${n}`);
  }
}

const rate = executable === 0 ? 1 : covered / executable;
const result = {
  base_ref: baseRef,
  threshold,
  changed_executable_lines: executable,
  covered_lines: covered,
  new_code_line: Number(rate.toFixed(4)),
  status: rate >= threshold ? 'pass' : 'fail',
  uncovered: uncovered.slice(0, 200),
};
if (executable === 0) {
  result.note = `no executable src/**/*.cs lines changed since ${baseRef}`;
}
fs.writeFileSync('artifacts/new-code-coverage.json', `${JSON.stringify(result, null, 2)}\n`);
console.log(`new-code coverage vs ${baseRef}: ${(100 * rate).toFixed(2)}% (${covered}/${executable}) → ${result.status}`);
process.exitCode = result.status === 'pass' ? 0 : 1;
