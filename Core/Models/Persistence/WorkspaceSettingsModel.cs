namespace VirtualCorkboard.Persistence.Models
{
    public class WorkspaceSettingsModel
    {
        public double Zoom { get; set; } =1.0;
        public double PanX { get; set; } =0.0;
        public double PanY { get; set; } =0.0;
        public string? Theme { get; set; }
        public string? LastMediaFolder { get; set; }
    }
}
