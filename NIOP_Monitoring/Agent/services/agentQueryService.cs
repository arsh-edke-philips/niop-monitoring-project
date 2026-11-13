using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;

namespace NIOP_Monitoring.Agent
{
    public class AgentQueryService
    {
        private readonly CatalogService _catalog;
        private readonly IDbConnection _dbConnection;

        public AgentQueryService(CatalogService catalog, IDbConnection dbConnection)
        {
            _catalog = catalog;
            _dbConnection = dbConnection;
        }

        private const int DefaultLimit = 10;
        private const int MaxLimit = 25;
        private const int MaxFilters = 10;
        private const int TimeoutSeconds = 15;
        private static readonly HashSet<string> Ops = ["eq", "contains"];

        private EntityDef GetEntity(string? name)
        {
            var entities = _catalog.Catalog.Entities;
            if (string.IsNullOrWhiteSpace(name) || !entities.TryGetValue(name.Trim(), out var entity))
                throw new AgentRequestException($"Unknown entity '{name}'. Valid: {string.Join(", ", entities.Keys)}.");
            return entity;
        }

        private static ColumnDef GetColumn(EntityDef e, string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || !e.Columns.TryGetValue(name.Trim(), out var column))
                throw new AgentRequestException($"Unknown column '{name}' for entity '{e.Name}'. Valid: {string.Join(", ", e.Columns.Keys)}.");
            return column;
        }

         // Text -> real .NET type for the column. null = "can't convert".
        private static object? ConvertValue(ColumnDef c, string text) => c.Type switch
        {
            "int"     => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            "decimal" => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null,
            "guid"    => Guid.TryParse(text, out var g) ? g : null,
            "date"    => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null,
            "bool"    => text.ToLowerInvariant() switch
                         {
                             "true" or "1" or "yes" => true,
                             "false" or "0" or "no" => false,
                             _ => null
                         },
            _         => text   // "string"
        };

        private static DbType DbTypeOf(ColumnDef c) => c.Type switch
        {
            "int"     => DbType.Int32,
            "decimal" => DbType.Decimal,
            "guid"    => DbType.Guid,
            "date"    => DbType.DateTime,
            "bool"    => DbType.Boolean,
            _         => c.Ansi ? DbType.AnsiString : DbType.String
        };

        private static string BuildWhere(EntityDef e, List<FilterDto> filters, DynamicParameters parameters, bool requireFilter = true)
        {
            if (filters is null || filters.Count == 0)
            {
                if (!requireFilter) return "";
                 throw new AgentRequestException(
                    $"At least one filter is required. Filterable columns on {e.Name}: " +
                    string.Join(", ", e.Columns.Values.Where(c => c.Filterable).Select(c => c.Name)) + ".");
            }
            if (filters.Count > MaxFilters)
                throw new AgentRequestException($"Too many filters (max {MaxFilters}).");

            var parts = new List<string>();
            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i] ?? throw new AgentRequestException($"Filter #{i + 1} is empty.");
                var col = GetColumn(e, f.Column);
                if (!col.Filterable)
                    throw new AgentRequestException(col.List
                        ? $"'{col.Name}' on {e.Name} is a list column and cannot be filtered. Query the linked entity instead."
                        : $"'{col.Name}' on {e.Name} cannot be filtered.");

                var op = (f.Op ?? "eq").Trim().ToLowerInvariant();
                if (!Ops.Contains(op))
                     throw new AgentRequestException($"Unknown op '{f.Op}'. Valid: eq, contains.");

                var text = JsonText.Of(f.Value)?.Trim();
                if (string.IsNullOrEmpty(text))
                    throw new AgentRequestException($"Filter on '{col.Name}' needs a value.");

