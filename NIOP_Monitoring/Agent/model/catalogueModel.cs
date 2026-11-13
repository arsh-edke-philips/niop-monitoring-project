using System.Text.Json.Serialization;

namespace NIOP_Monitoring.Agent
{
    public class AgentCatalog
    {
        public int Version { get; set; }
        public required Dictionary<string, EntityDef> Entities { get; set; }
    }

    public class EntityDef
    {
        public required string Description { get; set; }
        public required string SqlFile { get; set; }
        public required string Key { get; set; }
        public required SortDef DefaultSort { get; set; }
        public List<LinkDef> Links { get; set; } = [];
        public required Dictionary<string, ColumnDef> Columns { get; set; }

        // Filled in by CatalogService at startup, never read from JSON
        [JsonIgnore] public string Name { get; set; } = "";
        [JsonIgnore] public string SqlText { get; set; } = "";
    }

    public class SortDef
    {
        public required string Column { get; set; }
        public string Direction { get; set; } = "asc";
    }

    public class LinkDef
    {
        public required string To { get; set; }
        public required string Via { get; set; }
        public required string Kind { get; set; }   // "many-to-one" or "one-to-many"
    }

    public class ColumnDef
    {
        public string Type { get; set; } = "string";
        public required string Description { get; set; }
        public List<string> Synonyms { get; set; } = [];
        public bool Filterable { get; set; } = true;
        public bool Searchable { get; set; }
        public bool Identifier { get; set; }
        public bool LoadValues { get; set; }
        public bool Groupable { get; set; }
        public bool List { get; set; }
        public bool Ansi { get; set; }
        public string? Mask { get; set; }           // null = not masked, "last4" = show last 4 chars

        // Filled in by CatalogService at startup, never read from JSON
        [JsonIgnore] public string Name { get; set; } = "";
        [JsonIgnore] public List<string>? AllowedValues { get; set; }
    }

    // Thrown when the model sends a bad request (unknown entity, column, bad value...).
    // The controller turns it into HTTP 400 so the model can read the message and fix its call.
    public class AgentRequestException(string message) : Exception(message);
}