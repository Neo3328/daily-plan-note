using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 勾选框 / 小开关。视觉仍是便签的暖色系，动的是 PCL 那套时序：
// 勾选时外圈先“捏”到 12 再回弹到 18（OutBack），勾从 0 缩放弹出；
// 按住时外圈 1000ms 缓缩到 16.5，松手 400ms 回弹；悬停描边 100ms 进、200ms 出。
sealed class WidgetCheck : CheckBox {
    readonly bool toggle;
    readonly string id = Guid.NewGuid().ToString("N");
    bool hover, pressed;
    double ring = 18, mark, knob;
    Color line, track;

    public WidgetCheck(bool toggle) {
        this.toggle = toggle;
        AutoSize = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        line = NeutralLine();
        track = TrackColor(false);
        knob = Checked ? 1 : 0;
        mark = Checked ? 1 : 0;
        if (toggle) ring = 18;
    }

    bool Dark { get { return BackColor.GetBrightness() < 0.45f; } }
    Color Accent() {
        bool dark = Dark;
        return dark ? (hover ? Color.Wheat : Color.FromArgb(232, 181, 99))
                    : (hover ? Color.FromArgb(148, 92, 24) : Color.FromArgb(171, 111, 34));
    }
    Color NeutralLine() { return Dark ? Color.FromArgb(120, 116, 108) : Color.FromArgb(182, 177, 164); }
    Color TrackColor(bool on) {
        if (on) return Accent();
        return Dark ? Color.FromArgb(100, 96, 89) : Color.FromArgb(183, 180, 174);
    }

    protected override void OnBackColorChanged(EventArgs e) {
        base.OnBackColorChanged(e);
        line = hover || Checked ? Accent() : NeutralLine();
        track = TrackColor(Checked);
        Invalidate();
    }

    protected override void OnCheckedChanged(EventArgs e) {
        if (!IsHandleCreated || !Ani.Enabled) { FinishAnimation(); base.OnCheckedChanged(e); return; }
        if (toggle) {
            Ani.Start("wcKnob" + id,
                Ani.Value(delegate { return knob; }, delegate(double v) { knob = v; Invalidate(); }, Checked ? 1 : 0, 220, 0, new EaseOutFluent()));
            Ani.Start("wcTrack" + id,
                Ani.Color(delegate { return track; }, delegate(Color c) { track = c; Invalidate(); }, TrackColor(Checked), 150));
        } else {
            // Retarget from the current drawing, including when the user reverses mid-frame.
            Ani.Start("wcRing" + id, Ani.Value(delegate { return ring; },
                delegate(double v) { ring=v; Invalidate(); },18,140));
            Ani.Start("wcMark" + id, Ani.Value(delegate { return mark; },
                delegate(double v) { mark=v; Invalidate(); },Checked?1:0,150));
            Ani.Start("wcLine" + id, Ani.Color(delegate { return line; },
                delegate(Color c) { line=c; Invalidate(); }, Checked || hover ? Accent() : NeutralLine(),130));        }
        Invalidate();
        base.OnCheckedChanged(e);
    }

