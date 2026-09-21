// Harness gate runner. Invoked by .github/scripts/harness-dotnet.sh.
//
// Pre-gates (a failure stops the run): format, compile.
// Gates: unit (+ coverage), it, coverage, mutation, contract, security, web.
// Output: artifacts/harness-summary.json (and on stdout with --report).
//
// This file and .claude/skills/dotnet-build-harness/references/props-fragments.md are the only
// places the canonical `dotnet test` invocation may appear.
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { readMerged, totals, writeMerged } from './cobertura.mjs';

const repoRoot = process.cwd();
const solution = 'CashRegister.slnx';
const artifacts = path.join(repoRoot, 'artifacts');
const testResults = path.join(artifacts, 'test-results');
const coverageDir = path.join(artifacts, 'coverage');
const coverageFloor = Number(process.env.COVERAGE_FLOOR ?? '0.90');
const baseRef = process.env.BASE_REF ?? 'origin/main';
const emitReport = process.argv.includes('--report');

const log = (message) => process.stderr.write(`[harness] ${message}\n`);

function run(command, args, options = {}) {
  log(`$ ${command} ${args.join(' ')}`);
  const result = spawnSync(command, args, { encoding: 'utf8', shell: process.platform === 'win32', ...options });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  return { code: result.status ?? 1, output };
}

function clean(dir) {
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
}

function testProjects() {
  const tests = path.join(repoRoot, 'tests');
  return fs
    .readdirSync(tests, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => path.join(tests, d.name, `${d.name}.csproj`))
    .filter((p) => fs.existsSync(p));
}

/** Sums the <Counters> of every TRX file in a directory. */
function trxCounts(dir) {
  const counts = { tests: 0, passed: 0, failures: 0, errors: 0, skipped: 0 };
  if (!fs.existsSync(dir)) return counts;
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.trx'))) {
    const xml = fs.readFileSync(path.join(dir, file), 'utf8');
    const counters = xml.match(/<Counters ([^>]*)\/>/);
    if (!counters) continue;
    const attr = (name) => Number((counters[1].match(new RegExp(`${name}="(\\d+)"`)) ?? [])[1] ?? 0);
    counts.tests += attr('total');
    counts.passed += attr('passed');
    counts.failures += attr('failed');
    counts.errors += attr('error');
    counts.skipped += attr('notExecuted');
  }
  return counts;
}

/**
 * The canonical test invocation. Runs one test project with a trait filter; the unit gate also
 * collects coverage. Exit code 8 ("zero tests ran") is expected when a project has no tests for
 * this gate; the gate itself still fails if the total across projects is zero.
 * No --no-build: the repo guard forbids it, and the incremental rebuild is a no-op (ADR-009).
 */
function runTestGate(gate, traitArgs, withCoverage) {
  const resultsDir = path.join(testResults, gate);
  clean(resultsDir);
  const coverageFiles = [];
  let exitFailure = false;
  const outputs = [];

  for (const project of testProjects()) {
    const name = path.basename(project, '.csproj');
    const args = [
      'test', '--project', project, '-c', 'Release',
      ...traitArgs,
      '--ignore-exit-code', '8',
      '--report-xunit-trx', '--report-xunit-trx-filename', `${name}.trx`,
      '--results-directory', resultsDir,
    ];
    if (withCoverage) {
      const coverageFile = path.join(coverageDir, 'unit', `${name}.cobertura.xml`);
      args.push('--coverage', '--coverage-output-format', 'cobertura', '--coverage-output', coverageFile);
      coverageFiles.push(coverageFile);
    }
    const { code, output } = run('dotnet', args);
    outputs.push(output);
    if (code !== 0) exitFailure = true;
  }

  const counts = trxCounts(resultsDir);
  const status = !exitFailure && counts.tests > 0 && counts.failures === 0 && counts.errors === 0 ? 'pass' : 'fail';
  const gateResult = { status, ...counts, report: path.relative(repoRoot, resultsDir).replaceAll('\\', '/') };
  if (status === 'fail') gateResult.output_tail = outputs.join('\n').split('\n').slice(-40).join('\n');
  return { gateResult, coverageFiles: coverageFiles.filter((f) => fs.existsSync(f)) };
}

