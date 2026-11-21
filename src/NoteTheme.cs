using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

static class Ui {
    public static float Scale = 1f;
    // 圆角设计令牌：窗口 / 弹层 / 菜单项 / 键盘焦点；按钮与开关始终取高度的一半
    public const int WindowRadius = 22, MenuRadius = 11, ItemRadius = 7, FocusRadius = 8;
    public static int D(int value) { return (int)Math.Round(value * Scale); }
    public static float F(float value) { return value * Scale; }
    public static Size S(int width,int height) { return new Size(D(width),D(height)); }
    public static Point P(int x,int y) { return new Point(D(x),D(y)); }
}
static class NoteTheme {
    public static bool SystemDark() {
        using (RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
            return key!=null && Object.Equals(key.GetValue("AppsUseLightTheme"),0);
        }
    }
    public static Color Paper(bool dark) { return dark?Color.FromArgb(38,37,35):Color.FromArgb(252,250,245); }
    public static Color Ink(bool dark) { return dark?Color.FromArgb(241,237,228):Color.FromArgb(48,45,40); }
    public static Color Muted(bool dark) { return dark?Color.FromArgb(175,170,160):Color.FromArgb(112,110,103); }
}
class CalendarChip : Button {
    public bool Selected, MarkToday, Pill;
    public int Chevron;
    public Color Surface, Ink, Accent;
    bool over;
    public CalendarChip() {
        FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0;
        Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);over=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);over=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        Graphics g=e.Graphics;
        g.Clear(Surface); g.SmoothingMode=SmoothingMode.AntiAlias;
        int inset=Math.Max(1,Ui.D(2));
        Rectangle r=Rectangle.Inflate(ClientRectangle,-inset,-inset);
        Color fill=Selected?Accent:Color.FromArgb(over?34:Pill?18:0,Ink);
        bool outlined=(MarkToday&&!Selected)||(Focused&&ShowFocusCues);
        if(fill.A!=0 || outlined) using(Bitmap layer=new Bitmap(Width*3,Height*3))
        using(Graphics hi=Graphics.FromImage(layer)){
            hi.Clear(Surface);
            hi.ScaleTransform(3,3);
            hi.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath shape=ThemePopover.Rounded(r,Math.Min(r.Height,r.Width)/2)){
            using(Brush brush=new SolidBrush(fill))hi.FillPath(brush,shape);
            if(outlined)
                using(Pen pen=new Pen(Selected?Surface:Accent,Ui.F(1)))hi.DrawPath(pen,shape);
            }
            g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.Half;
            using(var edges=new System.Drawing.Imaging.ImageAttributes()){ edges.SetWrapMode(WrapMode.TileFlipXY); g.DrawImage(layer,ClientRectangle,0,0,layer.Width,layer.Height,GraphicsUnit.Pixel,edges); }
        }
        RectangleF label=ClientRectangle;
        if(Chevron!=0){
            float cx=Chevron==1?Width-Ui.F(16):Width/2f, cy=Height/2f;
            float a=Ui.F(3.5f);
            PointF[] pts=Chevron==1?new[]{new PointF(cx-a,cy-a/2),new PointF(cx,cy+a/2),new PointF(cx+a,cy-a/2)}:
                new[]{new PointF(cx-Chevron*a/4,cy-a),new PointF(cx+Chevron*a/4,cy),new PointF(cx-Chevron*a/4,cy+a)};
            using(Pen pen=new Pen(Ink,Ui.F(1.35f))){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLines(pen,pts);}
            if(Chevron!=1)return;
            label=new RectangleF(Ui.D(11),0,Width-Ui.D(39),Height);
        }
        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using(StringFormat format=new StringFormat { Alignment=Chevron==1?StringAlignment.Near:StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap })
        using(Brush textBrush=new SolidBrush(Selected?Surface:Ink))
            g.DrawString(Text,Font,textBrush,label,format);
    }
}
sealed class ThemedDate : CalendarChip {
    readonly ThemePopover menu=new ThemePopover();
    readonly Panel grid=new Panel();
    readonly System.Globalization.CultureInfo zh=System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
    DateTime value=DateTime.Today, month;
    int level;
    public bool Dark;
    public Color[] Blend;
    public event EventHandler ValueChanged;
    public DateTime Value {
        get {return value;}
        set { if(this.value==value.Date)return; this.value=value.Date; UpdateLabel(); if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty); }
    }
    public ThemedDate(){
        AccessibleName="选择计划日期"; Pill=true; UpdateLabel();
        menu.InsetOutline=true; menu.Padding=new Padding(Ui.D(6)); menu.AccessibleName="计划日历";
        menu.Items.Add(new ToolStripControlHost(grid){AutoSize=false,Margin=new Padding(Ui.D(6)),Padding=Padding.Empty});
    }
    static System.Globalization.CultureInfo Culture {get{return System.Globalization.CultureInfo.GetCultureInfo(Lang.English?"en-US":"zh-CN");}}
    void UpdateLabel(){Chevron=1;Text=value.ToString(Lang.English?"ddd, MMM d":"M月d日  dddd",Culture);}
    public void RefreshLanguage(){AccessibleName=Lang.T("选择计划日期");menu.AccessibleName=Lang.T("计划日历");UpdateLabel();Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        Surface=Blend==null?NoteTheme.Paper(Dark):Blend[0];
        Ink=Blend==null?NoteTheme.Ink(Dark):Blend[1]; Accent=Dark?Color.Wheat:Color.SaddleBrown;
        base.OnPaint(e);
    }
    protected override void OnClick(EventArgs e){
        base.OnClick(e);
        if(menu.Visible){menu.Close();return;}
        month=new DateTime(value.Year,value.Month,1); level=0;
        RenderGrid(); menu.Show(this,new Point(0,Height+Ui.D(4)));
    }
    protected override bool ProcessCmdKey(ref Message m,Keys key){
        if(key==Keys.F4 || key==(Keys.Alt|Keys.Down)){PerformClick();return true;}
        return base.ProcessCmdKey(ref m,key);
    }
    void Cell(string text,int x,int y,int w,int h,Action click,bool selected=false,bool marked=false,bool muted=false,bool pill=false,string name=null){
        CalendarChip c=new CalendarChip {Text=text, AccessibleName=name??text, Font=Font, Chevron=text=="‹"?-2:text=="›"?2:0, Surface=grid.BackColor,
            Ink=muted?NoteTheme.Muted(Dark):NoteTheme.Ink(Dark),Accent=Dark?Color.FromArgb(234,185,110):Color.FromArgb(143,90,27),
            Selected=selected, MarkToday=marked,Pill=pill,TabStop=click!=null};
        c.SetBounds(Ui.D(x),Ui.D(y),Ui.D(w),Ui.D(h));
        if(click!=null)c.Click+=delegate {click();}; else {c.Enabled=false;c.Cursor=Cursors.Default;}
        grid.Controls.Add(c);
    }
    void Pick(DateTime d){menu.Close();Value=d;}
    void Move(int dir){
        try{month=level==0?month.AddMonths(dir):month.AddYears(dir*(level==1?1:12));}
        catch(ArgumentOutOfRangeException){return;}
        if(month.Year<1753 || month.Year>9998){month=new DateTime(dir<0?1753:9998,1,1);}
        RenderGrid();
    }
    void RenderGrid(){
        grid.SuspendLayout();
        while(grid.Controls.Count>0)grid.Controls[0].Dispose();
        grid.BackColor=NoteTheme.Paper(Dark);menu.ApplyTheme(Dark);
        grid.Size=Ui.S(280,290); menu.Items[0].Size=grid.Size;
        Cell("‹",10,8,36,32,delegate{Move(-1);},name:Lang.T("上一页"));
        Cell("›",234,8,36,32,delegate{Move(1);},name:Lang.T("下一页"));
        int firstYear=Math.Max(1753,Math.Min(9987,month.Year-(month.Year-1)%12));
        string heading=level==0?month.ToString(Lang.English?"MMMM yyyy":"yyyy年M月",Culture):level==1?month.Year+(Lang.English?"":"年"):firstYear+" – "+(firstYear+11);
        Cell(heading,52,8,176,32,delegate{level=(level+1)%3;RenderGrid();});
        if(level==0){
            string[] days=Lang.English?new[]{"Mo","Tu","We","Th","Fr","Sa","Su"}:new[]{"一","二","三","四","五","六","日"};
            DateTime start=month.AddDays(-(((int)month.DayOfWeek+6)%7));
            for(int j=0;j<7;j++)Cell(days[j],7+j*38,44,38,22,null,muted:true);
            for(int i=0;i<42;i++){
                DateTime d=start.AddDays(i);
                Cell(d.Day.ToString(),7+i%7*38,68+i/7*29,38,29,delegate{Pick(d);},d==value,d==DateTime.Today,d.Month!=month.Month,name:d.ToString("yyyy-MM-dd"));
            }
        } else {
            for(int i=0;i<12;i++){
                int n=i;
                int year=firstYear+i;
                Cell(level==1?new DateTime(2000,i+1,1).ToString(Lang.English?"MMM":"M月",Culture):year.ToString(),8+i%3*88,50+i/3*47,88,42,delegate{
                    month=level==1?new DateTime(month.Year,n+1,1):new DateTime(year,month.Month,1);
                    level--;RenderGrid();
                },level==1?month.Year==value.Year && n+1==value.Month:year==value.Year);
            }
        }
        Cell(Lang.T("今天")+" · "+DateTime.Today.ToString(Lang.English?"MMM d":"M月d日",Culture),62,249,156,32,delegate{Pick(DateTime.Today);},pill:true);
        grid.ResumeLayout();
    }
    protected override void Dispose(bool disposing){if(disposing)menu.Dispose();base.Dispose(disposing);}
}


