from __future__ import annotations

from abc import ABC, abstractmethod

from app.schemas import SidecarRequest, SidecarResponse


class SidecarTool(ABC):
    @property
    @abstractmethod
    def key(self) -> str:
        raise NotImplementedError

    @abstractmethod
    async def execute(self, request: SidecarRequest) -> SidecarResponse:
        raise NotImplementedError