async function exportOpenApi() {
  const apiDll = path.join(repoRoot, 'src/CashRegister.Api/bin/Release/net10.0/CashRegister.Api.dll');
  const port = 5089;
  const child = spawn('dotnet', [apiDll], {
    cwd: path.dirname(apiDll),
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: `http://127.0.0.1:${port}` },
    stdio: 'ignore',
  });
  try {
    for (let attempt = 0; attempt < 60; attempt++) {
      try {
        const health = await fetch(`http://127.0.0.1:${port}/health`);
        if (health.ok) break;
      } catch {
        // not listening yet
      }
      await new Promise((resolve) => setTimeout(resolve, 500));
    }
    const response = await fetch(`http://127.0.0.1:${port}/openapi/v1.json`);
    if (!response.ok) throw new Error(`GET /openapi/v1.json returned ${response.status}`);
    return await response.json();
  } finally {
    child.kill();
  }
}

function operations(document) {
  const ops = new Set();
  for (const [route, item] of Object.entries(document?.paths ?? {})) {
    for (const method of Object.keys(item)) ops.add(`${method.toUpperCase()} ${route}`);
  }
  return ops;
}

async function contractGate() {
  const out = path.join(artifacts, 'openapi', 'openapi.json');
  const current = await exportOpenApi();
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, `${JSON.stringify(current, null, 2)}\n`);

  const baseline = run('git', ['show', `${baseRef}:artifacts/openapi/openapi.json`]);
  const now = operations(current);
  if (baseline.code !== 0) {
    return { status: 'pass', baseline: 'none', breaking: 0, non_breaking: now.size, added: [...now], removed: [] };
  }
  const before = operations(JSON.parse(baseline.output));
  const removed = [...before].filter((op) => !now.has(op));
  const added = [...now].filter((op) => !before.has(op));
  return {
    status: removed.length === 0 ? 'pass' : 'fail',
    baseline: baseRef,
    breaking: removed.length,
    non_breaking: added.length,
    added,
    removed,
    note: 'Operation-level diff (added/removed path + method); schema-level changes are not diffed.',
  };
}

function securityGate() {
  const dotnetScan = run('dotnet', ['list', solution, 'package', '--vulnerable', '--include-transitive']);
  fs.writeFileSync(path.join(artifacts, 'vulnerable.txt'), dotnetScan.output);
  const severity = (level) => (dotnetScan.output.match(new RegExp(`\\b${level}\\b`, 'g')) ?? []).length;
  const nuget = { high: severity('High'), critical: severity('Critical'), moderate: severity('Moderate'), low: severity('Low') };

  const npmScan = run('npm', ['--prefix', 'web', 'audit', '--json']);
  let npm = { high: 0, critical: 0, moderate: 0, low: 0 };
  try {
    const v = JSON.parse(npmScan.output.slice(npmScan.output.indexOf('{'))).metadata.vulnerabilities;
    npm = { high: v.high, critical: v.critical, moderate: v.moderate, low: v.low };
  } catch {
    npm = { error: 'npm audit output could not be parsed' };
  }

  const blocking = nuget.high + nuget.critical + (npm.high ?? 0) + (npm.critical ?? 0);
  return {
    status: npm.error ? 'error' : blocking === 0 ? 'pass' : 'fail',
    nuget,
    npm,
    waivers: 0,
    report: 'artifacts/vulnerable.txt',
  };
}

