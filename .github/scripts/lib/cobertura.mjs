// Minimal Cobertura reader/merger for Microsoft.Testing.Extensions.CodeCoverage output.
// Only first-party source under src/ counts; compiler-generated files under obj/ are ignored.
import fs from 'node:fs';
import path from 'node:path';

/** Repo-relative, forward-slash path, or null when the file is not first-party source. */
export function sourcePath(filename, repoRoot) {
  const normalized = filename.replaceAll('\\', '/');
  const root = repoRoot.replaceAll('\\', '/').replace(/\/?$/, '/');
  const relative = normalized.toLowerCase().startsWith(root.toLowerCase())
    ? normalized.slice(root.length)
    : normalized;
  if (!relative.startsWith('src/') || relative.includes('/obj/')) return null;
  return relative;
}

/**
 * Reads one or more Cobertura files and merges them: a line is covered if any run hit it;
 * a line's branch coverage is the best seen in any run.
 * @returns {Map<string, Map<number, {hits: number, branchesCovered: number, branchesTotal: number}>>}
 */
export function readMerged(files, repoRoot) {
  const byFile = new Map();
  for (const file of files) {
    const xml = fs.readFileSync(file, 'utf8');
    for (const cls of xml.split('<class ').slice(1)) {
      const filename = (cls.match(/filename="([^"]+)"/) ?? [])[1];
      const source = filename && sourcePath(filename, repoRoot);
      if (!source) continue;
      const lines = byFile.get(source) ?? new Map();
      byFile.set(source, lines);
      for (const m of cls.matchAll(/<line ([^>]*?)\/?>/g)) {
        const attrs = m[1];
        const number = Number((attrs.match(/number="(\d+)"/) ?? [])[1]);
        const hits = Number((attrs.match(/hits="(\d+)"/) ?? [])[1] ?? 0);
        if (!number) continue;
        const branch = attrs.match(/condition-coverage="\d+%\s*\((\d+)\/(\d+)\)"/);
        const previous = lines.get(number) ?? { hits: 0, branchesCovered: 0, branchesTotal: 0 };
        lines.set(number, {
          hits: Math.max(previous.hits, hits),
          branchesCovered: Math.max(previous.branchesCovered, branch ? Number(branch[1]) : 0),
          branchesTotal: Math.max(previous.branchesTotal, branch ? Number(branch[2]) : 0),
        });
      }
    }
  }
  return byFile;
}

export function totals(byFile) {
  let lines = 0;
  let covered = 0;
  let branches = 0;
  let branchesCovered = 0;
  for (const lineMap of byFile.values()) {
    for (const line of lineMap.values()) {
      lines++;
      if (line.hits > 0) covered++;
      branches += line.branchesTotal;
      branchesCovered += line.branchesCovered;
    }
  }
  return {
    lines,
    covered,
    branches,
    branchesCovered,
    line: lines === 0 ? 0 : covered / lines,
    branch: branches === 0 ? 1 : branchesCovered / branches,
  };
}

/** Writes the merged result as a single Cobertura document (one class per source file). */
export function writeMerged(byFile, outFile) {
  const t = totals(byFile);
  const classes = [...byFile.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([file, lineMap]) => {
      const ft = totals(new Map([[file, lineMap]]));
      const lineXml = [...lineMap.entries()]
        .sort(([a], [b]) => a - b)
        .map(([n, l]) =>
          l.branchesTotal > 0
            ? `<line number="${n}" hits="${l.hits}" branch="True" condition-coverage="${Math.round((100 * l.branchesCovered) / l.branchesTotal)}% (${l.branchesCovered}/${l.branchesTotal})"/>`
            : `<line number="${n}" hits="${l.hits}" branch="False"/>`,
        )
        .join('');
      return `<class name="${path.basename(file, '.cs')}" filename="${file}" line-rate="${ft.line.toFixed(4)}" branch-rate="${ft.branch.toFixed(4)}"><lines>${lineXml}</lines></class>`;
    })
    .join('\n');
  const xml = `<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="${t.line.toFixed(4)}" branch-rate="${t.branch.toFixed(4)}" lines-covered="${t.covered}" lines-valid="${t.lines}" branches-covered="${t.branchesCovered}" branches-valid="${t.branches}" version="merged">
<packages><package name="first-party" line-rate="${t.line.toFixed(4)}" branch-rate="${t.branch.toFixed(4)}"><classes>
${classes}
</classes></package></packages>
</coverage>
`;
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  fs.writeFileSync(outFile, xml);
}
