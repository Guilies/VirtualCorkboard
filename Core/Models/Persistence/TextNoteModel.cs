namespace VirtualCorkboard.Persistence.Models
{
    public class TextNoteModel : NoteModel
    {
        public string? Text { get; set; }
        public TextFormatModel? Format { get; set; }
    }
}
