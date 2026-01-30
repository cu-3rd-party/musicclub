import { promises as fs } from "fs";
import path from "path";
import zlib from "zlib";

const distDir = path.resolve(process.cwd(), "dist");
const assetsDir = path.join(distDir, "assets");

async function fileExists(p) {
  try {
    await fs.access(p);
    return true;
  } catch {
    return false;
  }
}

function formatBytes(bytes) {
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(2)} kb`;
  return `${(kb / 1024).toFixed(2)} mb`;
}

function gzipSize(buffer) {
  return zlib.gzipSync(buffer).length;
}

function brotliSize(buffer) {
  return zlib.brotliCompressSync(buffer).length;
}

async function getAssetFiles() {
  const files = [];
  const hasAssets = await fileExists(assetsDir);
  if (!hasAssets) return files;
  const entries = await fs.readdir(assetsDir, { withFileTypes: true });
  for (const entry of entries) {
    if (!entry.isFile()) continue;
    if (!entry.name.endsWith(".js") && !entry.name.endsWith(".css")) continue;
    files.push(path.join(assetsDir, entry.name));
  }
  return files;
}

async function main() {
  const hasDist = await fileExists(distDir);
  if (!hasDist) {
    console.error("dist/ not found. Run the build first: npm run build");
    process.exit(1);
  }

  const files = await getAssetFiles();
  if (files.length === 0) {
    console.error("No JS/CSS assets found in dist/assets.");
    process.exit(1);
  }

  let totalRaw = 0;
  let totalGzip = 0;
  let totalBrotli = 0;

  const rows = [];
  for (const file of files) {
    const buf = await fs.readFile(file);
    const raw = buf.length;
    const gz = gzipSize(buf);
    const br = brotliSize(buf);
    totalRaw += raw;
    totalGzip += gz;
    totalBrotli += br;
    rows.push({ file: path.basename(file), raw, gz, br });
  }

  rows.sort((a, b) => b.raw - a.raw);

  console.log("Bundle size (dist/assets):");
  for (const row of rows) {
    console.log(`- ${row.file}: ${formatBytes(row.raw)} | gzip ${formatBytes(row.gz)} | br ${formatBytes(row.br)}`);
  }
  console.log("Totals:");
  console.log(`- raw:   ${formatBytes(totalRaw)}`);
  console.log(`- gzip:  ${formatBytes(totalGzip)}`);
  console.log(`- br:    ${formatBytes(totalBrotli)}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
