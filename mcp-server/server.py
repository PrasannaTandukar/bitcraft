"""Bitcraft minimal MCP server.

Read-only Model Context Protocol server that surfaces Bitcraft governance docs,
agent roster, and prioritized inboxes to compatible coding agents (GitHub Copilot,
Claude Desktop, etc.).

Design rules:
- Read-only. No tool ever writes to disk, executes shell commands, or contacts
  external services.
- Scoped. All file access is constrained to specific subdirectories under the
  repository root. Path traversal attempts are rejected.
- Minimal. Adds capability only where Copilot context limits hurt research,
  planning, or audit prompts. Extend deliberately.

Run locally:
    python -m pip install -r requirements.txt
    python server.py
"""

from __future__ import annotations

import re
from pathlib import Path

from mcp.server.fastmcp import FastMCP

REPO_ROOT = Path(__file__).resolve().parent.parent
GITHUB_DIR = REPO_ROOT / ".github"
INBOX_DIR = GITHUB_DIR / "inboxes"
AGENTS_DIR = GITHUB_DIR / "agents"
PROMPTS_DIR = GITHUB_DIR / "prompts"
SKILLS_DIR = GITHUB_DIR / "skills"
GOVERNANCE_DIR = GITHUB_DIR / "agentic-ai"
SRC_DIR = REPO_ROOT / "src"

# Clean Architecture: Domain and Application must not depend on Infrastructure or Api.
# Application additionally must not reference external packages.
FORBIDDEN_PROJECT_REFERENCES = {
    "Bitcraft.Domain": ("Bitcraft.Infrastructure", "Bitcraft.Api", "Bitcraft.Application"),
    "Bitcraft.Application": ("Bitcraft.Infrastructure", "Bitcraft.Api"),
}
FORBIDDEN_USINGS = {
    "Bitcraft.Domain": ("Bitcraft.Infrastructure", "Bitcraft.Api", "Bitcraft.Application"),
    "Bitcraft.Application": ("Bitcraft.Infrastructure", "Bitcraft.Api"),
}
# Application is the only project with a hard "no external packages" rule.
NO_EXTERNAL_PACKAGES_PROJECTS = ("Bitcraft.Application",)

mcp = FastMCP("bitcraft-mcp")


def _safe_read(directory: Path, name: str, suffix: str = ".md") -> str:
    """Read ``directory / name + suffix`` while preventing path traversal."""
    candidate = (directory / f"{name}{suffix}").resolve()
    if directory.resolve() not in candidate.parents and candidate.parent != directory.resolve():
        raise ValueError("Path is outside the allowed directory.")
    if not candidate.is_file():
        raise FileNotFoundError(f"{candidate.relative_to(REPO_ROOT)} does not exist.")
    return candidate.read_text(encoding="utf-8")


@mcp.tool()
def list_inboxes() -> list[str]:
    """List agent slugs that have a prioritized inbox."""
    if not INBOX_DIR.is_dir():
        return []
    return sorted(p.name for p in INBOX_DIR.iterdir() if p.is_dir())


@mcp.tool()
def read_inbox(agent_slug: str) -> str:
    """Read the prioritized backlog for one agent.

    Args:
        agent_slug: Lowercase agent slug such as ``ai-ops`` or ``net-dev``.
    """
    inbox_file = (INBOX_DIR / agent_slug / "inbox.md").resolve()
    if INBOX_DIR.resolve() not in inbox_file.parents:
        raise ValueError("agent_slug is outside the inboxes directory.")
    if not inbox_file.is_file():
        raise FileNotFoundError(f"No inbox for agent '{agent_slug}'.")
    return inbox_file.read_text(encoding="utf-8")


@mcp.tool()
def list_governance_docs() -> list[str]:
    """List governance documents available under .github/agentic-ai/."""
    if not GOVERNANCE_DIR.is_dir():
        return []
    return sorted(p.stem for p in GOVERNANCE_DIR.glob("*.md"))


@mcp.tool()
def read_governance(name: str) -> str:
    """Read a governance doc by its base name.

    Args:
        name: Base filename without extension, for example ``PROJECT_BRIEF``,
            ``DEVELOPER_GUIDE``, ``METRICS``, ``MODEL_ROUTING``, or
            ``AUDIT_TEMPLATES``.
    """
    return _safe_read(GOVERNANCE_DIR, name)


