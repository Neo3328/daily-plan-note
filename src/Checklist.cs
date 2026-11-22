using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Checklist : FlowLayoutPanel {
    public event EventHandler Changed;
    public event EventHandler ScrollChanged;
    // 不用原生滚动条：AutoScroll 关掉，自行堆叠行并维护 offset，
    // 右侧交给自绘的 NoteScrollBar。这样滚动位置不会被 WinForms 的滚动条逻辑重置。
    int offset;
    // 负 Padding 在 FlowLayoutPanel 里会被吃掉，直接在布局后把每行往上摞
    void ApplyOffset() { PerformLayout(); }
    void RaiseScroll() { if(ScrollChanged!=null) ScrollChanged(this,EventArgs.Empty); }
    public int ScrollTotal {
        get { int sum=0; foreach(Panel row in Controls) sum+=row.Height; return sum; }
    }
    public int ScrollVisible { get { return ClientSize.Height; } }
    public int ScrollPos { get { return offset; } }
    public void ScrollTo(int target,bool animate) {
        int max=Math.Max(0,ScrollTotal-ScrollVisible);
        target=Math.Max(0,Math.Min(max,target));
        if(!animate || !Ani.Enabled || !IsHandleCreated) {
            Ani.Stop("listScroll");
            offset=target; ApplyOffset(); RaiseScroll(); return;
        }
        if(offset==target) return;
        Ani.Start("listScroll",
            Ani.Value(delegate{ return offset; },delegate(double v){ offset=(int)Math.Round(v); ApplyOffset(); RaiseScroll(); },
                target,160,0,new EaseOutFluent(3)));
    }
    // 光标跑到看不见的行时把它带进视野
    void EnsureVisible(Panel row) {
        int top=row.Top+offset;
        if(top<offset) ScrollTo(top,true);
        else if(top+row.Height>offset+ScrollVisible) ScrollTo(top+row.Height-ScrollVisible,true);
    }
    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e);
        int max=Math.Max(0,ScrollTotal-ScrollVisible);
        if(offset>max) offset=max;          // 删行后内容变短，别让视图停在空处
        int y=-offset;
        foreach(Panel row in Controls) { row.Top=y; y+=row.Height; }
        RaiseScroll();
    }
    void WheelScroll(MouseEventArgs e) {
        ScrollTo(offset-Math.Sign(e.Delta)*Ui.D(60),true);
        HandledMouseEventArgs handled=e as HandledMouseEventArgs;
        if(handled!=null) handled.Handled=true;
    }
    protected override void OnMouseWheel(MouseEventArgs e) {
        base.OnMouseWheel(e);
        WheelScroll(e);
    }
    readonly Font normal = new Font("Microsoft YaHei UI",11.5f);
    readonly Font done = new Font("Microsoft YaHei UI",11.5f,FontStyle.Strikeout);
    bool dark;
    public void ApplyTheme(bool value) {
        dark=value;
        ApplyColors(NoteTheme.Paper(dark),NoteTheme.Ink(dark),NoteTheme.Muted(dark));
    }
    // 主题渐变时逐帧调用：只改颜色，不动布局
    public void ApplyColors(Color paper,Color ink,Color muted) {
        dark=paper.GetBrightness()<0.45f;
        BackColor=paper;
        foreach(Panel row in Controls) {
            CheckBox box=(CheckBox)row.Controls[0];
            TextBox input=Input(row);
            input.BackColor=paper;
            input.ForeColor=box.Checked?muted:ink;
        }
    }
    bool rebuilding;
    public Checklist() {
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = false;
        Resize += delegate { foreach (Panel row in Controls) Fit(row); ScrollTo(offset,false); };
        MouseDown += delegate { if (Controls.Count > 0) Input(Controls[Controls.Count-1]).Focus(); };
    }
    static TextBox Input(Control row) { return (TextBox)row.Controls[1]; }
    void Fit(Panel row) {
        // 右侧固定留 22px：滚动条占 16px，文字与滑块之间再留 8px 标准间距
        row.Width = Math.Max(Ui.D(100),Width-Ui.D(22));
        TextBox input = Input(row);
        input.Width = row.Width-Ui.D(34);
        input.Height = Math.Max(Ui.D(26),input.GetPreferredSize(new Size(input.Width,0)).Height+Ui.D(2));
        row.Height = input.Height+Ui.D(13);
    }
    void Notify() { if (!rebuilding && Changed!=null) Changed(this,EventArgs.Empty); }
    // 新增行：高度从 0 弹到实际高度（PCL 卡片入场用的是同一条 OutFluent）
    void GrowRow(Panel row) {
        if(rebuilding || !Ani.Enabled || !IsHandleCreated) return;
        int full=row.Height;
        if(full<=0) return;
        row.Height=0;
        Ani.Start("rowGrow"+row.GetHashCode(),
            Ani.Value(delegate{ return row.Height; },delegate(double v){ row.Height=(int)Math.Round(v); },full,180,0,new EaseOutFluent(4)));
    }
    Panel AddRow(string text,bool complete,int index) {
        Panel row = new Panel { Margin=Padding.Empty };
        CheckBox box = new WidgetCheck(false) { Location=new Point(0,0), Size=Ui.S(23,24), AccessibleName=Lang.T("标记完成"), Checked=complete };
        TextBox input = new TextBox { Location=Ui.P(32,1), Multiline=true, BorderStyle=BorderStyle.None,
            BackColor=BackColor, Font=complete?done:normal, Text=text, AccessibleName=Lang.T("计划内容"), ForeColor=complete?NoteTheme.Muted(dark):NoteTheme.Ink(dark) };
        row.Controls.AddRange(new Control[]{box,input});
        // WM_MOUSEWHEEL 默认发给焦点输入框；转发给列表，鼠标停在文字上也能滚动。
        input.MouseWheel += delegate(object sender,MouseEventArgs e) { WheelScroll(e); };
        box.MouseWheel += delegate(object sender,MouseEventArgs e) { WheelScroll(e); };
        input.Enter += delegate { EnsureVisible(row); };
        Controls.Add(row); Controls.SetChildIndex(row,index); Fit(row);
        GrowRow(row);
        box.Enabled=!String.IsNullOrWhiteSpace(text);
        box.CheckedChanged += delegate {
            input.Font=box.Checked?done:normal;
            Color to=box.Checked?NoteTheme.Muted(dark):NoteTheme.Ink(dark);
            if(Ani.Enabled && IsHandleCreated)
                Ani.Start("rowInk"+row.GetHashCode(),
                    Ani.Color(delegate{ return input.ForeColor; },delegate(Color c){ input.ForeColor=c; },to,200));
            else input.ForeColor=to;
            Fit(row); Notify();
        };
        input.TextChanged += delegate {
            if(rebuilding) return;
            string[] lines=input.Text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
            if(lines.Length>1) {
                int next=Controls.GetChildIndex(row)+1;
                rebuilding=true;
                try { input.Text=lines[0]; foreach(string line in lines.Skip(1)) AddRow(line,false,next++); }
                finally { rebuilding=false; }
                TextBox last=Input(Controls[next-1]); last.Focus(); last.Select(last.TextLength,0);
            }
            box.Enabled=!String.IsNullOrWhiteSpace(input.Text);
            if(!box.Enabled) box.Checked=false;
            Fit(row); EnsureVisible(row); Notify();
        };
        input.KeyDown += delegate(object sender,KeyEventArgs e) {
            int pos=Controls.GetChildIndex(row);
            if(e.KeyCode==Keys.Enter) {
                e.SuppressKeyPress=true;
                int split=input.SelectionStart;
                string tail=input.Text.Substring(split+input.SelectionLength);
                input.Text=input.Text.Substring(0,split);
                Input(AddRow(tail,false,pos+1)).Focus(); Notify();
            } else if(e.KeyCode==Keys.Back && input.TextLength==0 && Controls.Count>1) {
                e.SuppressKeyPress=true;
                row.Dispose(); TextBox previous=Input(Controls[Math.Max(0,pos-1)]);
                previous.Focus(); previous.Select(previous.TextLength,0); Notify();
            }
        };
        return row;
    }
    public void ReadDay(string path) {
        rebuilding=true; SuspendLayout();
        try {
            while(Controls.Count>0) Controls[0].Dispose();
            if(File.Exists(path+".items")) {
                foreach(string line in File.ReadAllLines(path+".items")) {
                    if(line.Length==0 || (line[0]!='0' && line[0]!='1')) throw new IOException("无效计划文件");
                    try { AddRow(Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(1))),line[0]=='1',Controls.Count); }
                    catch(FormatException ex) { throw new IOException("无效计划文件",ex); }
                }
            } else if(File.Exists(path)) {
                foreach(string line in File.ReadAllText(path).Replace("\r\n","\n").Replace('\r','\n').Split('\n')) AddRow(line,false,Controls.Count);
            }
            if(Controls.Count==0) AddRow("",false,0);
        } finally { ResumeLayout(); rebuilding=false; offset=0; ApplyOffset(); RaiseScroll(); }
    }
    public void CountPlans(out int complete,out int total) {
        complete=0; total=0;
        foreach(Panel row in Controls) {
            if(String.IsNullOrWhiteSpace(Input(row).Text)) continue;
            total++;
            if(((CheckBox)row.Controls[0]).Checked) complete++;
        }
    }
    public string[] SerializeRows() {
        return Controls.Cast<Panel>().Select(row=>(((CheckBox)row.Controls[0]).Checked?"1":"0")+Convert.ToBase64String(Encoding.UTF8.GetBytes(Input(row).Text))).ToArray();
    }
    public void RefreshLanguage(){foreach(Panel row in Controls){row.Controls[0].AccessibleName=Lang.T("标记完成");Input(row).AccessibleName=Lang.T("计划内容");}}
    public void SelectFirstEditor() {
        if(Controls.Count==0 || !Enabled) return;
        TextBox input=Input(Controls[0]);
        input.Select(input.TextLength,0);
        input.Select();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if(disposing){ Ani.Stop("listScroll"); normal.Dispose(); done.Dispose(); } }
}




