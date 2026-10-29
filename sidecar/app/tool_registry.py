from __future__ import annotations

from app.tools.base import SidecarTool


class ToolRegistry:
    def __init__(self) -> None:
        self._tools: dict[str, SidecarTool] = {}

    def register(self, tool: SidecarTool) -> None:
        self._tools[tool.key] = tool

    def get(self, key: str) -> SidecarTool | None:
        return self._tools.get(key)
