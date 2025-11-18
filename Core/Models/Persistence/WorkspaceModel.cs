using System;
using System.Collections.Generic;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Persistence.Models
{
    public class WorkspaceModel
    {
        public string Version { get; set; } = PersistenceConstants.SaveVersion;
        public List<NoteModel> Notes { get; set; } = new();
        public List<TwineConnectionModel> Connections { get; set; } = new();
        public WorkspaceSettingsModel Settings { get; set; } = new();
    }
}
