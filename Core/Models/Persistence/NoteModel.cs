using System;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Persistence.Models
{
     public abstract class NoteModel
     {
        public Guid Id { get; set; }
        public NoteKind Kind { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Z { get; set; }
     }
}
