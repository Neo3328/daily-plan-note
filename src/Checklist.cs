using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Checklist : Panel {
    public event EventHandler Changed;
    public event EventHandler ScrollChanged;
    readonly FlowLayoutPanel content = new FlowLayoutPanel { FlowDirection=FlowDirection.TopDown, WrapContents=false, Margin=Padding.Empty };
    public ControlCollection Rows { get { return content.Controls; } }
    int offset, destination;
    double wheelRemainder;
    System.Threading.Timer scrollFrames;
    int frameQueued;
    bool higherTimerResolution;
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
    void QueueScrollFrame(object state) {
        if(System.Threading.Interlocked.Exchange(ref frameQueued,1)!=0) return;
        try {
            BeginInvoke((MethodInvoker)delegate {
                try { if(scrollFrames!=null) AdvanceScroll(); }
                finally { System.Threading.Interlocked.Exchange(ref frameQueued,0); }
            });
        } catch(InvalidOperationException) { System.Threading.Interlocked.Exchange(ref frameQueued,0); }
    }
    void StopScroll() {
        if(scrollFrames!=null) scrollFrames.Dispose();
        scrollFrames=null;
        if(higherTimerResolution) { timeEndPeriod(1); higherTimerResolution=false; }
        frameClock.Stop();
    }
    protected override void OnHandleDestroyed(EventArgs e) {
        StopScroll();
        System.Threading.Interlocked.Exchange(ref frameQueued,0);
        base.OnHandleDestroyed(e);
    }
    readonly System.Diagnostics.Stopwatch frameClock=new System.Diagnostics.Stopwatch();
    double preciseOffset;
    void ApplyOffset() {
        if(content.Top==-offset) return;
        // Moving the viewport does not change any child sizes or anchors.
        SuspendLayout();
        try { content.Top=-offset; } finally { ResumeLayout(false); }
    }
    void AdvanceScroll() {
        double elapsed=frameClock.Elapsed.TotalMilliseconds;
        frameClock.Restart();
        preciseOffset=destination+(preciseOffset-destination)*Math.Exp(-elapsed/30.0);
        int next=(int)Math.Round(preciseOffset);
        if(next==destination || !Ani.Enabled) {
            StopScroll(); next=destination; preciseOffset=next;
        }
        if(next==offset) return;
        offset=next; ApplyOffset(); RaiseScroll();
    }
    void RaiseScroll() { if(ScrollChanged!=null) ScrollChanged(this,EventArgs.Empty); }
    public int ScrollTotal { get { return content.Height; } }
    public int ScrollVisible { get { return ClientSize.Height; } }
    public int ScrollPos { get { return offset; } }
    public void ScrollTo(int target,bool animate) {
        int max=Math.Max(0,ScrollTotal-ScrollVisible);
        target=Math.Max(0,Math.Min(max,target));
        destination=target;
        if(!animate || !Ani.Enabled || !IsHandleCreated) {
            StopScroll(); preciseOffset=offset=target;
            ApplyOffset(); RaiseScroll(); return;
        }
        if(target==offset){
            StopScroll(); preciseOffset=offset; return;
        }
        if(scrollFrames==null){
            preciseOffset=offset; higherTimerResolution=timeBeginPeriod(1)==0; frameClock.Restart(); scrollFrames=new System.Threading.Timer(QueueScrollFrame,null,0,15);
        }
    }
    // 光标跑到看不见的行时把它带进视野
    void EnsureVisible(Panel row) {
        int top=row.Top;
        if(top<offset) ScrollTo(top,true);
        else if(top+row.Height>offset+ScrollVisible) ScrollTo(top+row.Height-ScrollVisible,true);
    }
    void FitContent() {
        int height=0;
        foreach(Panel row in Rows) height+=row.Height;
        if(content.Height!=height) content.Height=height;
        int max=Math.Max(0,height-ScrollVisible);
        if(offset>max || destination>max) ScrollTo(Math.Min(offset,max),false);
        else RaiseScroll();
    }
    public void ScrollWheel(int delta) {
        if(delta==0) return;
        if(wheelRemainder*delta>0) wheelRemainder=0;
        double distance=wheelRemainder-delta*Ui.D(60)/120.0;
        int pixels=(int)distance;
        wheelRemainder=distance-pixels;
        if(pixels==0) return;
        bool continuing=scrollFrames!=null && Math.Sign(destination-offset)==Math.Sign(pixels);
        int start=continuing?destination:offset;
        ScrollTo(start+pixels,true);
    }
    void WheelScroll(MouseEventArgs e) {
        ScrollWheel(e.Delta);
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
        foreach(Panel row in Rows) {
            CheckBox box=(CheckBox)row.Controls[0];
            TextBox input=Input(row);
            input.BackColor=paper;
            input.ForeColor=box.Checked?muted:ink;
        }
    }
    bool rebuilding;
    public Checklist() {

        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        content.SetBounds(0,0,ClientSize.Width,0);
        Controls.Add(content);
        content.Layout+=delegate { if(!rebuilding) FitContent(); };
        AutoScroll = false;
        Resize += delegate { content.SuspendLayout(); content.Width=ClientSize.Width; foreach (Panel row in Rows) Fit(row); content.ResumeLayout(true); ScrollTo(offset,false); };
        MouseDown += delegate { if (Rows.Count > 0) Input(Rows[Rows.Count-1]).Focus(); };
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
        Rows.Add(row); Rows.SetChildIndex(row,index); Fit(row);
        GrowRow(row);
        box.Enabled=!String.IsNullOrWhiteSpace(text);
        box.CheckedChanged += delegate {
            input.Font=box.Checked?done:normal;
            input.ForeColor=box.Checked?NoteTheme.Muted(dark):NoteTheme.Ink(dark);
            Notify();
        };
        input.TextChanged += delegate {
            if(rebuilding) return;
            string[] lines=input.Text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
            if(lines.Length>1) {
                int next=Rows.GetChildIndex(row)+1;
                rebuilding=true;
                try { input.Text=lines[0]; foreach(string line in lines.Skip(1)) AddRow(line,false,next++); }
                finally { rebuilding=false; }
                TextBox last=Input(Rows[next-1]); last.Focus(); last.Select(last.TextLength,0);
            }
            box.Enabled=!String.IsNullOrWhiteSpace(input.Text);
            if(!box.Enabled) box.Checked=false;
            Fit(row); EnsureVisible(row); Notify();
        };
        input.KeyDown += delegate(object sender,KeyEventArgs e) {
            int pos=Rows.GetChildIndex(row);
            if(e.KeyCode==Keys.Enter) {
                e.SuppressKeyPress=true;
                int split=input.SelectionStart;
                string tail=input.Text.Substring(split+input.SelectionLength);
                input.Text=input.Text.Substring(0,split);
                Input(AddRow(tail,false,pos+1)).Focus(); Notify();
            } else if(e.KeyCode==Keys.Back && input.TextLength==0 && Rows.Count>1) {
                e.SuppressKeyPress=true;
                row.Dispose(); TextBox previous=Input(Rows[Math.Max(0,pos-1)]);
                previous.Focus(); previous.Select(previous.TextLength,0); Notify();
            }
        };
        return row;
    }
    public void ReadDay(string path) {
        StopScroll(); rebuilding=true; content.SuspendLayout();
        try {
            while(Rows.Count>0) Rows[0].Dispose();
            if(File.Exists(path+".items")) {
                foreach(string line in File.ReadAllLines(path+".items")) {
                    if(line.Length==0 || (line[0]!='0' && line[0]!='1')) throw new IOException("无效计划文件");
                    try { AddRow(Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(1))),line[0]=='1',Rows.Count); }
                    catch(FormatException ex) { throw new IOException("无效计划文件",ex); }
                }
            } else if(File.Exists(path)) {
                foreach(string line in File.ReadAllText(path).Replace("\r\n","\n").Replace('\r','\n').Split('\n')) AddRow(line,false,Rows.Count);
            }
            if(Rows.Count==0) AddRow("",false,0);
        } finally { rebuilding=false; wheelRemainder=0; offset=destination=0; content.ResumeLayout(true); FitContent(); ApplyOffset(); }
    }
    public void CountPlans(out int complete,out int total) {
        complete=0; total=0;
        foreach(Panel row in Rows) {
            if(String.IsNullOrWhiteSpace(Input(row).Text)) continue;
            total++;
            if(((CheckBox)row.Controls[0]).Checked) complete++;
        }
    }
    public string[] SerializeRows() {
        return Rows.Cast<Panel>().Select(row=>(((CheckBox)row.Controls[0]).Checked?"1":"0")+Convert.ToBase64String(Encoding.UTF8.GetBytes(Input(row).Text))).ToArray();
    }
    public void RefreshLanguage(){foreach(Panel row in Rows){row.Controls[0].AccessibleName=Lang.T("标记完成");Input(row).AccessibleName=Lang.T("计划内容");}}
    public void SelectFirstEditor() {
        if(Rows.Count==0 || !Enabled) return;
        TextBox input=Input(Rows[0]);
        input.Select(input.TextLength,0);
        input.Select();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if(disposing){ StopScroll(); normal.Dispose(); done.Dispose(); } }
}