@mcp.tool()
def list_agents() -> list[str]:
    """List custom agent slugs defined in .github/agents/."""
    if not AGENTS_DIR.is_dir():
        return []
    return sorted(p.stem.replace(".agent", "") for p in AGENTS_DIR.glob("*.agent.md"))


@mcp.tool()
def read_roster() -> str:
    """Read the agent roster at .github/agents/ROSTER.md."""
    return _safe_read(AGENTS_DIR, "ROSTER")


@mcp.tool()
def verify_architecture_readonly() -> dict:
    """Report Clean Architecture rule violations in Bitcraft.Domain and Bitcraft.Application.

    Read-only inspector. No edits, no shell, no network. Scans `.csproj` files
    and `.cs` `using` statements for:

    - Forbidden `<ProjectReference>` entries (e.g., Domain referencing Infrastructure).
    - Forbidden `using` statements (same boundaries) in `.cs` files.
    - Any `<PackageReference>` entries inside `Bitcraft.Application` (which must
      not reference external packages per project rules).

    Returns a dict with:
    - `violations`: list of {project, file, kind, detail} entries.
    - `inspectedProjects`: list of project paths scanned.
    - `note`: human-readable summary.

    Enforcement of these rules belongs to tests and CI; this tool is only a
    fast, agent-side spot check.
    """
    violations: list[dict] = []
    inspected: list[str] = []

    if not SRC_DIR.is_dir():
        return {
            "violations": [],
            "inspectedProjects": [],
            "note": "src/ directory not found; nothing to inspect.",
        }

    project_ref_pattern = re.compile(
        r'<ProjectReference\s+Include="([^"]+)"', re.IGNORECASE
    )
    package_ref_pattern = re.compile(
        r'<PackageReference\s+Include="([^"]+)"', re.IGNORECASE
    )
    using_pattern = re.compile(r"^\s*using\s+([A-Za-z0-9_.]+)\s*;", re.MULTILINE)

    for project_name, forbidden_refs in FORBIDDEN_PROJECT_REFERENCES.items():
        project_dir = SRC_DIR / project_name
        csproj = project_dir / f"{project_name}.csproj"
        if not csproj.is_file():
            continue
        inspected.append(str(csproj.relative_to(REPO_ROOT)))
        try:
            content = csproj.read_text(encoding="utf-8")
        except OSError as exc:
            violations.append(
                {
                    "project": project_name,
                    "file": str(csproj.relative_to(REPO_ROOT)),
                    "kind": "read-error",
                    "detail": str(exc),
                }
            )
            continue

        for ref in project_ref_pattern.findall(content):
            ref_name = Path(ref).stem
            if ref_name in forbidden_refs:
                violations.append(
                    {
                        "project": project_name,
                        "file": str(csproj.relative_to(REPO_ROOT)),
                        "kind": "forbidden-project-reference",
                        "detail": ref_name,
                    }
                )

        if project_name in NO_EXTERNAL_PACKAGES_PROJECTS:
            for pkg in package_ref_pattern.findall(content):
                violations.append(
                    {
                        "project": project_name,
                        "file": str(csproj.relative_to(REPO_ROOT)),
                        "kind": "forbidden-package-reference",
                        "detail": pkg,
                    }
                )

        forbidden_usings = FORBIDDEN_USINGS.get(project_name, ())
        for cs_file in project_dir.rglob("*.cs"):
            if any(part in {"bin", "obj"} for part in cs_file.relative_to(project_dir).parts):
                continue
            try:
                text = cs_file.read_text(encoding="utf-8")
            except OSError:
                continue
            for using in using_pattern.findall(text):
                if any(using == bad or using.startswith(f"{bad}.") for bad in forbidden_usings):
                    violations.append(
                        {
                            "project": project_name,
                            "file": str(cs_file.relative_to(REPO_ROOT)),
                            "kind": "forbidden-using",
                            "detail": using,
                        }
                    )

    note = (
        "Clean Architecture check passed."
        if not violations
        else f"Found {len(violations)} violation(s). Treat as spot check; rely on tests/CI for enforcement."
    )

    return {
        "violations": violations,
        "inspectedProjects": inspected,
        "note": note,
    }


if __name__ == "__main__":
    mcp.run()
