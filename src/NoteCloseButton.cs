using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

sealed class HeaderButton : Button {
    bool hover,pressed;
    public HeaderButton() {

        Cursor=Cursors.Hand;
        FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnMouseEnter(System.EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
    protected override void OnMouseLeave(System.EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);pressed=e.Button==MouseButtons.Left;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        Graphics g=e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode=SmoothingMode.HighQuality;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        g.CompositingQuality=CompositingQuality.HighQuality;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;

        float scale=System.Math.Min(Width,Height)/28f;
        bool active=hover || (Focused && ShowFocusCues);
        if(active){
            int inset=System.Math.Max(1,(int)System.Math.Round(3*scale));
            Rectangle bounds=Rectangle.Inflate(ClientRectangle,-inset,-inset);
            using(GraphicsPath shape=ThemePopover.Rounded(bounds,bounds.Height/2))
            using(Brush fill=new SolidBrush(Color.FromArgb(pressed?52:26,ForeColor))) g.FillPath(fill,shape);
        }
        if(Text.Length>0){
            TextRenderer.DrawText(g,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
            return;
        }
        g.TranslateTransform(Width/2f,Height/2f);
        g.ScaleTransform(scale,scale);        using(Pen p=new Pen(ForeColor,1.6f)){
            p.StartCap=p.EndCap=LineCap.Round;
            g.DrawLine(p,-3.6f,-3.6f,3.6f,3.6f);
            g.DrawLine(p,-3.6f,3.6f,3.6f,-3.6f);
        }
    }
}

