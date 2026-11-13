import logging
from typing import Any, Literal

from copilot import define_tool
from pydantic import BaseModel, Field, create_model

from app.dotnet_client import DataServiceUnavailable, DotnetClient

log = logging.getLogger("ops_agent.tools")

UNAVAILABLE = {
    "error": "The data service is unavailable right now. Do not retry. "
             "Tell the user the data cannot be reached and to try again later."
}


class Filter(BaseModel):
    column: str = Field(description="Exact column name from the catalog, e.g. 'ReportNumber'.")
    op: Literal["eq", "contains"] = Field(
        "eq", description="'eq' = exact match (default). 'contains' = text contains; only for columns marked 'contains allowed'."
    )
    value: str | int | float | bool = Field(
        description="Value to match. If the catalog lists Values for the column, use one of them exactly."
    )


async def _call(tool: str, request) -> dict[str, Any]:
    try:
        result = await request
    except DataServiceUnavailable as ex:
        log.error("%s: %s", tool, ex)
        return UNAVAILABLE
    if "error" in result:
        log.info("%s -> error: %s", tool, result["error"])
    return result


def build_tools(client: DotnetClient, entity_names: list[str]) -> list:
    EntityName = Literal[tuple(entity_names)]

    QueryParams = create_model(
        "QueryParams",
        entity=(EntityName, Field(description="Which entity to read.")),
        filters=(list[Filter], Field(description="One or more filters, combined with AND. At least one is required.")),
        limit=(int | None, Field(None, description="Maximum rows to return. Default 10, maximum 25.")),
    )
    FindParams = create_model(
        "FindParams",
        value=(str, Field(description="The identifier exactly as the user gave it, e.g. '5521' or 'MT31061169'.")),
    )
    CountParams = create_model(
        "CountParams",
        entity=(EntityName, Field(description="Which entity to count.")),
        filters=(list[Filter], Field(default_factory=list, description="Optional filters, combined with AND.")),
        group_by=(str | None, Field(None, description="Optional ONE column marked 'groupable', for breakdowns like 'by center'.")),
    )

    @define_tool(
        name="query_entity",
        description=(
            "Read rows of ONE entity that match the filters. Use it when you know the entity and have a value to filter on, "
            "and to follow a relationship to a linked entity. Returns {rowCount, truncated, rows}."
        ),
        skip_permission=True,
    )
    async def query_entity(params: QueryParams) -> dict[str, Any]:
        filters = [f.model_dump() for f in params.filters]
        return await _call("query_entity", client.query(params.entity, filters, params.limit))

    @define_tool(
        name="find_by_identifier",
        description=(
            "Find which entity an identifier belongs to, when the user gives an ID or number without saying what it is "
            "(e.g. 'what is 5521?'). Returns only matches [{entity, column}], not the data; then call query_entity."
        ),
        skip_permission=True,
    )
    async def find_by_identifier(params: FindParams) -> dict[str, Any]:
        return await _call("find_by_identifier", client.find(params.value))

    @define_tool(
        name="count_entity",
        description=(
            "Count rows of ONE entity, optionally filtered, optionally grouped by one groupable column. "
            "Use it for 'how many' and breakdowns ('by status', 'per center'). Returns {total, groups, groupsTruncated}."
        ),
        skip_permission=True,
    )
    async def count_entity(params: CountParams) -> dict[str, Any]:
        filters = [f.model_dump() for f in params.filters]
        return await _call("count_entity", client.count(params.entity, filters, params.group_by))

    return [query_entity, find_by_identifier, count_entity]