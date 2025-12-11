using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace DevSticky.Helpers;

/// <summary>
/// Helper class for properly disposing WPF resources to prevent memory leaks
/// Requirements: 10.3
/// </summary>
public static class WpfResourceHelper
{
    /// <summary>
    /// Safely dispose of an AvalonEdit TextEditor and its resources
    /// </summary>
    /// <param name="editor">The TextEditor to dispose</param>
    public static void DisposeTextEditor(TextEditor? editor)
    {
        if (editor == null) return;

        try
        {
            // Clear large content from memory
            editor.Text = string.Empty;
            
            // Clear undo/redo stack to free memory
            editor.Document?.UndoStack?.ClearAll();
            
            // Clear syntax highlighting to release resources
            editor.SyntaxHighlighting = null;
            
            // Dispose if it implements IDisposable
            if (editor is IDisposable disposableEditor)
            {
                disposableEditor.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing TextEditor: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely dispose of a WebView2 control and its resources
    /// </summary>
    /// <param name="webView">The WebView2 to dispose</param>
    public static void DisposeWebView2(Microsoft.Web.WebView2.Wpf.WebView2? webView)
    {
        if (webView == null) return;

        try
        {
            // Navigate to about:blank to clear content
            if (webView.CoreWebView2 != null)
            {
                webView.NavigateToString("about:blank");
            }
            
            // Dispose the WebView2
            webView.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing WebView2: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively dispose of all disposable children in a visual tree
    /// </summary>
    /// <param name="parent">The parent element to start from</param>
    public static void DisposeVisualChildren(DependencyObject? parent)
    {
        if (parent == null) return;

        try
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                // Recursively dispose children first
                DisposeVisualChildren(child);
                
                // Dispose the child if it implements IDisposable
                if (child is IDisposable disposableChild)
                {
                    disposableChild.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing visual children: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all items from a ListBox and dispose any disposable items
    /// </summary>
    /// <param name="listBox">The ListBox to clear</param>
    public static void ClearAndDisposeListBox(System.Windows.Controls.ListBox? listBox)
    {
        if (listBox?.Items == null) return;

        try
        {
            // Dispose any disposable items
            foreach (var item in listBox.Items)
            {
                if (item is IDisposable disposableItem)
                {
                    disposableItem.Dispose();
                }
            }
            
            // Clear the items
            listBox.Items.Clear();
            listBox.ItemsSource = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing ListBox: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all children from a Panel and dispose any disposable children
    /// </summary>
    /// <param name="panel">The Panel to clear</param>
    public static void ClearAndDisposePanel(System.Windows.Controls.Panel? panel)
    {
        if (panel?.Children == null) return;

        try
        {
            // Dispose any disposable children
            foreach (UIElement child in panel.Children)
            {
                if (child is IDisposable disposableChild)
                {
                    disposableChild.Dispose();
                }
            }
            
            // Clear the children
            panel.Children.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing Panel: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely dispose of a Window and all its resources
    /// </summary>
    /// <param name="window">The Window to dispose</param>
    public static void DisposeWindow(Window? window)
    {
        if (window == null) return;

        try
        {
            // Dispose visual children first
            DisposeVisualChildren(window);
            
            // Clear data context if it's disposable
            if (window.DataContext is IDisposable disposableContext)
            {
                disposableContext.Dispose();
                window.DataContext = null;
            }
            
            // Dispose the window if it implements IDisposable
            if (window is IDisposable disposableWindow)
            {
                disposableWindow.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing Window: {ex.Message}");
        }
    }

    /// <summary>
    /// Force garbage collection and wait for finalizers
    /// Use sparingly and only when necessary for testing memory cleanup
    /// </summary>
    public static void ForceGarbageCollection()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during garbage collection: {ex.Message}");
        }
    }
}