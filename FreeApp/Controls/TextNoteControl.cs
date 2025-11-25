using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VirtualCorkboard.Controls;
using Brushes = System.Windows.Media.Brushes;
using TextBox = System.Windows.Controls.TextBox;

namespace VirtualCorkboard.Free.Controls
{
    // Free extension: concrete TextNote on top of Core BaseNoteControl
    public class TextNoteControl : BaseNoteControl
    {
        public static readonly DependencyProperty NoteTextProperty =
            DependencyProperty.Register(nameof(NoteText), typeof(string), typeof(TextNoteControl), new PropertyMetadata("", OnNoteTextChanged));

        public string NoteText
        {
            get => (string)GetValue(NoteTextProperty);
            set => SetValue(NoteTextProperty, value);
        }

        private readonly TextBox _textBox;
        private MouseButtonEventHandler? _outsideClickHandler;

        public TextNoteControl()
        {
            _textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                AcceptsReturn = true,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 30,
                Padding = new Thickness(4),
                IsReadOnly = true,
                IsHitTestVisible = false,
                Text = NoteText
            };

            _textBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(NoteText))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });

            _textBox.TextChanged += (_, _) => TextEdited?.Invoke(this);
            _textBox.MouseDoubleClick += (s, e) => { EnterEditMode(); _textBox.SelectAll(); };
            _textBox.PreviewMouseLeftButtonDown += (s, e) => { if (_textBox.IsReadOnly) OnMouseLeftButtonDown(e); };
            _textBox.PreviewMouseMove += (s, e) => { if (_textBox.IsReadOnly) OnMouseMove(e); };
            _textBox.PreviewMouseLeftButtonUp += (s, e) => { if (_textBox.IsReadOnly) OnMouseLeftButtonUp(e); };

            Content = _textBox;
        }

        public static event System.Action<TextNoteControl>? TextEdited;
        private static void OnNoteTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Could be used for future formatting dirty tracking
        }

        protected override void EnterEditMode()
        {
            if (!_textBox.IsReadOnly) return;
            _textBox.IsReadOnly = false;
            _textBox.IsHitTestVisible = true;
            _textBox.Focus();
            _textBox.SelectAll();
            try { Mouse.Capture(this, CaptureMode.SubTree); } catch { }
            _outsideClickHandler ??= GlobalWindowClickWhileEditing;
            AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, _outsideClickHandler, handledEventsToo: true);
            System.Windows.Application.Current.MainWindow.PreviewMouseDown += GlobalWindowClickWhileEditing;
        }

        protected override void ExitEditMode()
        {
            if (_textBox.IsReadOnly) return;
            if (_outsideClickHandler != null)
            {
                RemoveHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, _outsideClickHandler);
            }
            System.Windows.Application.Current.MainWindow.PreviewMouseDown -= GlobalWindowClickWhileEditing;
            if (Mouse.Captured == this) { Mouse.Capture(null); }
            _textBox.IsReadOnly = true;
            _textBox.IsHitTestVisible = false;
            _textBox.SelectionLength = 0;
            _textBox.SelectionStart = _textBox.CaretIndex;
            if (_textBox.IsKeyboardFocusWithin) { Keyboard.ClearFocus(); }
            base.ExitEditMode();
            Debug.WriteLine("[TextNoteControl] Exiting edit mode, caret hidden and focus cleared.");
        }

        private void GlobalWindowClickWhileEditing(object? sender, MouseButtonEventArgs e)
        {
            if (!_textBox.IsReadOnly)
            {
                ExitEditMode();
                e.Handled = false;
            }
        }
    }
}
