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
GOVERNANCE_DIR = GITHUB_DIR / "governance"
CONFIG_DIR = GITHUB_DIR / "config"
PLANS_DIR = GITHUB_DIR / "plans"
DOCS_DEV_DIR = REPO_ROOT / "docs" / "dev"
SRC_DIR = REPO_ROOT / "src"

# Clean Architecture: Domain and Application must not depend on Infrastructure or Api.
# Application additionally must not reference external packages.
FORBIDDEN_PROJECT_REFERENCES = {
    "Bitcraft.Domain": (
        "Bitcraft.Infrastructure",
        "Bitcraft.Api",
        "Bitcraft.Application",
    ),
    "Bitcraft.Application": ("Bitcraft.Infrastructure", "Bitcraft.Api"),
}
FORBIDDEN_USINGS = {
    "Bitcraft.Domain": (
        "Bitcraft.Infrastructure",
        "Bitcraft.Api",
        "Bitcraft.Application",
    ),
    "Bitcraft.Application": ("Bitcraft.Infrastructure", "Bitcraft.Api"),
}
# Application is the only project with a hard "no external packages" rule.
NO_EXTERNAL_PACKAGES_PROJECTS = ("Bitcraft.Application",)
# Packages explicitly approved inside Bitcraft.Application despite the
# no-external-packages rule. Keep this list narrow; expansions require human approval.
APPLICATION_PACKAGE_ALLOWLIST = frozenset(
    {
        "MediatR",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
    }
)
FRONTMATTER_PATTERN = re.compile(r"\A---\r?\n(?P<body>.*?)\r?\n---\r?\n", re.DOTALL)
STACK_SLUG_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$", re.IGNORECASE)

mcp = FastMCP("bitcraft-mcp")


def _safe_read(directory: Path, name: str, suffix: str = ".md") -> str:
    """Read ``directory / name + suffix`` while preventing path traversal."""
    candidate = (directory / f"{name}{suffix}").resolve()
    if (
        directory.resolve() not in candidate.parents
        and candidate.parent != directory.resolve()
    ):
        raise ValueError("Path is outside the allowed directory.")
    if not candidate.is_file():
        raise FileNotFoundError(f"{candidate.relative_to(REPO_ROOT)} does not exist.")
    return candidate.read_text(encoding="utf-8")


def _safe_child_dir(directory: Path, slug: str) -> Path:
    if not STACK_SLUG_PATTERN.match(slug):
        raise ValueError("Slug contains unsupported characters.")
    root = directory.resolve()
    candidate = (root / slug).resolve()
    if root not in candidate.parents:
        raise ValueError("Slug is outside the allowed directory.")
    return candidate


def _parse_frontmatter(text: str) -> dict:
    match = FRONTMATTER_PATTERN.match(text)
    if not match:
        return {}

    metadata: dict[str, str] = {}
    for line in match.group("body").splitlines():
        if not line.strip() or line.startswith((" ", "\t", "-")):
            continue
        key, separator, value = line.partition(":")
        if separator:
            metadata[key.strip()] = value.strip().strip("'\"")
    return metadata


def _strip_frontmatter(text: str) -> str:
    return FRONTMATTER_PATTERN.sub("", text, count=1)


def _extract_stack_source_paths(stack_dir: Path, text: str) -> list[dict]:
    source_paths: list[dict] = []
    stack_root = stack_dir.resolve()
    for raw_path in re.findall(r"^\s*path:\s*['\"]?([^'\"\r\n]+)", text, re.MULTILINE):
        candidate = (stack_root / raw_path).resolve()
        if stack_root not in candidate.parents and candidate.parent != stack_root:
            source_paths.append(
                {"path": raw_path, "exists": False, "error": "outside-stack"}
            )
            continue
        source_paths.append({"path": raw_path, "exists": candidate.is_file()})
    return source_paths


def _extract_external_sources(text: str) -> list[str]:
    return re.findall(r"^\s*url:\s*['\"]?([^'\"\r\n]+)", text, re.MULTILINE)


