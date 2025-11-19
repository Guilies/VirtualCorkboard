using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Serialization
{
    public class WorkspaceSerializer : IWorkspaceSerializer
    {
        private readonly JsonSerializerOptions _options;
        public WorkspaceSerializer()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _options.Converters.Add(new JsonStringEnumConverter());
        }
        public string Serialize(WorkspaceModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return JsonSerializer.Serialize(model, _options);
        }
        public WorkspaceModel Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty json", nameof(json));
            var model = JsonSerializer.Deserialize<WorkspaceModel>(json, _options) ?? new WorkspaceModel();
            return model;
        }
    }
}
