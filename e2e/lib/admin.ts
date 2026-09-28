import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("../..", import.meta.url));

/**
 * An administrative command of the API (grant-role, import-content), run
 * inside the backend container the way the README tells a person to. The
 * development container keeps the built API next to dotnet watch, so the
 * command does not build it a second time.
 */
export function admin(...args: string[]): string {
  const dll = process.env.E2E_ADMIN_DLL ?? "src/Ocwip.Api/bin/Debug/net10.0/Ocwip.Api.dll";
  return execFileSync("docker", ["compose", "exec", "-T", "backend", "dotnet", dll, ...args], {
    cwd: repoRoot,
    encoding: "utf8",
  });
}
