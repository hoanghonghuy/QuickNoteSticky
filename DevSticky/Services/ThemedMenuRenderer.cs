using WinFormsColor = System.Drawing.Color;

namespace DevSticky.Services;

/// <summary>
/// Custom renderer for themed tray context menu (Requirements 4.1, 4.2, 4.3, 4.4)
/// </summary>
internal class ThemedMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private readonly WinFormsColor _backgroundColor;
    private readonly WinFormsColor _textColor;
    private readonly WinFormsColor _hoverColor;
    
    public ThemedMenuRenderer(WinFormsColor backgroundColor, WinFormsColor textColor, WinFormsColor hoverColor)
        : base(new ThemedColorTable(backgroundColor, hoverColor))
    {
        _backgroundColor = backgroundColor;
        _textColor = textColor;
        _hoverColor = hoverColor;
    }
    
    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        var rect = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
        var color = e.Item.Selected ? _hoverColor : _backgroundColor;
        
        using var brush = new System.Drawing.SolidBrush(color);
        e.Graphics.FillRectangle(brush, rect);
    }
    
    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = _textColor;
        base.OnRenderItemText(e);
    }
    
    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        var rect = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
        using var brush = new System.Drawing.SolidBrush(_backgroundColor);
        e.Graphics.FillRectangle(brush, rect);
        
        // Draw separator line
        var separatorColor = WinFormsColor.FromArgb(
            Math.Min(255, _hoverColor.R + 20),
            Math.Min(255, _hoverColor.G + 20),
            Math.Min(255, _hoverColor.B + 20));
        using var pen = new System.Drawing.Pen(separatorColor);
        var y = rect.Height / 2;
        e.Graphics.DrawLine(pen, 4, y, rect.Width - 4, y);
    }
}

/// <summary>
/// Custom color table for themed tray menu
/// </summary>
internal class ThemedColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private readonly WinFormsColor _backgroundColor;
    private readonly WinFormsColor _hoverColor;
    
    public ThemedColorTable(WinFormsColor backgroundColor, WinFormsColor hoverColor)
    {
        _backgroundColor = backgroundColor;
        _hoverColor = hoverColor;
    }
    
    public override WinFormsColor ToolStripDropDownBackground => _backgroundColor;
    public override WinFormsColor ImageMarginGradientBegin => _backgroundColor;
    public override WinFormsColor ImageMarginGradientMiddle => _backgroundColor;
    public override WinFormsColor ImageMarginGradientEnd => _backgroundColor;
    public override WinFormsColor MenuBorder => _hoverColor;
    public override WinFormsColor MenuItemBorder => WinFormsColor.Transparent;
    public override WinFormsColor MenuItemSelected => _hoverColor;
    public override WinFormsColor MenuItemSelectedGradientBegin => _hoverColor;
    public override WinFormsColor MenuItemSelectedGradientEnd => _hoverColor;
    public override WinFormsColor MenuStripGradientBegin => _backgroundColor;
    public override WinFormsColor MenuStripGradientEnd => _backgroundColor;
    public override WinFormsColor MenuItemPressedGradientBegin => _hoverColor;
    public override WinFormsColor MenuItemPressedGradientEnd => _hoverColor;
    public override WinFormsColor SeparatorDark => _hoverColor;
    public override WinFormsColor SeparatorLight => _backgroundColor;
}
