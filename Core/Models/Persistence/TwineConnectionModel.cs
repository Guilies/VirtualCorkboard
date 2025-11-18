using System;

namespace VirtualCorkboard.Persistence.Models
{
    public class TwineConnectionModel
    {
        public Guid SourceNoteId { get; set; }
        public Guid TargetNoteId { get; set; }
        public TwineStyleModel Style { get; set; } = new();
    }
}
