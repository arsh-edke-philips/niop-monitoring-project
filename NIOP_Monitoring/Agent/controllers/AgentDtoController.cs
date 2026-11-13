using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NIOP_Monitoring.Agent
{
    [ApiController]
    [Route("api/agent")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [TypeFilter(typeof(ApiKeyFilter))]
    public class AgentDataController(
        CatalogService catalogService,
        AgentQueryService queryService,
        ILogger<AgentDataController> logger) : ControllerBase
    {
        [HttpGet("catalog")]
        public IActionResult GetCatalog() =>
            catalogService.IsLoaded
                ? Ok(PublicView(catalogService.Catalog))
                : StatusCode(503, new { error = "Catalog is not loaded." });

        [HttpPost("query")]
        public Task<IActionResult> Query([FromBody] QueryRequest req, CancellationToken ct) =>
            Run("query", req.Entity, () => queryService.QueryAsync(req, ct));

        [HttpPost("find")]
        public Task<IActionResult> Find([FromBody] FindRequest req, CancellationToken ct) =>
            Run("find", null, () => queryService.FindAsync(req, ct));

        [HttpPost("count")]
        public Task<IActionResult> Count([FromBody] CountRequest req, CancellationToken ct) =>
            Run("count", req.Entity, () => queryService.CountAsync(req, ct));

        // ---------- One place that turns exceptions into HTTP codes ----------
        private async Task<IActionResult> Run<T>(string tool, string? entity, Func<Task<T>> action)
        {
            if (!catalogService.IsLoaded)
                return StatusCode(503, new { error = "Catalog is not loaded." });

            try
            {
                var result = await action();
                logger.LogInformation("agent {Tool} {Entity} OK", tool, entity);
                return Ok(result);
            }
            catch (AgentRequestException ex)
            {
                logger.LogInformation("agent {Tool} {Entity} 400: {Msg}", tool, entity, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning("agent {Tool} {Entity} 504 timeout", tool, entity);
                return StatusCode(504, new { error = ex.Message });
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                return new EmptyResult();   // caller hung up, nobody is listening
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "agent {Tool} {Entity} 500", tool, entity);
                return StatusCode(500, new { error = "Internal error. Try a different query." });
            }
        }

        // ---------- What the model is allowed to see ----------
        private static object PublicView(AgentCatalog cat) => new
        {
            version = cat.Version,
            entities = cat.Entities.Values.Select(e => new
            {
                name = e.Name,
                description = e.Description,
                key = e.Key,
                links = e.Links.Select(l => new { to = l.To, via = l.Via, kind = l.Kind }),
                columns = e.Columns.Values.Select(c => new
                {
                    name = c.Name,
                    type = c.Type,
                    description = c.Description,
                    synonyms = c.Synonyms,
                    filterable = c.Filterable,
                    searchable = c.Searchable,
                    identifier = c.Identifier,
                    groupable = c.Groupable,
                    list = c.List,
                    masked = c.Mask is not null,
                    allowedValues = c.AllowedValues
                })
            })
        };
    }
}