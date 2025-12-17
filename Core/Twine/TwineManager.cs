using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VirtualCorkboard.Controls;
using Cursors = System.Windows.Input.Cursors;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace VirtualCorkboard.Twine
{
     public class TwineManager
     {
         private readonly Canvas _twineCanvas;
         private readonly Dictionary<TwineConnection, Line> _twineLines = new();
         private readonly Dictionary<Line, TwineConnection> _lineToConnection = new();
         private readonly HashSet<TwineConnection> _selectedConnections = new();
         private readonly Dictionary<TwineConnection, Line> _selectionOverlays = new();
         private Line? _ghostLine;
         private PinControl? _dragSourcePin;
         private int _layoutGeneration;
         private readonly Dictionary<PinControl, (GeneralTransform Transform, int Generation)> _transformCache = new();
         public bool HasSelectedConnections => _selectedConnections.Count >0;
         public event EventHandler? TwineSelectionChanged;

         public TwineManager(Canvas twineCanvas)
         {
             _twineCanvas = twineCanvas;
             _twineCanvas.LayoutUpdated += (_, _) => { _layoutGeneration++; };
             _twineCanvas.MouseLeftButtonDown += (s, e) =>
             {
                 if (ReferenceEquals(e.Source, _twineCanvas))
                 {
                    ClearSelection();
                 }
             };
         }

         // New: enumerate all active connections for persistence
         public IEnumerable<TwineConnection> EnumerateConnections() => _twineLines.Keys.ToList();

         /// <summary>
         /// Gets a list of currently selected connections for deletion/commands.
         /// </summary>
         public IEnumerable<TwineConnection> GetSelectedConnections() => _selectedConnections.ToList();

         public void StartTwineConnection(PinControl sourcePin, Point _)
         {
             _dragSourcePin = sourcePin;
             var start = GetPinPositionOnTwineCanvas(sourcePin);
             _ghostLine = CreateTwineLine(start, start, new TwineStyle());
             _ghostLine.IsHitTestVisible = false;
             _twineCanvas.Children.Add(_ghostLine);
         }

         public void UpdateGhostLine(Point currentPoint)
         {
             if (_ghostLine != null)
             {
                 _ghostLine.X2 = currentPoint.X;
                 _ghostLine.Y2 = currentPoint.Y;
             }
         }

         /// <summary>
         /// Completes the twine drag interaction by cleaning up ghost line.
         /// Returns the source and target pins for command creation, or null if drag is invalid.
         /// </summary>
         public (PinControl source, PinControl target)? CompleteConnection(PinControl targetPin)
         {
             if (_dragSourcePin == null || _ghostLine == null)
             {
                return null;
             }

             var sourcePin = _dragSourcePin;
             
             // Clean up ghost line
             _twineCanvas.Children.Remove(_ghostLine);
             _ghostLine = null;
             _dragSourcePin = null;
             
             // Return pins for command creation
             return (sourcePin, targetPin);
         }

         public void CancelConnection()
         {
             if (_ghostLine != null)
             {
                 _twineCanvas.Children.Remove(_ghostLine);
                 _ghostLine = null;
                 _dragSourcePin = null;
             }
         }

         public void UpdateConnectionPosition(TwineConnection connection)
         {
             if (_twineLines.TryGetValue(connection, out var line))
             {
                 var sourcePos = GetPinPositionOnTwineCanvas(connection.SourcePin);
                 var targetPos = GetPinPositionOnTwineCanvas(connection.TargetPin);

                 line.X1 = sourcePos.X;
                 line.Y1 = sourcePos.Y;
                 line.X2 = targetPos.X;
                 line.Y2 = targetPos.Y;

                 if (_selectionOverlays.TryGetValue(connection, out var overlay) && overlay != null)
                 {
                     overlay.X1 = sourcePos.X;
                     overlay.Y1 = sourcePos.Y;
                     overlay.X2 = targetPos.X;
                     overlay.Y2 = targetPos.Y;
                 }
             }
         }

         public void AddConnection(TwineConnection connection)
         {
             var start = GetPinPositionOnTwineCanvas(connection.SourcePin);
             var end = GetPinPositionOnTwineCanvas(connection.TargetPin);

             var line = new Line
             {
                 X1 = start.X,
                 Y1 = start.Y,
                 X2 = end.X,
                 Y2 = end.Y,
                 Stroke = new SolidColorBrush(connection.Style.TwineColor),
                 StrokeThickness = connection.Style.Thickness,
                 StrokeDashArray = connection.Style.Texture switch
                 {
                     TwineTextureType.Dotted => new DoubleCollection {2,2},
                     TwineTextureType.Dashed => new DoubleCollection {6,2},
                     _ => null
                 },
                 StrokeStartLineCap = PenLineCap.Round,
                 StrokeEndLineCap = PenLineCap.Round
             };

             line.MouseLeftButtonDown += OnLineMouseLeftButtonDown;
             line.Cursor = Cursors.Hand;

             _twineLines[connection] = line;
             _lineToConnection[line] = connection;
             _twineCanvas.Children.Add(line);
         }

         public void UpdateAllConnectionsForPin(PinControl pin)
         {
             foreach (var conn in pin.OutgoingConnections.ToList())
             {
                UpdateConnectionPosition(conn);
             }

             foreach (var conn in pin.IncomingConnections.ToList())
             {
                UpdateConnectionPosition(conn);
             }
         }

         public void RequestUpdateForPin(PinControl pin) => UpdateAllConnectionsForPin(pin);

         public void RemoveAllConnectionsForPin(PinControl pin)
         {
             foreach (var conn in pin.OutgoingConnections.ToList())
             {
                RemoveConnection(conn);
             }

             foreach (var conn in pin.IncomingConnections.ToList())
             {
                RemoveConnection(conn);
             }

             _transformCache.Remove(pin);
         }

         public void RemoveConnection(TwineConnection connection)
         {
             connection.SourcePin.OutgoingConnections.Remove(connection);
             connection.TargetPin.IncomingConnections.Remove(connection);

             if (_selectedConnections.Contains(connection))
             {
                DeselectConnection(connection);
             }

             if (_twineLines.TryGetValue(connection, out var line))
             {
                 _twineCanvas.Children.Remove(line);
                 _twineLines.Remove(connection);
                 _lineToConnection.Remove(line);
             }
         }

         public void DeleteSelectedConnections()
         {
             if (_selectedConnections.Count ==0)
             {
                return;
             }

             var toDelete = _selectedConnections.ToList();
             ClearSelection();

             foreach (var conn in toDelete)
             {
                RemoveConnection(conn);
             }
         }

         private Line CreateTwineLine(Point start, Point end, TwineStyle style)
         {
             return new Line
             {
                 X1 = start.X,
                 Y1 = start.Y,
                 X2 = end.X,
                 Y2 = end.Y,
                 Stroke = new SolidColorBrush(style.TwineColor),
                 StrokeThickness = style.Thickness,
                 StrokeDashArray = style.Texture switch
                 {
                     TwineTextureType.Dotted => new DoubleCollection {2,2 },
                     TwineTextureType.Dashed => new DoubleCollection {6,2 },
                     _ => null
                 },
                 StrokeStartLineCap = PenLineCap.Round,
                 StrokeEndLineCap = PenLineCap.Round
             };
         }

         private void OnLineMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         {
             if (sender is not Line line)
             {
                return;
             }

             if (!_lineToConnection.TryGetValue(line, out var connection))
             {
                return;
             }

             bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

             if (ctrl)
             {
                ToggleSelection(connection);
             }
             else
             {
                ClearSelection();
                SelectConnection(connection);
             }

             TwineSelectionChanged?.Invoke(this, EventArgs.Empty);
             Keyboard.Focus(_twineCanvas);
             e.Handled = true;
         }

         private void ToggleSelection(TwineConnection connection)
         {
             if (_selectedConnections.Contains(connection))
             {
                DeselectConnection(connection);
             }
             else
             {
                SelectConnection(connection);
             }
         }

         private void SelectConnection(TwineConnection connection)
         {
             if (_selectedConnections.Contains(connection))
             {
                return;
             }

                _selectedConnections.Add(connection);

             if (!_twineLines.TryGetValue(connection, out var main))
             {
                return;
             }

             var start = new Point(main.X1, main.Y1);
             var end = new Point(main.X2, main.Y2);

             var overlay = new Line
             {
                 X1 = start.X,
                 Y1 = start.Y,
                 X2 = end.X,
                 Y2 = end.Y,
                 Stroke = new SolidColorBrush(connection.Style.HighlightColor),
                 StrokeThickness = main.StrokeThickness +2,
                 StrokeStartLineCap = PenLineCap.Round,
                 StrokeEndLineCap = PenLineCap.Round,
                 IsHitTestVisible = false
             };

             _twineCanvas.Children.Add(overlay);

             int mainZ = Panel.GetZIndex(main);
             Panel.SetZIndex(overlay, mainZ -1);

             _selectionOverlays[connection] = overlay;
         }

         private void DeselectConnection(TwineConnection connection)
         {
             if (!_selectedConnections.Remove(connection))
             {
                 return;
             }

             if (_selectionOverlays.TryGetValue(connection, out var overlay))
             {
                 _twineCanvas.Children.Remove(overlay);
                 _selectionOverlays.Remove(connection);
             }
         }

         public void ClearSelection()
         {
             if (_selectedConnections.Count ==0)
             {
                return;
             }

             foreach (var kvp in _selectionOverlays.ToList())
             {
                 _twineCanvas.Children.Remove(kvp.Value);
             }

             _selectionOverlays.Clear();
             _selectedConnections.Clear();
         }

         public void RefreshAllConnections()
         {
             foreach (var conn in _twineLines.Keys.ToList())
             {
                  UpdateConnectionPosition(conn);
             }
         }

         private Point GetPinPositionOnTwineCanvas(PinControl pin)
         {
             try
             {
                 var centerOnPin = new Point(pin.ActualWidth /2, pin.ActualHeight /2);

                 GeneralTransform transform;

                 if (_transformCache.TryGetValue(pin, out var cached) && cached.Generation == _layoutGeneration)
                 {
                    transform = cached.Transform;
                 }
                 else
                 {
                     transform = pin.TransformToVisual(_twineCanvas);
                     _transformCache[pin] = (transform, _layoutGeneration);
                     pin.LayoutUpdated += (s, e) => { _transformCache.Remove(pin); };
                 }
                 return transform.Transform(centerOnPin);
             }
             catch
             {
                 return new Point(0,0);
             }
         }
     }
}