function webGate() {
  const steps = {
    lint: run('npm', ['--prefix', 'web', 'run', 'lint']).code,
    typecheck: run('npm', ['--prefix', 'web', 'run', 'typecheck']).code,
    test_coverage: run('npm', ['--prefix', 'web', 'run', 'test:coverage']).code,
    build: run('npm', ['--prefix', 'web', 'run', 'build']).code,
  };
  const failed = Object.entries(steps).filter(([, code]) => code !== 0).map(([step]) => step);
  let coverage = null;
  const webCobertura = path.join(coverageDir, 'web', 'cobertura-coverage.xml');
  if (fs.existsSync(webCobertura)) {
    // Istanbul orders the attributes differently from the .NET collector, so read them one by one.
    const header = (fs.readFileSync(webCobertura, 'utf8').match(/<coverage ([^>]*)>/) ?? [])[1] ?? '';
    const rate = (name) => Number((header.match(new RegExp(`${name}="([\\d.]+)"`)) ?? [])[1]);
    if (header) coverage = { line: Number(rate('line-rate').toFixed(4)), branch: Number(rate('branch-rate').toFixed(4)) };
  }
  return { status: failed.length === 0 ? 'pass' : 'fail', failed_steps: failed, coverage };
}

async function main() {
  const startedAt = new Date().toISOString();
  const sha = run('git', ['rev-parse', '--short', 'HEAD']).output.trim();
  const gates = {};

  fs.mkdirSync(artifacts, { recursive: true });

  gates.format = { status: run('dotnet', ['format', solution, '--verify-no-changes']).code === 0 ? 'pass' : 'fail' };
  gates.compile =
    gates.format.status === 'pass'
      ? { status: run('dotnet', ['build', solution, '-c', 'Release']).code === 0 ? 'pass' : 'fail' }
      : { status: 'not_run', reason: 'format pre-gate failed' };

  if (gates.compile.status === 'pass') {
    clean(path.join(coverageDir, 'unit'));
    const unit = runTestGate('unit', ['--filter-not-trait', 'Category=Integration'], true);
    gates.unit = unit.gateResult;
    gates.it = runTestGate('it', ['--filter-trait', 'Category=Integration'], false).gateResult;

    if (unit.coverageFiles.length === 0) {
      gates.coverage = { status: 'error', reason: 'no coverage report was produced by the unit run' };
    } else {
      const merged = readMerged(unit.coverageFiles, repoRoot);
      writeMerged(merged, path.join(coverageDir, 'cobertura.xml'));
      const t = totals(merged);
      gates.coverage = {
        status: t.line >= coverageFloor && t.branch >= coverageFloor ? 'pass' : 'fail',
        source: 'unit run only (canonical)',
        floor: coverageFloor,
        line: Number(t.line.toFixed(4)),
        branch: Number(t.branch.toFixed(4)),
        report: 'artifacts/coverage/cobertura.xml',
      };
    }

    gates.mutation = fs.existsSync(path.join(repoRoot, 'stryker-config.json'))
      ? { status: 'error', reason: 'stryker-config.json present but the mutation runner is not wired yet' }
      : { status: 'skipped', reason: 'no stryker-config.json (mutation testing not opted in)' };

    try {
      gates.contract = await contractGate();
    } catch (error) {
      gates.contract = { status: 'error', reason: String(error) };
    }
  } else {
    for (const gate of ['unit', 'it', 'coverage', 'mutation', 'contract']) {
      gates[gate] = { status: 'not_run', reason: 'a pre-gate failed' };
    }
  }

  gates.security = securityGate();
  gates.web = webGate();

  const overall = Object.values(gates).every((g) => g.status === 'pass' || g.status === 'skipped') ? 'pass' : 'fail';
  const summary = { git_sha: sha, started_at: startedAt, finished_at: new Date().toISOString(), gates, overall };

  fs.writeFileSync(path.join(artifacts, 'harness-summary.json'), `${JSON.stringify(summary, null, 2)}\n`);
  for (const [name, g] of Object.entries(gates)) log(`${name.padEnd(9)} ${g.status}`);
  log(`overall   ${overall}`);
  if (emitReport) process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
  process.exitCode = overall === 'pass' ? 0 : 1;
}

await main();
