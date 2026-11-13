import logging
import ssl
from typing import Any

import httpx
import truststore

from app.config import Settings

log = logging.getLogger("ops_agent.dotnet") # logger for http related errors

class DataServiceUnavailable(Exception):
    """The .NET data API can't be used right now: down, wrong key, or a server bug."""

class DotnetClient:
    def __init__(self, settings: Settings) -> None:
        self._http = httpx.AsyncClient(
            base_url=settings.dotnet_base_url.rstrip("/"),
            headers={"X-Agent-Key": settings.agent_api_key.get_secret_value()},
            timeout=settings.http_timeout_seconds,
            verify=truststore.SSLContext(ssl.PROTOCOL_TLS_CLIENT),
        )

    async def aclose(self) -> None:
        await self._http.aclose()

    async def get_catalog(self) -> dict[str, Any]:
        return await self._send("GET", "/api/agent/catalog")

    async def query(self, entity: str, filters: list[dict], limit: int | None = None) -> dict[str, Any]:
        body: dict[str, Any] = {"entity": entity, "filters": filters}
        if limit is not None:
            body["limit"] = limit

        return await self._send("POST", "api/agent/query", body)

    async def find(self, value: str) -> dict[str, Any]:
        return await self._send("POST", "/api/agent/find", {"value": value})

    async def count(self, entity: str, filters: list[dict], group_by: str | None = None) -> dict[str, Any]:
        body: dict[str, Any] = {"entity": entity, "filters": filters}
        if group_by:
            body["groupBy"] = group_by
        return await self._send("POST", "/api/agent/count", body)

    async def _send(self, method: str, path: str, body: dict | None = None) -> dict[str, Any]:
        try:
            resp = await self._http.request(method, path, json=body)
        except httpx.HTTPError as ex:
            log.error("%s %s failed: %r", method, path, ex)
            raise DataServiceUnavailable(f"cannot reach the data service ({type(ex).__name__})") from ex

        if resp.status_code == 200:
            return resp.json()

        if resp.status_code in (400, 504):
            return {"error": _error_text(resp)}

        if resp.status_code == 500:
            log.error("%s %s -> HTTP 500: %s", method, path, resp.text[:500])
            return {"error": "This lookup failed on the server. Do not repeat the same call. "
                             "Tell the user this lookup failed and suggest rephrasing the question."}

        log.error("%s %s -> HTTP %s: %s", method, path, resp.status_code, resp.text[:500])
        raise DataServiceUnavailable(f"data service returned HTTP {resp.status_code}")


def _error_text(resp: httpx.Response) -> str:
    try:
        return str(resp.json().get("error") or resp.text)
    except ValueError:
        return resp.text