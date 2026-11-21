using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 自绘滚动条：胶囊滑块 + 抗锯齿 + 跟随主题插值。
// 交互节奏沿用 PCL：悬停进 100ms / 出 200ms，滚轮后的滑块跟随 140ms OutFluent。
sealed class NoteScrollBar : Control {
    readonly Checklist owner;
    readonly string id = Guid.NewGuid().ToString("N");
    Color paper = Color.FromArgb(252, 250, 245), muted = Color.FromArgb(112, 110, 103);
    double presence, glow, thumbY, thumbH;
    bool dragging, hover, ready;
    int grabOffset;
    int lastPos = -1, lastTotal = -1, lastVisible = -1, lastHeight = -1;

    public NoteScrollBar(Checklist list) {
        owner = list;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        TabStop = false;
        owner.ScrollChanged += delegate { Sync(true); };
        owner.Disposed += delegate { Ani.Stop("sbPres" + id); Ani.Stop("sbGlow" + id); Ani.Stop("sbMove" + id); };
    }

    public void ApplyColors(Color paperColor, Color mutedColor) {
        paper = paperColor; muted = mutedColor;
        BackColor = paperColor;
        Invalidate();
    }

    int Max { get { return Math.Max(0, owner.ScrollTotal - owner.ScrollVisible); } }

    // 把滚动状态换算成滑块几何；animate=true 时滑块用缓动追上去
    void Sync(bool animate) {
        if (!IsHandleCreated) return;
        int pos = owner.ScrollPos, total = owner.ScrollTotal, visible = owner.ScrollVisible;
        if (ready && pos == lastPos && total == lastTotal && visible == lastVisible && Height == lastHeight) return;
        lastPos = pos; lastTotal = total; lastVisible = visible; lastHeight = Height;
        int max = Math.Max(0, total - visible);
        double want = max <= 0 ? 0 : (double)pos / max;
        double h = max <= 0 ? Height : Math.Max(Ui.D(30), Height * (double)visible / Math.Max(1, total));
        double y = (Height - h) * want;
        thumbH = h;
        bool shouldShow = max > 0;
        if (!ready) { ready = true; presence = shouldShow ? 1 : 0; thumbY = y; Invalidate(); return; }

        if (Ani.Enabled) {
            if ((presence > 0.5) != shouldShow)
                Ani.Start("sbPres" + id, Ani.Value(delegate { return presence; }, delegate(double v) { presence = v; Invalidate(); },
                    shouldShow ? 1 : 0, shouldShow ? 150 : 200, 0, new EaseOutFluent()));
        } else { presence = shouldShow ? 1 : 0; }

        if (dragging || !animate || !Ani.Enabled) { Ani.Stop("sbMove" + id); thumbY = y; }
        else if (Math.Abs(thumbY - y) > 0.5)
            Ani.Start("sbMove" + id, Ani.Value(delegate { return thumbY; }, delegate(double v) { thumbY = v; Invalidate(); },
                y, 140, 0, new EaseOutFluent(3)));
        Invalidate();
    }

    public void FinishAnimation() {
        Ani.Stop("sbPres" + id); Ani.Stop("sbGlow" + id); Ani.Stop("sbMove" + id);
        presence = Max > 0 ? 1 : 0; glow = hover ? 1 : 0;
        lastPos = -1;
        Sync(false);
    }

    void SetGlow(double to, int ms) {
        if (!Ani.Enabled) { glow = to; Invalidate(); return; }
        Ani.Start("sbGlow" + id, Ani.Value(delegate { return glow; }, delegate(double v) { glow = v; Invalidate(); }, to, ms, 0, new EaseOutFluent()));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; SetGlow(1, 100); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; if (!dragging) SetGlow(0, 200); }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || Max <= 0) return;
        int y = e.Y;
        if (y >= thumbY && y <= thumbY + thumbH) { dragging = true; grabOffset = y - (int)Math.Round(thumbY); SetGlow(1.6, 100); }
        else { ScrollTo((y - thumbH / 2) / Math.Max(1, Height - thumbH) * Max); }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (!dragging) return;
        ScrollTo((e.Y - grabOffset) / Math.Max(1, Height - thumbH) * Max);
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (!dragging) return;
        dragging = false;
        SetGlow(hover ? 1 : 0, 200);
    }
    protected override void OnMouseWheel(MouseEventArgs e) {
        base.OnMouseWheel(e);
        ScrollTo(owner.ScrollPos - Math.Sign(e.Delta) * Ui.D(48));
    }
    void ScrollTo(double pos) {
        int max = Max;
        owner.ScrollTo((int)Math.Round(Math.Max(0, Math.Min(max, pos))), !dragging);
        Sync(!dragging);
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(paper);
        if (presence <= 0.01 || Max <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        double g = Math.Min(1, glow);
        float w = Ui.F(5f) + Ui.F(2f) * (float)g;          // 静止 5px，悬停涨到 7px
        float x = (Width - w) / 2f;
        float y = (float)thumbY, h = (float)Math.Max(Ui.D(30), thumbH);
        if (h > Height) h = Height;

        // 轨道：只在悬停时浮出一条极淡的底，平时完全隐形
        int trackAlpha = (int)(22 * g * presence);
        if (trackAlpha > 1) {
            using (GraphicsPath tp = Capsule(x, 0, w, Height))
            using (Brush tb = new SolidBrush(Color.FromArgb(trackAlpha, muted))) e.Graphics.FillPath(tb, tp);
        }

        int alpha = (int)((62 + 108 * Math.Min(1.6, glow)) * presence);
        if (alpha > 255) alpha = 255;
        using (GraphicsPath path = Capsule(x, y, w, h))
        using (Brush brush = new SolidBrush(Color.FromArgb(alpha, muted))) e.Graphics.FillPath(brush, path);
    }

    static GraphicsPath Capsule(float x, float y, float w, float h) {
        GraphicsPath path = new GraphicsPath();
        float d = Math.Min(w, h);
        if (d <= 0.5f) { path.AddRectangle(new RectangleF(x, y, Math.Max(0.5f, w), Math.Max(0.5f, h))); return path; }
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Sync(false); }
    protected override void Dispose(bool disposing) {
        if (disposing) { Ani.Stop("sbPres" + id); Ani.Stop("sbGlow" + id); Ani.Stop("sbMove" + id); }
        base.Dispose(disposing);
    }
}