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

from pathlib import Path

from mcp.server.fastmcp import FastMCP

REPO_ROOT = Path(__file__).resolve().parent.parent
GITHUB_DIR = REPO_ROOT / ".github"
INBOX_DIR = GITHUB_DIR / "inboxes"
AGENTS_DIR = GITHUB_DIR / "agents"
PROMPTS_DIR = GITHUB_DIR / "prompts"
SKILLS_DIR = GITHUB_DIR / "skills"
GOVERNANCE_DIR = GITHUB_DIR / "agentic-ai"

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


if __name__ == "__main__":
    mcp.run()
