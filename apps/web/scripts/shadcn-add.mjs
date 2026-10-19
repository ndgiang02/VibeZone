// Wraps `shadcn add`: the radix-nova registry imports `cn` from the young "cn" npm package; this
// project keeps the established clsx + tailwind-merge helper in "@/lib/utils" instead.
//   npm run ui:add -- button dialog
import { execSync } from "node:child_process";
import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const components = process.argv.slice(2);
if (components.length === 0) {
  console.error("Usage: npm run ui:add -- <component...>");
  process.exit(1);
}

execSync(`npx shadcn@4.22.1 add ${components.join(" ")} -y`, { stdio: "inherit" });

const dir = "src/components/ui";
for (const file of readdirSync(dir)) {
  const path = join(dir, file);
  const source = readFileSync(path, "utf8");
  const fixed = source.replace(/from ["']cn["']/g, 'from "@/lib/utils"');
  if (fixed !== source) writeFileSync(path, fixed);
}

const pkg = JSON.parse(readFileSync("package.json", "utf8"));
if (pkg.dependencies?.cn) execSync("npm uninstall cn", { stdio: "inherit" });
console.log("ui:add: imports point at @/lib/utils");
