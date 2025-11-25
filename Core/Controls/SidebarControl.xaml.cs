using System;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Services;

namespace VirtualCorkboard.Controls
{
     public partial class SidebarControl : UserControl
     {
         public event EventHandler? AddTextNoteRequested;
         public event EventHandler? NewWorkspaceRequested;
         public event EventHandler? OpenWorkspaceRequested;
         public event EventHandler? SaveWorkspaceRequested;
         public event EventHandler? SaveAsWorkspaceRequested;
         public IThemeManager? ThemeManager { get; set; }

         public SidebarControl()
         {
            InitializeComponent();
         }

         public void UpdateWorkspaceTitle(string? filePath, bool isDirty, bool isRecovery)
         {
             string displayName;
             if (string.IsNullOrWhiteSpace(filePath))
             {
                 displayName = isRecovery ? "(recovered - unsaved)" : "Untitled Workspace";
             }
             else
             {
                 displayName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                 if (isRecovery) displayName += " (recovered)";
             }
             if (isDirty) displayName += "*";
             WorkspaceTitleText.Text = displayName;
         }

         private void Quit_Click(object sender, RoutedEventArgs e)
         {
             var app = Application.Current;
             if (app?.MainWindow != null) app.MainWindow.Close();
             else app?.Shutdown();
         }

         private void AddTextNote_Click(object sender, RoutedEventArgs e) => AddTextNoteRequested?.Invoke(this, EventArgs.Empty);
         private void Close_Click(object sender, RoutedEventArgs e) => this.Visibility = this.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
         private void New_Click(object sender, RoutedEventArgs e) => NewWorkspaceRequested?.Invoke(this, EventArgs.Empty);
         private void Open_Click(object sender, RoutedEventArgs e) => OpenWorkspaceRequested?.Invoke(this, EventArgs.Empty);
         private void Save_Click(object sender, RoutedEventArgs e) => SaveWorkspaceRequested?.Invoke(this, EventArgs.Empty);
         private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveAsWorkspaceRequested?.Invoke(this, EventArgs.Empty);
     }
}
