using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 菜单里的“动效”开关：滑块用 OutBack 弹一下，颜色跟着插值。
sealed class MotionSwitchItem : CheckBox {
    readonly string id = Guid.NewGuid().ToString("N");
    double position;
    public double Position { get { return position; } private set { position = value; } }
    public MotionSwitchItem(bool enabled) {
        Text = "动效";
        AccessibleName = "动效";
        AutoCheck = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = false;
        Size = Ui.S(148, 34);
        Checked = enabled;
        position = enabled ? 1 : 0;
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Ui.D(12), 0, Width - Ui.D(60), Height), ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        double t = position;
        bool dark = BackColor.GetBrightness() < 0.45f;
        // 与底部两个开关同一套暖色，避免同一个界面里出现两种强调色
        Color off = dark ? Color.FromArgb(76, 73, 67) : Color.FromArgb(228, 225, 217);
        Color on = dark ? Color.FromArgb(232, 181, 99) : Color.FromArgb(171, 111, 34);
        Color color = Ani.Mix(off, on, t);
        int trackH = Ui.D(20);
        Rectangle track = new Rectangle(Width - Ui.D(48), (Height - trackH) / 2, Ui.D(36), trackH);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (GraphicsPath shape = ThemePopover.Rounded(track, trackH / 2))
        using (Brush fill = new SolidBrush(color)) e.Graphics.FillPath(fill, shape);
        float left = track.Left + Ui.D(2) + (float)t * Ui.D(16);
        using (Brush shadow = new SolidBrush(Color.FromArgb(35, Color.Black)))
            e.Graphics.FillEllipse(shadow, left, track.Top + Ui.D(3), Ui.D(16), Ui.D(16));
        e.Graphics.FillEllipse(Brushes.White, left, track.Top + Ui.D(2), Ui.D(16), Ui.D(16));
        if (Focused && ShowFocusCues) {
            using (GraphicsPath outline = ThemePopover.Rounded(new Rectangle(2,2,Width-5,Height-5), Ui.D(Ui.FocusRadius)))
            using (Pen pen = new Pen(Color.FromArgb(160,ForeColor), Ui.F(1f))) {
                pen.DashStyle=DashStyle.Dot;
                e.Graphics.DrawPath(pen,outline);
            }
        }
    }
    public void SlideTo(bool enabled) {
        Checked = enabled;
        if (!Ani.Enabled) { position = enabled ? 1 : 0; Invalidate(); return; }
        Ani.Start("motionSlide" + id,
            Ani.Value(delegate { return position; }, delegate(double v) { position = v; Invalidate(); },
                enabled ? 1 : 0, 260, 0, new EaseOutBack(2)));
    }
    protected override void Dispose(bool disposing) {
        if (disposing) Ani.Stop("motionSlide" + id);
        base.Dispose(disposing);
    }
}

// 主题弹层。入场：淡入 120ms（延迟 60）+ 从上方 8px 落到位置（260ms OutBack）；
// 退场：淡出 80ms（延迟 20）+ 再下沉 6px（150ms）。与 PCL 弹窗同一套节奏，只是幅度收小。
sealed class ThemePopover : ContextMenuStrip {
    public bool InsetOutline;
    double fade = 1, offset;
    int baseTop;
    bool finishing, closingNow;
    ToolStripDropDownCloseReason pendingReason;