    // 关掉动效时立刻落到终态
    public void FinishAnimation() {
        Ani.Stop("wcRing" + id); Ani.Stop("wcMark" + id);
        Ani.Stop("wcKnob" + id); Ani.Stop("wcLine" + id); Ani.Stop("wcTrack" + id);
        ring = 18;
        mark = Checked ? 1 : 0;
        knob = Checked ? 1 : 0;
        line = Checked || hover ? Accent() : NeutralLine();
        track = TrackColor(Checked);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) {
        base.OnMouseEnter(e);
        hover = true;
        Ani.Start("wcLine" + id, Ani.Color(delegate { return line; }, delegate(Color c) { line = c; Invalidate(); }, Accent(), 100));
        if (toggle) Ani.Start("wcTrack" + id, Ani.Color(delegate { return track; }, delegate(Color c) { track = c; Invalidate(); }, TrackColor(Checked), 100));
        Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        hover = false;
        Ani.Start("wcLine" + id, Ani.Color(delegate { return line; }, delegate(Color c) { line = c; Invalidate(); }, Checked ? Accent() : NeutralLine(), 200));
        if (toggle) Ani.Start("wcTrack" + id, Ani.Color(delegate { return track; }, delegate(Color c) { track = c; Invalidate(); }, TrackColor(Checked), 200));
        if (pressed) { pressed = false; Release(); }
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        pressed = true;
        if (!toggle) {
            Ani.Start("wcRing" + id, Ani.Delta(delegate(double d) { ring += d; Invalidate(); }, 16.5 - ring, 1000, 0, new EaseOutFluent(4)));

        }
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (!pressed) return;
        pressed = false;
        Release();
    }
    void Release() {
        if (toggle) return;
        Ani.Start("wcRing" + id, Ani.Delta(delegate(double d) { ring += d; Invalidate(); }, 18 - ring, 140, 0, new EaseOutFluent(4)));

    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool dark = Dark;
        if (toggle) {
            int trackH = Ui.D(14), trackW = Ui.D(26), knobSize = Ui.D(10), top = (Height - trackH) / 2;
            using (Brush fill = new SolidBrush(track))
            using (GraphicsPath shape = ThemePopover.Rounded(new Rectangle(Ui.D(1), top, trackW, trackH), trackH / 2)) g.FillPath(fill, shape);
            float kx = Ui.D(3) + (float)knob * Ui.D(11);
            g.FillEllipse(Brushes.White, kx, top + (trackH - knobSize) / 2f, knobSize, knobSize);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.D(34), 0, Width - Ui.D(34), Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        } else {
            float size = Ui.F((float)ring);
            float cx = Ui.F(11), cy = Height / 2f;
            RectangleF circle = new RectangleF(cx - size / 2f, cy - size / 2f, size, size);
            using (Pen ringPen = new Pen(Enabled ? line : NeutralLine(), Ui.F(1.5f))) g.DrawEllipse(ringPen, circle);
            if (mark > 0.001) {
                float diameter = size * (float)mark;
                using (Brush fill = new SolidBrush(Accent()))
                    g.FillEllipse(fill, cx - diameter / 2f, cy - diameter / 2f, diameter, diameter);
                float stroke = Math.Max(0, ((float)mark - 0.35f) / 0.65f);
                if (stroke > 0) using (Pen ink = new Pen(dark ? NoteTheme.Paper(true) : Color.White, Ui.F(2f))) {
                    ink.StartCap = ink.EndCap = LineCap.Round;
                    float first = Math.Min(1, stroke * 3), second = Math.Max(0, (stroke - 1f / 3) * 1.5f);
                    float bx = cx - Ui.F(4), by = cy, arm = Ui.F(3), leg = Ui.F(6);
                    g.DrawLine(ink, bx, by, bx + arm * first, by + arm * first);
                    if (second > 0) g.DrawLine(ink, bx + arm, by + arm, bx + arm + leg * second, by + arm - leg * second);
                }
            }
        }
        if (Focused && ShowFocusCues) {
            using (GraphicsPath outline = ThemePopover.Rounded(new Rectangle(2,2,Width-5,Height-5),Ui.D(Ui.FocusRadius)))
            using (Pen pen = new Pen(Color.FromArgb(160,ForeColor),Ui.F(1f))) {
                pen.DashStyle=DashStyle.Dot;
                g.DrawPath(pen,outline);
            }
        }
    }
}

