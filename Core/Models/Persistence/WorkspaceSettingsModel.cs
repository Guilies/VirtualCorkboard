namespace VirtualCorkboard.Persistence.Models
{
    public class WorkspaceSettingsModel
    {
        public double Zoom { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public double WorkspaceWidth { get; set; } = 5000.0; // Default canvas size
        public double WorkspaceHeight { get; set; } = 5000.0; // Default canvas size
        public string? Theme { get; set; }
        public string? LastMediaFolder { get; set; }
    }
}
