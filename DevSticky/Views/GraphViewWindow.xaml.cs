using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;

namespace DevSticky.Views;

/// <summary>
/// Window displaying a visual network diagram of notes and their connections (Requirements 7.8, 7.9)
/// </summary>
public partial class GraphViewWindow : Window
{
    /// <summary>
    /// Event raised when a note node is clicked to navigate
    /// </summary>
    public event EventHandler<Guid>? NoteClicked;

    private ILinkService? _linkService;
    private INoteService? _noteService;
    private NoteGraph? _graph;
    
    // Zoom and pan state
    private double _zoom = 1.0;
    private const double MinZoom = 0.2;
    private const double MaxZoom = 3.0;
    private const double ZoomStep = 0.1;
    
    private bool _isPanning;
    private System.Windows.Point _lastMousePosition;
    
    // Node layout
    private readonly Dictionary<Guid, System.Windows.Point> _nodePositions = new();
    private readonly Dictionary<Guid, Ellipse> _nodeElements = new();
    private const double NodeRadius = 25;
    private const double NodeSpacing = 100;

    public GraphViewWindow(ILinkService? linkService = null, INoteService? noteService = null)
    {
        InitializeComponent();
        
        try
        {
            _linkService = linkService ?? App.GetService<ILinkService>();
            _noteService = noteService ?? App.GetService<INoteService>();
        }
        catch
        {
            // Services not available during design time
        }
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadGraphAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load graph: {ex.Message}");
            StatusText.Text = $"Error loading graph: {ex.Message}";
        }
    }

    /// <summary>
    /// Load and render the note graph
    /// </summary>
    private async Task LoadGraphAsync()
    {
        if (_linkService == null)
        {
            StatusText.Text = L.Get("LinkServiceNotAvailable");
            return;
        }

        StatusText.Text = L.Get("BuildingGraph");
        
        try
        {
            _graph = await _linkService.BuildGraphAsync();
            RenderGraph();
            StatusText.Text = $"{_graph.Nodes.Count} notes, {_graph.Edges.Count} links";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Render the graph on the canvas
    /// </summary>
    private void RenderGraph()
    {
        GraphCanvas.Children.Clear();
        _nodePositions.Clear();
        _nodeElements.Clear();

        if (_graph == null || _graph.Nodes.Count == 0)
        {
            StatusText.Text = L.Get("NoNotesToDisplay");
            return;
        }

        // Calculate node positions using a simple force-directed layout
        CalculateNodePositions();

        // Draw edges first (so they appear behind nodes)
        DrawEdges();

        // Draw nodes
        DrawNodes();
    }

    /// <summary>
    /// Calculate node positions using a simple circular/force-directed layout
    /// </summary>
    private void CalculateNodePositions()
    {
        if (_graph == null) return;

        var nodes = _graph.Nodes.ToList();
        var centerX = ActualWidth / 2;
        var centerY = (ActualHeight - 80) / 2; // Account for toolbar and status bar
        
        if (nodes.Count == 1)
        {
            _nodePositions[nodes[0].NoteId] = new System.Windows.Point(centerX, centerY);
            return;
        }

        // Use circular layout for simplicity
        var radius = Math.Min(centerX, centerY) * 0.7;
        var angleStep = 2 * Math.PI / nodes.Count;

        for (int i = 0; i < nodes.Count; i++)
        {
            var angle = i * angleStep - Math.PI / 2; // Start from top
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            _nodePositions[nodes[i].NoteId] = new System.Windows.Point(x, y);
        }

        // Apply simple force-directed adjustments
        ApplyForceDirectedLayout(5);
    }

    /// <summary>
    /// Apply force-directed layout adjustments
    /// </summary>
    private void ApplyForceDirectedLayout(int iterations)
    {
        if (_graph == null) return;

        var nodes = _graph.Nodes.ToList();
        var edges = _graph.Edges.ToList();
        
        for (int iter = 0; iter < iterations; iter++)
        {
            var forces = new Dictionary<Guid, System.Windows.Point>();
            foreach (var node in nodes)
            {
                forces[node.NoteId] = new System.Windows.Point(0, 0);
            }

            // Repulsion between all nodes
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var pos1 = _nodePositions[nodes[i].NoteId];
                    var pos2 = _nodePositions[nodes[j].NoteId];
                    
                    var dx = pos2.X - pos1.X;
                    var dy = pos2.Y - pos1.Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1);
                    
                    var repulsion = 5000 / (dist * dist);
                    var fx = dx / dist * repulsion;
                    var fy = dy / dist * repulsion;
                    
                    forces[nodes[i].NoteId] = new System.Windows.Point(
                        forces[nodes[i].NoteId].X - fx,
                        forces[nodes[i].NoteId].Y - fy);
                    forces[nodes[j].NoteId] = new System.Windows.Point(
                        forces[nodes[j].NoteId].X + fx,
                        forces[nodes[j].NoteId].Y + fy);
                }
            }

            // Attraction along edges
            foreach (var edge in edges)
            {
                if (!_nodePositions.ContainsKey(edge.SourceId) || 
                    !_nodePositions.ContainsKey(edge.TargetId))
                    continue;

                var pos1 = _nodePositions[edge.SourceId];
                var pos2 = _nodePositions[edge.TargetId];
                
                var dx = pos2.X - pos1.X;
                var dy = pos2.Y - pos1.Y;
                var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1);
                
                var attraction = dist / 50;
                var fx = dx / dist * attraction;
                var fy = dy / dist * attraction;
                
                forces[edge.SourceId] = new System.Windows.Point(
                    forces[edge.SourceId].X + fx,
                    forces[edge.SourceId].Y + fy);
                forces[edge.TargetId] = new System.Windows.Point(
                    forces[edge.TargetId].X - fx,
                    forces[edge.TargetId].Y - fy);
            }

            // Apply forces
            foreach (var node in nodes)
            {
                var pos = _nodePositions[node.NoteId];
                var force = forces[node.NoteId];
                
                // Damping
                var newX = pos.X + force.X * 0.1;
                var newY = pos.Y + force.Y * 0.1;
                
                // Keep within bounds
                newX = Math.Max(NodeRadius, Math.Min(ActualWidth - NodeRadius, newX));
                newY = Math.Max(NodeRadius, Math.Min(ActualHeight - 80 - NodeRadius, newY));
                
                _nodePositions[node.NoteId] = new System.Windows.Point(newX, newY);
            }
        }
    }

    /// <summary>
    /// Draw edges between connected nodes
    /// </summary>
    private void DrawEdges()
    {
        if (_graph == null) return;

        var edgeBrush = (System.Windows.Media.Brush)FindResource("Surface2Brush");

        foreach (var edge in _graph.Edges)
        {
            if (!_nodePositions.TryGetValue(edge.SourceId, out var sourcePos) ||
                !_nodePositions.TryGetValue(edge.TargetId, out var targetPos))
                continue;

            var line = new Line
            {
                X1 = sourcePos.X,
                Y1 = sourcePos.Y,
                X2 = targetPos.X,
                Y2 = targetPos.Y,
                Stroke = edgeBrush,
                StrokeThickness = 1.5,
                Opacity = 0.6
            };

            // Add arrowhead
            var angle = Math.Atan2(targetPos.Y - sourcePos.Y, targetPos.X - sourcePos.X);
            var arrowLength = 10;
            var arrowAngle = Math.PI / 6;
            
            var arrowX = targetPos.X - NodeRadius * Math.Cos(angle);
            var arrowY = targetPos.Y - NodeRadius * Math.Sin(angle);
            
            var arrow = new Polygon
            {
                Points = new PointCollection
                {
                    new System.Windows.Point(arrowX, arrowY),
                    new System.Windows.Point(arrowX - arrowLength * Math.Cos(angle - arrowAngle),
                              arrowY - arrowLength * Math.Sin(angle - arrowAngle)),
                    new System.Windows.Point(arrowX - arrowLength * Math.Cos(angle + arrowAngle),
                              arrowY - arrowLength * Math.Sin(angle + arrowAngle))
                },
                Fill = edgeBrush,
                Opacity = 0.6
            };

            GraphCanvas.Children.Add(line);
            GraphCanvas.Children.Add(arrow);
        }
    }

    /// <summary>
    /// Draw note nodes
    /// </summary>
    private void DrawNodes()
    {
        if (_graph == null) return;

        var nodeFill = (System.Windows.Media.Brush)FindResource("Surface1Brush");
        var nodeStroke = (System.Windows.Media.Brush)FindResource("Surface2Brush");
        var textBrush = (System.Windows.Media.Brush)FindResource("TextBrush");
        var accentBrush = (System.Windows.Media.Brush)FindResource("BlueBrush");

        foreach (var node in _graph.Nodes)
        {
            if (!_nodePositions.TryGetValue(node.NoteId, out var pos))
                continue;

            // Node circle
            var ellipse = new Ellipse
            {
                Width = NodeRadius * 2,
                Height = NodeRadius * 2,
                Fill = nodeFill,
                Stroke = node.LinkCount > 0 || node.BacklinkCount > 0 ? accentBrush : nodeStroke,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = node.NoteId
            };

            Canvas.SetLeft(ellipse, pos.X - NodeRadius);
            Canvas.SetTop(ellipse, pos.Y - NodeRadius);

            ellipse.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            ellipse.MouseEnter += Node_MouseEnter;
            ellipse.MouseLeave += Node_MouseLeave;

            _nodeElements[node.NoteId] = ellipse;
            GraphCanvas.Children.Add(ellipse);

            // Node label
            var label = new TextBlock
            {
                Text = TruncateTitle(node.Title, 15),
                Foreground = textBrush,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 100,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, pos.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, pos.Y + NodeRadius + 5);

            GraphCanvas.Children.Add(label);
        }
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        if (title.Length <= maxLength)
            return title;
        return title[..(maxLength - 3)] + "...";
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is Guid noteId)
        {
            NoteClicked?.Invoke(this, noteId);
            e.Handled = true;
        }
    }

    private void Node_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Ellipse ellipse)
        {
            ellipse.StrokeThickness = 3;
            
            if (ellipse.Tag is Guid noteId && _graph != null)
            {
                var node = _graph.Nodes.FirstOrDefault(n => n.NoteId == noteId);
                if (node != null)
                {
                    StatusText.Text = $"{node.Title} - {node.LinkCount} links, {node.BacklinkCount} backlinks";
                }
            }
        }
    }

    private void Node_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Ellipse ellipse)
        {
            ellipse.StrokeThickness = 2;
            
            if (_graph != null)
            {
                StatusText.Text = $"{_graph.Nodes.Count} notes, {_graph.Edges.Count} links";
            }
        }
    }

    #region Window Controls

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            BtnMaximize.Content = "☐";
        }
        else
        {
            WindowState = WindowState.Maximized;
            BtnMaximize.Content = "❐";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Zoom and Pan (Requirements 7.9)

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom + ZoomStep);
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom - ZoomStep);
    }

    private void BtnResetView_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
        TranslateTransform.X = 0;
        TranslateTransform.Y = 0;
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadGraphAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh graph: {ex.Message}");
            StatusText.Text = $"Error refreshing graph: {ex.Message}";
        }
    }

    private void SetZoom(double newZoom)
    {
        _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
        ScaleTransform.ScaleX = _zoom;
        ScaleTransform.ScaleY = _zoom;
    }

    private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
        SetZoom(_zoom + delta);
        e.Handled = true;
    }

    private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source == GraphCanvas)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(this);
            GraphCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            GraphCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void GraphCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPanning)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _lastMousePosition;
            
            TranslateTransform.X += delta.X;
            TranslateTransform.Y += delta.Y;
            
            _lastMousePosition = currentPosition;
        }
    }

    #endregion
}
