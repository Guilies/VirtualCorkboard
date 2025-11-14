using System;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Services;

namespace VirtualCorkboard.Controls
{
 public partial class SidebarControl : UserControl
 {
 public event EventHandler? AddTextNoteRequested;
 public IThemeManager? ThemeManager { get; set; }

 public SidebarControl()
 {
 InitializeComponent();
 }

 private void Quit_Click(object sender, RoutedEventArgs e)
 {
 var app = Application.Current;
 if (app?.MainWindow != null)
 app.MainWindow.Close();
 else
 app?.Shutdown();
 }

 private void AddTextNote_Click(object sender, RoutedEventArgs e)
 {
 AddTextNoteRequested?.Invoke(this, EventArgs.Empty);
 }

 private void Close_Click(object sender, RoutedEventArgs e)
 {
 this.Visibility = this.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
 }
 }
}
