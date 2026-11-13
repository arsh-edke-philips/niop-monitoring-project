using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace NIOP_Monitoring.Agent
{
    public partial class CatalogService(IConfiguration config, IWebHostEnvironment env, ILogger<CatalogService> logger)
    {
        private static readonly HashSet<string> AllowedTypes = new HashSet<string> { "string", "int", "decimal", "date", "bool", "guid" };
        private static readonly HashSet<string> AllowedKinds = new HashSet<string> { "many-to-one", "one-to-many", "one-to-one", "many-to-many" };
        private const int MaxAllowedValues = 50;

        [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
        private static partial Regex SafeName();

        private AgentCatalog? _catalog;
        public bool IsLoaded => _catalog is not null;
        public AgentCatalog Catalog => _catalog ?? throw new InvalidOperationException("Agent catalog is not loaded.");

        public async Task LoadAsync(CancellationToken ct)
        {
            var agentDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Agent"));
            var catalog = await ReadCatalogAsync(agentDir, ct);

            Validate(catalog);

            var connStr = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            foreach (var e in catalog.Entities.Values)
            {
                await TestRunSqlAsync(conn, e, ct);
                await LoadAllowedValuesAsync(conn, e, ct);
            }

            _catalog = catalog;
            logger.LogInformation("Agent catalog loaded: {Count} entities ({Names})",
                catalog.Entities.Count, string.Join(", ", catalog.Entities.Keys));
        }

        // ---------- 1. Read JSON + SQL files ----------
        private static async Task<AgentCatalog> ReadCatalogAsync(string agentDir, CancellationToken ct)
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };

            var json = await File.ReadAllTextAsync(Path.Combine(agentDir, "catalogue.json"), ct);
            var raw = JsonSerializer.Deserialize<AgentCatalog>(json, options)
                ?? throw new InvalidOperationException("catalog.json is empty.");

            var catalog = new AgentCatalog
            {
                Version = raw.Version,
                Entities = new Dictionary<string, EntityDef>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var (name, e) in raw.Entities)
            {
                e.Name = name;
                e.Columns = new Dictionary<string, ColumnDef>(e.Columns, StringComparer.OrdinalIgnoreCase);
                foreach (var (colName, c) in e.Columns)
                {
                    c.Name = colName;
                    if (c.List) c.Filterable = false;   // lists are display-only
                }

                var sqlPath = Path.GetFullPath(Path.Combine(agentDir, e.SqlFile));
                if (!sqlPath.StartsWith(agentDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"{name}: sqlFile must be inside the Agent folder.");
                e.SqlText = await File.ReadAllTextAsync(sqlPath, ct);

                catalog.Entities.Add(name, e);   // Add (not indexer) throws on "report" + "Report"
            }
            return catalog;
        }

        // ---------- 2. Rule checks (no database needed) ----------
        private static void Validate(AgentCatalog cat)
        {
            var errors = new List<string>();
            if (cat.Entities.Count == 0) errors.Add("Catalog has no entities.");

            foreach (var e in cat.Entities.Values)
            {
                var p = e.Name;
                if (!SafeName().IsMatch(p)) errors.Add($"{p}: entity name has invalid characters.");
                if (e.Columns.Count == 0) errors.Add($"{p}: has no columns.");

                foreach (var c in e.Columns.Values)
                {
                    var q = $"{p}.{c.Name}";
                    if (!SafeName().IsMatch(c.Name)) errors.Add($"{q}: column name has invalid characters.");
                    if (!AllowedTypes.Contains(c.Type)) errors.Add($"{q}: unknown type '{c.Type}'. Allowed: {string.Join(", ", AllowedTypes)}.");
                    if (c.Searchable && c.Type != "string") errors.Add($"{q}: only string columns can be searchable.");
                    if (c.LoadValues && (c.Type is "guid" or "date")) errors.Add($"{q}: loadValues is not allowed on {c.Type} columns.");
                    if (c.Mask is not null && c.Mask != "last4") errors.Add($"{q}: unknown mask '{c.Mask}'. Allowed: last4.");
                    if (c.Mask is not null && (c.Groupable || c.LoadValues)) errors.Add($"{q}: masked columns cannot be groupable or loadValues (would leak real values).");
                    if (c.List && (c.Searchable || c.Identifier || c.Groupable)) errors.Add($"{q}: list columns cannot be searchable, identifier or groupable.");
                }

                if (!e.Columns.TryGetValue(e.Key, out var key)) errors.Add($"{p}: key column '{e.Key}' is not in columns.");
                else if (key.List) errors.Add($"{p}: key column cannot be a list.");

                if (!e.Columns.ContainsKey(e.DefaultSort.Column)) errors.Add($"{p}: defaultSort column '{e.DefaultSort.Column}' is not in columns.");
                if (e.DefaultSort.Direction is not ("asc" or "desc")) errors.Add($"{p}: defaultSort direction must be 'asc' or 'desc'.");

                foreach (var l in e.Links)
                {
                    var q = $"{p} -> {l.To}";
                    if (!AllowedKinds.Contains(l.Kind)) errors.Add($"{q}: unknown link kind '{l.Kind}'.");
                    if (!e.Columns.TryGetValue(l.Via, out var src)) errors.Add($"{q}: via column '{l.Via}' is not in {p}.");
                    if (!cat.Entities.TryGetValue(l.To, out var target)) { errors.Add($"{q}: target entity does not exist."); continue; }
                    if (!target.Columns.TryGetValue(l.Via, out var dst) || !dst.Filterable)
                        errors.Add($"{q}: via '{l.Via}' must be a filterable column on {l.To}.");
                    else if (src is not null && src.Type != dst.Type)
                        errors.Add($"{q}: via '{l.Via}' is {src.Type} in {p} but {dst.Type} in {l.To}.");
                }
            }

            if (errors.Count > 0)
                throw new InvalidOperationException("catalog.json is invalid:\n - " + string.Join("\n - ", errors));
        }

        // ---------- 3. Test-run each SQL file ----------
        private async Task TestRunSqlAsync(SqlConnection conn, EntityDef e, CancellationToken ct)
        {
            var sql = $"SELECT TOP (0) * FROM (\n{e.SqlText}\n) AS x";
            var returned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                for (var i = 0; i < reader.FieldCount; i++) returned.Add(reader.GetName(i));
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"{e.Name}: {e.SqlFile} failed to run: {ex.Message}", ex);
            }

            var missing = e.Columns.Keys.Where(k => !returned.Contains(k)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"{e.Name}: columns not returned by {e.SqlFile}: {string.Join(", ", missing)}");

            var extra = returned.Where(r => !e.Columns.ContainsKey(r)).ToList();
            if (extra.Count > 0)
                logger.LogWarning("{Entity}: {File} returns columns not in the catalog (never exposed): {Cols}",
                    e.Name, e.SqlFile, string.Join(", ", extra));
        }

        // ---------- 4. Load allowed values ----------
        private async Task LoadAllowedValuesAsync(SqlConnection conn, EntityDef e, CancellationToken ct)
        {
            foreach (var c in e.Columns.Values.Where(c => c.LoadValues))
            {
                // Safe to put c.Name in SQL: it passed SafeName() and the test run proved it exists.
                var sql = $"SELECT DISTINCT TOP ({MaxAllowedValues + 1}) x.[{c.Name}] FROM (\n{e.SqlText}\n) AS x " +
                          $"WHERE x.[{c.Name}] IS NOT NULL ORDER BY x.[{c.Name}]";
                var values = new List<string>();
                await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 })
                await using (var reader = await cmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                        values.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!);
                }

                if (values.Count > MaxAllowedValues)
                {
                    logger.LogWarning("{Entity}.{Col}: more than {Max} distinct values; not sent to the model.", e.Name, c.Name, MaxAllowedValues);
                    c.AllowedValues = null;
                }
                else c.AllowedValues = values;
            }
        }
    }
}