                var paramName = $"p{i}";
                if (op == "contains")
                {
                    if (!col.Searchable)
                        throw new AgentRequestException(
                            $"'{col.Name}' does not support contains. Use eq, or one of: " +
                            string.Join(", ", e.Columns.Values.Where(x => x.Searchable).Select(x => x.Name)) + ".");

                    AddParam(parameters, paramName, col, "%" + EscapeLike(text) + "%");
                    parts.Add($"x.[{col.Name}] LIKE @{paramName} ESCAPE '\\'");
                }
                else
                {
                    var value = ConvertValue(col, text)
                        ?? throw new AgentRequestException($"'{text}' is not valid {col.Type} for {e.Name}.{col.Name}.");

                    AddParam(parameters, paramName, col, value);
                    parts.Add($"x.[{col.Name}] = @{paramName}");
                }
            }
            return string.Join(" AND ", parts);
        }

        internal static void AddParam(DynamicParameters p, string name, ColumnDef c, object value)
        {
            if (c.Type == "string") p.Add(name, value, DbTypeOf(c), size: c.Ansi ? 8000 : 4000);
            else p.Add(name, value, DbTypeOf(c));
        }

        private static string EscapeLike(string s) =>
            s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");

        private static object? Mask(ColumnDef c, object? v)
        {
            if (c.Mask is null || v is null) return v;
            var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            return s.Length <= 4 ? "****" : "***" + s[^4..];
        }

        public async Task<QueryResponse> QueryAsync(QueryRequest req, CancellationToken ct)
        {
            var entity = GetEntity(req.Entity);
            var limit = Math.Clamp(req.Limit ?? DefaultLimit, 1, MaxLimit);

            var p = new DynamicParameters();
            var where = BuildWhere(entity, req.Filters, p);
            p.Add("take", limit + 1, DbType.Int32);

            var cols = entity.Columns.Values.ToList();
            var select = string.Join(", ", cols.Select(c => $"x.[{c.Name}] AS [{c.Name}]"));

            var dir = entity.DefaultSort.Direction == "desc" ? "DESC" : "ASC";
            var orderBy = $"x.[{entity.DefaultSort.Column}] {dir}";
            if (!string.Equals(entity.DefaultSort.Column, entity.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                orderBy += $", x.[{entity.Key}]";
            }
            var sql = $"SELECT TOP (@take) {select}\nFROM (\n{entity.SqlText}\n) AS x\nWHERE {where}\nORDER BY {orderBy};";

            var raw = await RunAsync(sql, p, ct);

            var rows = raw.Take(limit)
                       .Select(r => cols.ToDictionary(c => c.Name, c => Mask(c, r[c.Name])))
                       .ToList();   

            return new QueryResponse
            {
                Entity = entity.Name,
                RowCount = rows.Count,
                Truncated = raw.Count > limit,
                Rows = rows
            };
        }

        private const int MaxIdLength = 100;
        private const int MaxGroups = 100;

        public async Task<FindResponse> FindAsync(FindRequest req, CancellationToken ct)
        {
            var text = JsonText.Of(req.Value)?.Trim();
            if (string.IsNullOrEmpty(text))
                throw new AgentRequestException("value is required: the ID exactly as the user typed it.");
            if (text.Length > MaxIdLength)
                throw new AgentRequestException($"value is too long (max {MaxIdLength} characters).");

            var candidates = new List<FindMatch>();
            var checks = new List<string>();
            var p = new DynamicParameters();

            foreach(var e in _catalog.Catalog.Entities.Values)
            foreach(var c in e.Columns.Values.Where(c => c.Identifier))
            {
                var value = ConvertValue(c, text);
                if (value is null) continue;

                var i = candidates.Count;
                AddParam(p, $"v{i}", c, value);
                checks.Add($"SELECT {i} AS [I] WHERE EXISTS (SELECT 1 FROM (\n{e.SqlText}\n) AS x WHERE x.[{c.Name}] = @v{i})");
                candidates.Add(new FindMatch{Entity = e.Name, Column = c.Name});  
            }

            if (checks.Count == 0)
            {
                return new FindResponse{Value = text, Matches = []};
            }

            var sql = string.Join("\n UNION ALL\n", checks) + "\n ORDER BY [I];";
            var raw = await RunAsync(sql, p, ct);

            return new FindResponse
            {
                Value = text,
                Matches = raw.Select(r => candidates[Convert.ToInt32(r["I"])]).ToList()
            };
        }

        public async Task<CountResponse> CountAsync(CountRequest req, CancellationToken ct)
        {
            var e = GetEntity(req.Entity);
            var p = new DynamicParameters();
            var where = BuildWhere(e, req.Filters, p, requireFilter: false);
            var from = $"FROM (\n{e.SqlText}\n) AS x" + (where.Length > 0 ? $"\nWHERE {where}" : "");

            // no grouping, just a total
            if (string.IsNullOrWhiteSpace(req.GroupBy))
            {
                var raw = await RunAsync($"SELECT COUNT(*) AS [Total]\n{from};", p, ct);
                return new CountResponse { Entity = e.Name, Total = Convert.ToInt32(raw[0]["Total"]) };
            }

            // grouped
            var g = GetColumn(e, req.GroupBy);
            if (!g.Groupable)
            {
                throw new AgentRequestException(
                    $"'{g.Name}' on {e.Name} cannot be used for groupBy. Groupable: " +
                    string.Join(", ", e.Columns.Values.Where(c => c.Groupable).Select(c => c.Name)) + ".");
            }

            p.Add("maxGroups", MaxGroups + 1, DbType.Int32);
            var sql =
                $"SELECT TOP (@maxGroups) x.[{g.Name}] AS [Group], COUNT(*) AS [Count], SUM(COUNT(*)) OVER () AS [Total]\n" +
                $"{from}\nGROUP BY x.[{g.Name}]\nORDER BY COUNT(*) DESC, x.[{g.Name}];";

            var rows = await RunAsync(sql, p, ct);
            return new CountResponse
            {
                Entity = e.Name,
                Total = rows.Count == 0 ? 0 : Convert.ToInt32(rows[0]["Total"]),
                Groups = rows.Take(MaxGroups)
                            .Select(r => new CountGroup{ Group = r["Group"], Count = Convert.ToInt32(r["Count"])})
                            .ToList(),
                GroupsTruncated = rows.Count > MaxGroups
            };
        }
        private async Task<List<IDictionary<string, object?>>> RunAsync(string sql, DynamicParameters p, CancellationToken ct)
        {
            try
            {
                var cmd = new CommandDefinition(sql, p, commandTimeout: TimeoutSeconds, cancellationToken: ct);
                var result = await _dbConnection.QueryAsync(cmd);
                return result.Cast<IDictionary<string, object?>>().ToList();
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                throw new TimeoutException("Query timed out. Add a more specific filter.", ex);
            }
        }
    }
}