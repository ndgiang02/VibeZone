// Copies static assets into the standalone output so `node .next/standalone/server.js`
// serves them (Next.js leaves this to the deployer; see output.md "standalone").
import { cpSync, existsSync } from "node:fs";

const target = ".next/standalone";
if (!existsSync(`${target}/server.js`)) {
  console.error("No standalone build found. Run `npm run build` first.");
  process.exit(1);
}
cpSync(".next/static", `${target}/.next/static`, { recursive: true });
if (existsSync("public")) {
  cpSync("public", `${target}/public`, { recursive: true });
}
console.log("standalone: static assets copied");
