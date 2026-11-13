using System.Text.Json;

namespace NIOP_Monitoring.Agent
{
    // ---------- Shared ----------
    public class FilterDto
    {
        public string Column { get; set; } = "";
        public string Op { get; set; } = "eq";
        public JsonElement Value { get; set; }
    }

    // ---------- /api/agent/query ----------
    public class QueryRequest
    {
        public string Entity { get; set; } = "";
        public List<FilterDto> Filters { get; set; } = [];
        public int? Limit { get; set; }
    }

    public class QueryResponse
    {
        public required string Entity { get; init; }
        public int RowCount { get; init; }
        public bool Truncated { get; init; }
        public required List<Dictionary<string, object?>> Rows { get; init; }
    }

    // ---------- /api/agent/find (Day 5) ----------
    public class FindRequest
    {
        public JsonElement Value { get; set; }
    }

    public class FindMatch
    {
        public required string Entity { get; init; }
        public required string Column { get; init; }
    }

    public class FindResponse
    {
        public required string Value { get; init; }
        public required List<FindMatch> Matches { get; init; }
    }

    // ---------- /api/agent/count (Day 5) ----------
    public class CountRequest
    {
        public string Entity { get; set; } = "";
        public List<FilterDto> Filters { get; set; } = [];
        public string? GroupBy { get; set; }
    }

    public class CountGroup
    {
        public object? Group { get; init; }
        public int Count { get; init; }
    }

    public class CountResponse
    {
        public required string Entity { get; init; }
        public int Total { get; init; }
        public bool GroupsTruncated { get; init; }
        public List<CountGroup>? Groups { get; init; }
    }

    // ---------- Helper ----------
    internal static class JsonText
    {
        // The model may send "5521" or 5521 or true. Turn any simple JSON value into text.
        public static string? Of(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => v.GetRawText(),
            _ => null   // missing, null, object, array → treated as "no value"
        };
    }
}