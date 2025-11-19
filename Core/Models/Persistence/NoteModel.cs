using System;
using System.Text.Json.Serialization;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Persistence.Models
{
    // Use a distinct discriminator name to avoid colliding with the Kind property.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$noteType")]
    [JsonDerivedType(typeof(TextNoteModel), "Text")]
    // Future derived types:
    // [JsonDerivedType(typeof(ImageNoteModel), "Image")]
    // [JsonDerivedType(typeof(AudioNoteModel), "Audio")]
    public abstract class NoteModel
    {
        public Guid Id { get; set; }
        public NoteKind Kind { get; set; } // business enum separate from discriminator
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Z { get; set; }
    }
}
