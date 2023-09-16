using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tangerine.Manager.Mod
{
    internal class ModInfo
    {
        // Properties deserialized from mod.json
        public string Name { get; init; }
        public string Author { get; init; }
        public string Version { get; init; }
        public string Description { get; init; }
        public int NexusModId { get; init; }
        public string Link { get; init; }

        // Mod folder name (guaranteed to be unique at runtime)
        public string Id { get; init; }

        private bool _enabled;
        public bool IsEnabled
        {
            get => _enabled;
            set
            {
                if (value)
                {
                    DisabledReason = null;
                }

                _enabled = value;
            }
        }

        public string DisabledReason;

        public ModInfo()
        {

        }

        public ModInfo(string id, JsonObject obj)
        {
            Id = id;
            IsEnabled = true;

            Name = obj[nameof(Name)].Deserialize<string>();
            Author = obj[nameof(Author)].Deserialize<string>();
            Version = obj[nameof(Version)].Deserialize<string>();
            Description = obj[nameof(Description)].Deserialize<string>();
            NexusModId = obj[nameof(NexusModId)].Deserialize<int>();
            Link = obj[nameof(Link)].Deserialize<string>();
        }
    }
}