def _parse_inbox_items(text: str) -> list[dict]:
    items: list[dict] = []
    current: dict[str, str] | None = None
    current_key = ""

    for line in _strip_frontmatter(text).splitlines():
        if line.startswith("- ") and ":" in line:
            key, value = line[2:].split(":", 1)
            normalized = (
                key.strip()
                .lower()
                .replace("/", "_")
                .replace(" ", "_")
                .replace("-", "_")
            )
            if normalized == "date":
                if current:
                    items.append(current)
                current = {}
            if current is None:
                current = {}
            current[normalized] = value.strip()
            current_key = normalized
            continue

        if current is not None and current_key and line.startswith("  "):
            continuation = line.strip()
            if continuation:
                current[current_key] = f"{current[current_key]}\n{continuation}".strip()

    if current:
        items.append(current)
    return items


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
        agent_slug: Lowercase agent slug such as ``ai-ops`` or ``lead-dev``.
    """
    inbox_file = (INBOX_DIR / agent_slug / "inbox.md").resolve()
    if INBOX_DIR.resolve() not in inbox_file.parents:
        raise ValueError("agent_slug is outside the inboxes directory.")
    if not inbox_file.is_file():
        raise FileNotFoundError(f"No inbox for agent '{agent_slug}'.")
    return inbox_file.read_text(encoding="utf-8")


@mcp.tool()
def read_inbox_structured(agent_slug: str) -> dict:
    """Read one agent inbox as structured items parsed from the Markdown source.

    Args:
        agent_slug: Lowercase agent slug such as ``ai-ops`` or ``lead-dev``.
    """
    inbox_text = read_inbox(agent_slug)
    inbox_file = (INBOX_DIR / agent_slug / "inbox.md").resolve()
    items = _parse_inbox_items(inbox_text)
    return {
        "agentSlug": agent_slug,
        "sourcePath": str(inbox_file.relative_to(REPO_ROOT)),
        "itemCount": len(items),
        "items": items,
    }


@mcp.tool()
def list_doc_stacks() -> list[dict]:
    """List developer-local documentation stacks under ignored docs/dev/.

    Missing or empty ``docs/dev/`` is valid and returns an empty list.
    """
    if not DOCS_DEV_DIR.is_dir():
        return []

    stacks: list[dict] = []
    for stack_dir in sorted(p for p in DOCS_DEV_DIR.iterdir() if p.is_dir()):
        stack_file = stack_dir / "STACK.md"
        if not stack_file.is_file():
            continue
        text = stack_file.read_text(encoding="utf-8")
        metadata = _parse_frontmatter(text)
        stacks.append(
            {
                "slug": stack_dir.name,
                "path": str(stack_file.relative_to(REPO_ROOT)),
                "name": metadata.get("name", stack_dir.name),
                "description": metadata.get("description", ""),
                "validationStatus": metadata.get("validation_status", "unknown"),
                "sourcePaths": _extract_stack_source_paths(stack_dir, text),
                "sourceKind": "developer-local",
            }
        )
    return stacks


@mcp.tool()
def read_doc_stack(stack_slug: str) -> dict:
    """Read one developer-local documentation stack manifest.

    Args:
        stack_slug: Folder name under ``docs/dev/`` such as ``github-copilot``.
    """
    stack_dir = _safe_child_dir(DOCS_DEV_DIR, stack_slug)
    stack_file = stack_dir / "STACK.md"
    if not stack_file.is_file():
        raise FileNotFoundError(f"No STACK.md for documentation stack '{stack_slug}'.")

    text = stack_file.read_text(encoding="utf-8")
    return {
        "slug": stack_slug,
        "sourcePath": str(stack_file.relative_to(REPO_ROOT)),
        "metadata": _parse_frontmatter(text),
        "sourcePaths": _extract_stack_source_paths(stack_dir, text),
        "externalSources": _extract_external_sources(text),
        "sourceKind": "developer-local",
        "stackDocument": text,
    }


@mcp.tool()
def list_governance_docs() -> list[str]:
    """List governance documents available under .github/governance/."""
    if not GOVERNANCE_DIR.is_dir():
        return []
    return sorted(p.stem for p in GOVERNANCE_DIR.glob("*.md"))


@mcp.tool()
def read_governance(name: str) -> str:
    """Read a governance doc by its base name.

    Args:
        name: Base filename without extension, for example ``PROJECT_BRIEF``,
            ``DEVELOPER_GUIDE``, ``METRICS``, ``MODEL_ROUTING``,
            ``BRANCH_CONVENTION``, or ``HUMAN_DECISIONS``.
    """
    return _safe_read(GOVERNANCE_DIR, name)


@mcp.tool()
def list_configs() -> list[str]:
    """List human-managed configuration files under .github/config/."""
    if not CONFIG_DIR.is_dir():
        return []
    return sorted(
        p.name
        for p in CONFIG_DIR.iterdir()
        if p.is_file() and p.suffix.lower() in {".yaml", ".yml"}
    )


@mcp.tool()
def read_config(name: str) -> str:
    """Read a configuration file from .github/config/ by its filename.

    Args:
        name: Filename including extension, for example
            ``approved-external-sources.yaml`` or ``cil-medium.yaml``.
    """
    if not isinstance(name, str) or not name:
        raise ValueError("name must be a non-empty string")
    # Disallow path separators; only flat filenames under CONFIG_DIR.
    if "/" in name or "\\" in name or ".." in name:
        raise ValueError("name must be a flat filename without path separators")
    if Path(name).suffix.lower() not in {".yaml", ".yml"}:
        raise ValueError("only .yaml/.yml configuration files are exposed")
    target = (CONFIG_DIR / name).resolve()
    if (
        CONFIG_DIR.resolve() not in target.parents
        and target.parent != CONFIG_DIR.resolve()
    ):
        raise ValueError("config path escapes the configuration directory")
    if not target.is_file():
        raise FileNotFoundError(f"config not found: {name}")
    return target.read_text(encoding="utf-8")


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
def list_active_plans(include_archived: bool = False) -> list[dict]:
    """List active plan branch folders under .github/plans/.

    Excludes the global ``archive/`` and ``templates/`` directories. Each
    returned entry includes minimal metadata parsed from ``plan.yaml`` when
    present.

    Args:
        include_archived: When ``True``, also list folders under
            ``.github/plans/archive/``. Defaults to ``False``.
    """
    if not PLANS_DIR.is_dir():
        return []

    plans: list[dict] = []
    reserved = {"archive", "templates"}
    for entry in sorted(p for p in PLANS_DIR.iterdir() if p.is_dir()):
        if entry.name in reserved:
            continue
        plan_yaml = entry / "plan.yaml"
        metadata: dict[str, str] = {}
        if plan_yaml.is_file():
            for line in plan_yaml.read_text(encoding="utf-8").splitlines():
                if not line or line.startswith(("#", " ", "\t", "-")):
                    continue
                key, separator, value = line.partition(":")
                if separator:
                    metadata[key.strip()] = value.strip().strip("'\"")
        plans.append(
            {
                "branchSlug": entry.name,
                "path": str(entry.relative_to(REPO_ROOT)),
                "hasPlanYaml": plan_yaml.is_file(),
                "status": metadata.get("status", "unknown"),
                "cil": metadata.get("cil", "unknown"),
                "goal": metadata.get("goal", ""),
                "archived": False,
            }
        )

    if include_archived:
        archive_dir = PLANS_DIR / "archive"
        if archive_dir.is_dir():
            for entry in sorted(
                p for p in archive_dir.iterdir() if p.is_dir() or p.is_file()
            ):
                plans.append(
                    {
                        "branchSlug": entry.name,
                        "path": str(entry.relative_to(REPO_ROOT)),
                        "hasPlanYaml": (entry / "plan.yaml").is_file()
                        if entry.is_dir()
                        else False,
                        "status": "archived",
                        "cil": "unknown",
                        "goal": "",
                        "archived": True,
                    }
                )

    return plans


@mcp.tool()
def read_plan(branch_slug: str) -> dict:
    """Read one active plan folder under .github/plans/<branch_slug>/.

    Returns ``plan.yaml`` content, optional ``plan.md``, and optional
    ``context.md``. Archived plans are not served via this tool; use
    ``list_active_plans(include_archived=True)`` to discover them and read
    them through ordinary file access.

    Args:
        branch_slug: Folder name under ``.github/plans/`` such as
            ``batch-2-agentic-stack``.
    """
    if branch_slug in {"archive", "templates"}:
        raise ValueError(
            "branch_slug must be an active plan folder, not a reserved subfolder."
        )

    plan_dir = _safe_child_dir(PLANS_DIR, branch_slug)
    if not plan_dir.is_dir():
        raise FileNotFoundError(f"No active plan folder for '{branch_slug}'.")

    plan_yaml = plan_dir / "plan.yaml"
    plan_md = plan_dir / "plan.md"
    context_md = plan_dir / "context.md"

    return {
        "branchSlug": branch_slug,
        "path": str(plan_dir.relative_to(REPO_ROOT)),
        "planYaml": plan_yaml.read_text(encoding="utf-8")
        if plan_yaml.is_file()
        else None,
        "planMarkdown": plan_md.read_text(encoding="utf-8")
        if plan_md.is_file()
        else None,
        "contextMarkdown": context_md.read_text(encoding="utf-8")
        if context_md.is_file()
        else None,
    }


@mcp.tool()
def verify_architecture_readonly() -> dict:
    """Report Clean Architecture rule violations in Bitcraft.Domain and Bitcraft.Application.

    Read-only inspector. No edits, no shell, no network. Scans `.csproj` files
    and `.cs` `using` statements for:

    - Forbidden `<ProjectReference>` entries (e.g., Domain referencing Infrastructure).
    - Forbidden `using` statements (same boundaries) in `.cs` files.
    - Any `<PackageReference>` entries inside `Bitcraft.Application` that are
      not in ``APPLICATION_PACKAGE_ALLOWLIST``.

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
                if pkg in APPLICATION_PACKAGE_ALLOWLIST:
                    continue
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
            if any(
                part in {"bin", "obj"}
                for part in cs_file.relative_to(project_dir).parts
            ):
                continue
            try:
                text = cs_file.read_text(encoding="utf-8")
            except OSError:
                continue
            for using in using_pattern.findall(text):
                if any(
                    using == bad or using.startswith(f"{bad}.")
                    for bad in forbidden_usings
                ):
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