// 抗锯齿圆角胶囊按钮。按下 80ms 缩到 0.955（ExtraStrong）再 700ms 微沉 0.01，
// 松手 300ms 回弹，按住移出 800ms 慢慢回弹 —— 与 PCL 按钮同一套手感。
sealed class PillButton : Button {
    readonly string id = Guid.NewGuid().ToString("N");
    bool hover, press;
    double scale = 1;
    Color fill;
    bool ghost;
    public bool Ghost {
        get { return ghost; }
        set { ghost=value; fill=ghost && Parent!=null ? Parent.BackColor : BackColor; Invalidate(); }
    }
    public Color? HoverTint { get; set; }
    public PillButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        fill = BackColor;
    }
    static Color Blend(Color from, Color to, float amount) {
        return Color.FromArgb(from.A,
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }
    Color HoverFill() {
        if (HoverTint.HasValue) return HoverTint.Value;
        bool dark = BackColor.GetBrightness() < 0.45f;
        return Blend(BackColor, Color.White, dark ? 0.12f : 0.22f);
    }
    Color IdleFill() { return ghost && Parent!=null ? Parent.BackColor : BackColor; }
    protected override void OnBackColorChanged(EventArgs e) {
        base.OnBackColorChanged(e);
        Ani.Stop("pillFill" + id);
        fill = hover ? HoverFill() : IdleFill();
        Invalidate();
    }
    protected override void OnMouseEnter(EventArgs e) {
        base.OnMouseEnter(e);
        hover = true;
        Ani.Start("pillFill" + id, Ani.Color(delegate { return fill; }, delegate(Color c) { fill = c; Invalidate(); }, HoverFill(), 100));
    }
    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        hover = false;
        Ani.Start("pillFill" + id, Ani.Color(delegate { return fill; }, delegate(Color c) { fill = c; Invalidate(); }, IdleFill(), 200));
        if (press) {
            press = false;
            Ani.Start("pillScale" + id, Ani.Delta(delegate(double d) { scale += d; Invalidate(); }, 1 - scale, 800, 0, new EaseOutFluent(4)));
        }
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        press = true;
        Ani.Start("pillScale" + id,
            Ani.Delta(delegate(double d) { scale += d; Invalidate(); }, 0.955 - scale, 80, 0, new EaseOutFluent(5)),
            Ani.Delta(delegate(double d) { scale += d; Invalidate(); }, -0.01, 700, 0, new EaseOutFluent()));
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (!press) return;
        press = false;
        Ani.Start("pillScale" + id, Ani.Delta(delegate(double d) { scale += d; Invalidate(); }, 1 - scale, 300, 10, new EaseOutFluent()));
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float d = Math.Min(Height - 1, Width - 1);
        if (d < 2) { using (SolidBrush b = new SolidBrush(fill)) g.FillRectangle(b, ClientRectangle); return; }

        GraphicsState state = g.Save();
        g.TranslateTransform(Width / 2f, Height / 2f);
        g.ScaleTransform((float)scale, (float)scale);
        g.TranslateTransform(-Width / 2f, -Height / 2f);
        using (GraphicsPath pill = new GraphicsPath()) {
            pill.AddArc(0, 0, d, d, 90, 180);
            pill.AddArc(Width - 1 - d, 0, d, d, 270, 180);
            pill.CloseFigure();
            using (Brush brush = new SolidBrush(fill)) g.FillPath(brush, pill);
        }
        if (Math.Abs(scale - 1) < 0.002) {
            // 静止时用 GDI 画字，最锐利
            g.Restore(state);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        } else {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (StringFormat sf = new StringFormat()) {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                using (Brush ink = new SolidBrush(ForeColor))
                    g.DrawString(Text, Font, ink, new RectangleF(0, 0, Width, Height), sf);
            }
            g.Restore(state);
        }
        if (Focused && ShowFocusCues) {
            using (GraphicsPath outline = ThemePopover.Rounded(new Rectangle(2,2,Width-5,Height-5),Ui.D(Ui.FocusRadius)))
            using (Pen pen = new Pen(Color.FromArgb(160,ForeColor),Ui.F(1f))) {
                pen.DashStyle=DashStyle.Dot;
                g.DrawPath(pen,outline);
            }
        }
    }
}