    public ThemePopover() {
        Padding = new Padding(Ui.D(5));
        ShowImageMargin = false;
        ShowCheckMargin = false;
        Renderer = new MenuPainter();
    }
    void Place() { Top = baseTop + (int)Math.Round(offset); }
    public void FinishAnimation() {
        bool wasClosing = closingNow;
        Ani.Stop("popoverIn"); Ani.Stop("popoverOut");
        fade = 1; offset = 0; Opacity = 1; Place();
        if (wasClosing) { closingNow = false; finishing = true; try { Close(pendingReason); } finally { finishing = false; } }
    }
    protected override void OnOpening(System.ComponentModel.CancelEventArgs e) {
        base.OnOpening(e);
        if (e.Cancel) return;
        closingNow = false;
        fade = Ani.Enabled ? 0 : 1;
        offset = Ani.Enabled ? -8 : 0;
        Opacity = fade;
    }
    protected override void OnOpened(EventArgs e) {
        base.OnOpened(e);
        baseTop = Top;
        Place();
        if (!Ani.Enabled) { Opacity = 1; return; }
        Ani.Start("popoverIn",
            Ani.Value(delegate { return fade; }, delegate(double v) { fade = v; Opacity = Math.Max(0, Math.Min(1, v)); }, 1, 120, 60, new EaseLinear()),
            Ani.Value(delegate { return offset; }, delegate(double v) { offset = v; Place(); }, 0, 260, 60, new EaseOutBack(2)));
    }
    protected override void OnClosing(ToolStripDropDownClosingEventArgs e) {
        base.OnClosing(e);
        if (finishing || e.Cancel || Disposing || !Ani.Enabled || Opacity <= 0) return;
        e.Cancel = true;
        pendingReason = e.CloseReason;
        if (closingNow) return;
        closingNow = true;
        baseTop = Top - (int)Math.Round(offset);
        Ani.Start("popoverOut",
            Ani.Value(delegate { return fade; }, delegate(double v) { fade = v; Opacity = Math.Max(0, Math.Min(1, v)); }, 0, 80, 20, new EaseLinear()),
            Ani.Value(delegate { return offset; }, delegate(double v) { offset = v; Place(); }, 6, 150, 0, new EaseOutFluent()),
            Ani.Code(delegate {
                closingNow = false;
                finishing = true;
                try { Close(pendingReason); } finally { finishing = false; }
            }, 190));
    }
    protected override void OnClosed(ToolStripDropDownClosedEventArgs e) {
        Ani.Stop("popoverIn"); Ani.Stop("popoverOut");
        fade = 1; offset = 0; Opacity = 1;
        base.OnClosed(e);
    }
    public void ApplyTheme(bool dark) { ApplyColors(NoteTheme.Paper(dark), NoteTheme.Ink(dark)); }
    public void ApplyColors(Color paper, Color ink) {
        BackColor = paper;
        ForeColor = ink;
        Invalidate(true);
    }
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);
        if (Width < 22 || Height < 22) return;
        using (GraphicsPath outline = Rounded(new Rectangle(0, 0, Width, Height), Ui.D(Ui.MenuRadius)))
            Region = new Region(outline);
    }
    internal static GraphicsPath Rounded(Rectangle r, int radius) {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    sealed class MenuPainter : ToolStripRenderer {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
            e.Graphics.Clear(e.ToolStrip.BackColor);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int inset=((ThemePopover)e.ToolStrip).InsetOutline?Ui.D(2):0;
            Rectangle bounds=new Rectangle(inset,inset,e.ToolStrip.Width-1-2*inset,e.ToolStrip.Height-1-2*inset);
            using (GraphicsPath outline = Rounded(bounds, Math.Max(1,Ui.D(Ui.MenuRadius)-inset)))
            using (Pen edge = new Pen(Color.FromArgb(60, e.ToolStrip.ForeColor)))
                e.Graphics.DrawPath(edge, outline);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            if (!e.Item.Selected) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = Rounded(new Rectangle(0, Ui.D(1), e.Item.Width - 1, e.Item.Height - Ui.D(2)), Ui.D(Ui.ItemRadius)))
            using (Brush brush = new SolidBrush(Color.FromArgb(28, e.ToolStrip.ForeColor)))
                e.Graphics.FillPath(brush, shape);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            ToolStripMenuItem item = (ToolStripMenuItem)e.Item;
            TextRenderer.DrawText(e.Graphics, item.Text, item.Font, new Rectangle(Ui.D(12), 0, item.Width - Ui.D(44), item.Height),
                e.ToolStrip.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            if (!item.Checked) return;
            int x = item.Width - Ui.D(25), y = item.Height / 2;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen ink = new Pen(e.ToolStrip.BackColor.GetBrightness() < 0.45f ? Color.Wheat : Color.FromArgb(151, 95, 25), Ui.F(2f))) {
                ink.StartCap = ink.EndCap = LineCap.Round;
                e.Graphics.DrawLines(ink, new Point[] { new Point(x, y), new Point(x + Ui.D(3), y + Ui.D(3)), new Point(x + Ui.D(Ui.MenuRadius), y - Ui.D(4)) });
            }
        }
    }
}

