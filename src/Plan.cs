using System;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class Plan : Form {
    readonly string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyPlanNote");
    readonly ThemedDate date = new ThemedDate();
    readonly Checklist editor = new Checklist();
    readonly Label status = new Label();
    readonly System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
    DateTime day = DateTime.Today, today = DateTime.Today;
    void SetStatus(string key){status.Tag=key; status.Text=Lang.T(key);}
    bool loading, dirty;
    readonly NotifyIcon tray = new NotifyIcon();
    bool exiting, trayHintShown, uiReady;
    const string WakeEventName = "Local\\DailyPlanNote_74b3_Wake";
    readonly Panel progressTrack=new Panel();
    readonly Panel progressFill=new Panel();
    readonly Label progressCount=new Label();
    int targetWidth;
    void UpdateProgress(bool animate=false) {
        int complete,total;
        editor.CountPlans(out complete,out total);
        progressCount.Text=total==0?"":complete+" / "+total;
        progressCount.AccessibleName=Lang.English?complete+" of "+total+" complete":"已完成 "+complete+" 项，共 "+total+" 项";
        int width=total==0?0:(int)Math.Round(progressTrack.ClientSize.Width*(double)complete/total);
        if(animate && width==targetWidth) return;
        targetWidth=width;
        if(animate && width!=progressFill.Width && Ani.Enabled) {
            // 进度条走 OutFluent(Strong)：起步快、收尾稳，和勾选动画同一拍
            Ani.Start("progress",
                Ani.Value(delegate { return progressFill.Width; },
                          delegate(double v) { progressFill.Width=(int)Math.Round(v); },
                          width,260,0,new EaseOutFluent(4)));
        } else { Ani.Stop("progress"); progressFill.Width=width; }
    }
    const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    string NotePath { get { return Path.Combine(folder, day.ToString("yyyy-MM-dd") + ".txt"); } }
    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetProcessDPIAware();
    static void EnableDpiAwareness() {
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
        catch (DllNotFoundException) { }
          catch (EntryPointNotFoundException) { }
        try { SetProcessDPIAware(); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
    bool cornerRoundedBySystem;
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try {
            int round = 2; // DWMWCP_ROUND：由合成器做真正抗锯齿，消除 Region 外缘台阶
            cornerRoundedBySystem = DwmSetWindowAttribute(Handle, 33, ref round, 4) >= 0;
            int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE：去掉系统 1px 边框
            DwmSetWindowAttribute(Handle, 34, ref noBorder, 4);
        } catch (DllNotFoundException) { }
          catch (EntryPointNotFoundException) { }
    }
    void DragCard(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }
    static GraphicsPath CardPath(float w,float h,float d) {
        GraphicsPath path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(w-d, 0, d, d, 270, 90);
        path.AddArc(w-d, h-d, d, d, 0, 90);
        path.AddArc(0, h-d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    void RoundCard() {
        if (cornerRoundedBySystem) { Region = null; return; }
        int d = Ui.D(Ui.WindowRadius*2), w = ClientSize.Width, h = ClientSize.Height;   // 半径 22 逻辑像素
        if (w < d || h < d) return;
        using (GraphicsPath path = CardPath(w,h,d)) Region = new Region(path);
    }
    // Region 是 1bit 硬边，必然有台阶；沿边再画一道 GDI+ 抗锯齿描边，
    // 视觉上把锯齿压成一条发丝高光，圆角越大台阶越缓。
    protected override void OnPaintBackground(PaintEventArgs e) {
        base.OnPaintBackground(e);
        float w = ClientSize.Width, h = ClientSize.Height, d = Ui.D(Ui.WindowRadius*2);
        if (cornerRoundedBySystem || w < d || h < d) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float inset = Ui.F(0.6f);
        using (GraphicsPath path = CardPath(w-inset*2, h-inset*2, d-inset*2)) {
            path.Transform(new System.Drawing.Drawing2D.Matrix(1,0,0,1,inset,inset));
            using (Pen rim = new Pen(Color.FromArgb(26, ForeColor), Ui.F(1.1f))) e.Graphics.DrawPath(rim, path);
        }
    }
    string PositionPath { get { return Path.Combine(folder,"position.txt"); } }
    int restoreW, restoreH;
    Point RestoreBoundsLocation(Rectangle primary) {
        try {
            string[] parts = File.ReadAllLines(PositionPath);
            int x, y;
            if (parts.Length >= 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y)) {
                Point saved = new Point(x,y);
                Rectangle area = Screen.FromRectangle(new Rectangle(saved,Size)).WorkingArea;
                int width, height;
                if (parts.Length == 4 && int.TryParse(parts[2], out width) && int.TryParse(parts[3], out height)
                    && width > 0 && height > 0) { restoreW = width; restoreH = height; }
                return new Point(Math.Max(area.Left,Math.Min(x,area.Right-Width)),
                                 Math.Max(area.Top,Math.Min(y,area.Bottom-Height)));
            }
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return new Point(primary.Right - Width - Ui.D(24), primary.Top + Ui.D(32));
    }
    void RememberBounds() {
        if (WindowState != FormWindowState.Normal) return;
        try {
            string temp = PositionPath + ".tmp";
            File.WriteAllLines(temp,new string[] { Left.ToString(), Top.ToString(),
                ((int)Math.Round(Width/Ui.Scale)).ToString(), ((int)Math.Round(Height/Ui.Scale)).ToString() });
            if (File.Exists(PositionPath)) File.Replace(temp,PositionPath,null);
            else File.Move(temp,PositionPath);
        } catch (IOException) { SetStatus("布局保存失败"); }
          catch (UnauthorizedAccessException) { SetStatus("布局保存失败"); }
    }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if (m.Msg == 0x0232) RememberBounds(); // WM_EXITSIZEMOVE
        if (m.Msg == 0x84 && m.Result == (IntPtr)1) {
            long coords = m.LParam.ToInt64();
            Point p = PointToClient(new Point((short)coords,(short)(coords >> 16)));
            int w=ClientSize.Width, h=ClientSize.Height;
            int edge=Ui.D(6), corner=Ui.D(14);
            // 先判四角，再判四边；返回值对应 Windows 的 8 个调整方向。
            if(p.Y<corner && p.X<corner) m.Result=(IntPtr)13;                    // HTTOPLEFT
            else if(p.Y<corner && p.X>=w-corner) m.Result=(IntPtr)14;             // HTTOPRIGHT
            else if(p.Y>=h-corner && p.X<corner) m.Result=(IntPtr)16;             // HTBOTTOMLEFT
            else if(p.Y>=h-corner && p.X>=w-corner) m.Result=(IntPtr)17;          // HTBOTTOMRIGHT
            else if(p.Y<edge) m.Result=(IntPtr)12;                                // HTTOP
            else if(p.Y>=h-edge) m.Result=(IntPtr)15;                             // HTBOTTOM
            else if(p.X<edge) m.Result=(IntPtr)10;                                // HTLEFT
            else if(p.X>=w-edge) m.Result=(IntPtr)11;                             // HTRIGHT
        }
    }
    void RestoreFromTray() {
        if(!uiReady) return;
        tray.Visible=false;
        ShowInTaskbar=true;
        if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
        if(!Visible) Show();
        Activate();
    }
    void HideToTray() {
        if(!Save()) { MessageBox.Show(Lang.T("保存失败，请检查磁盘空间或文件夹权限后重试。")); return; }
        ShowInTaskbar=false;
        Hide();
        tray.Visible=true;
        if(!trayHintShown) {
            trayHintShown=true;
            tray.BalloonTipTitle=Lang.T("每日计划");
            tray.BalloonTipText=Lang.T("已最小化到托盘，点击图标可恢复窗口；右键可真正退出。");
            tray.ShowBalloonTip(1600);
        }
    }
    void ExitFromTray() {
        if(!Save()) { MessageBox.Show(Lang.T("保存失败，请检查磁盘空间或文件夹权限后重试。")); return; }
        exiting=true;
        tray.Visible=false;
        Close();
    }
    Plan() {
        FormBorderStyle = FormBorderStyle.None;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None;
        Ui.Scale = DetectScale();
        SuspendLayout();
        Directory.CreateDirectory(folder); Lang.Load(); MotionSettings.Load(folder);
        Ani.Enabled = MotionSettings.Enabled;
        Text = Lang.T("每日计划"); Font = new Font("Microsoft YaHei UI", 10); BackColor = Color.FromArgb(252,250,245); ForeColor=Color.FromArgb(48,45,40);
        StartPosition = FormStartPosition.Manual;
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        ClientSize = new Size(366,408); MinimumSize = new Size(320,280);
        Location = RestoreBoundsLocation(area);
        Label title = new Label { Text = Lang.T("每日计划"), AutoSize = true, Font = new Font(Font.FontFamily,19,FontStyle.Bold), Location = new Point(19,13) };
        RoundCard();
        SizeChanged += delegate { RoundCard(); Invalidate(true); };
        title.MouseDown += DragCard;
        MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Y < 56) DragCard(sender,e); };
        HeaderButton close = new HeaderButton { AccessibleName=Lang.T("最小化到托盘"), Size=new Size(28,28),
            Location=new Point(ClientSize.Width-44,16), Anchor=AnchorStyles.Top|AnchorStyles.Right,
            ForeColor=Color.DimGray, Font=new Font("Segoe UI",12) };
        close.Cursor=Cursors.Hand;
        close.Click += delegate { HideToTray(); };
        Controls.Add(close);
        ToolTip closeTip=new ToolTip();
        closeTip.SetToolTip(close,Lang.T("最小化到托盘（真正退出请右键托盘图标）"));
        ContextMenuStrip trayMenu=new ContextMenuStrip();
        trayMenu.Font=new Font("Microsoft YaHei UI",9);
        trayMenu.Items.Add(Lang.T("打开每日计划"),null,delegate { RestoreFromTray(); });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(Lang.T("退出每日计划"),null,delegate { ExitFromTray(); });
        tray.ContextMenuStrip=trayMenu;
        tray.Icon=Icon;
        tray.Text=Lang.T("每日计划 - 正在运行");
        tray.MouseClick += delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) RestoreFromTray(); };
        Disposed += delegate { tray.Visible=false; tray.Dispose(); trayMenu.Dispose(); closeTip.Dispose(); };
        date.Font=new Font(Font.FontFamily,10); date.SetBounds(22,63,186,28);
        PillButton back = new PillButton { Text = Lang.T("今天"), Location = new Point(ClientSize.Width-80,64), Size = new Size(58,27) };
        back.Anchor=AnchorStyles.Right|AnchorStyles.Top; back.Click += delegate { date.Value = DateTime.Today; };
        
        editor.Font = new Font(Font.FontFamily,12); editor.BackColor = BackColor;
        editor.SetBounds(22,115,ClientSize.Width-44,ClientSize.Height-165); editor.Anchor = AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        FlowLayoutPanel bar = new FlowLayoutPanel { Anchor = AnchorStyles.Left|AnchorStyles.Bottom|AnchorStyles.Right, Location=new Point(8,ClientSize.Height-42), Width=ClientSize.Width-24, Height = 36, Padding = new Padding(Ui.D(14),Ui.D(3),0,0), WrapContents=false };
        CheckBox pin = new WidgetCheck(true) { Text = Lang.T("置顶"), Size=new Size(62,24), Margin=Padding.Empty };
        string pinFile = Path.Combine(folder,"pinned");
        pin.Checked = TopMost = File.Exists(pinFile);
        pin.CheckedChanged += delegate { TopMost = pin.Checked; if(pin.Checked) File.WriteAllText(pinFile,"1"); else File.Delete(pinFile); };
        CheckBox startup = new WidgetCheck(true) { Text = Lang.T("开机自启"), Size=new Size(90,24), Margin=new Padding(Ui.D(8),0,0,0) };
        using(RegistryKey key = Registry.CurrentUser.CreateSubKey(runKey)) {
            string marker = Path.Combine(folder,"initialized");
            if(!File.Exists(marker)) { key.SetValue("DailyPlanNote", "\"" + Application.ExecutablePath + "\""); File.WriteAllText(marker,"1"); }
            startup.Checked = key.GetValue("DailyPlanNote") != null;
        }
        startup.CheckedChanged += delegate { using(RegistryKey key = Registry.CurrentUser.CreateSubKey(runKey)) { if(startup.Checked) key.SetValue("DailyPlanNote", "\"" + Application.ExecutablePath + "\""); else key.DeleteValue("DailyPlanNote",false); } };
        bar.Font=new Font(Font.FontFamily,9); status.AutoSize = true; status.Margin = new Padding(8,6,0,0);
        bar.Controls.AddRange(new Control[]{pin,startup}); status.AutoSize=false; status.TextAlign=ContentAlignment.MiddleRight; status.SetBounds(ClientSize.Width-114,ClientSize.Height-41,92,30); status.Anchor=AnchorStyles.Bottom|AnchorStyles.Right; Controls.Add(status); status.BringToFront(); Controls.AddRange(new Control[]{title,date,back,editor,bar}); status.BringToFront(); back.Font=new Font(Font.FontFamily,9,FontStyle.Bold); back.BackColor=Color.FromArgb(255,239,189); back.ForeColor=Color.FromArgb(107,74,0); bar.BackColor=BackColor; status.ForeColor=Color.DimGray; progressTrack.SetBounds(22,101,ClientSize.Width-44,3);
        progressTrack.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
        progressFill.Height=3;
        progressTrack.Controls.Add(progressFill);
        progressCount.AutoSize=true;
        progressCount.Location=new Point(title.Right+8,26);
        progressCount.Font=new Font("Segoe UI",10);
        progressCount.MouseDown+=DragCard;
        Controls.AddRange(new Control[]{progressTrack,progressCount});
        Disposed+=delegate { Ani.StopAll(); };
        progressTrack.Resize+=delegate { UpdateProgress(); };
        saveTimer.Interval = 400; saveTimer.Tick += delegate { Save(); };
        editor.Changed += delegate { if(!loading) { UpdateProgress(true); dirty=true; SetStatus("保存中…"); saveTimer.Stop(); saveTimer.Start(); } };
        date.ValueChanged += delegate { if(loading || day==date.Value.Date) return; if(Save()) LoadDay(date.Value.Date); else { loading=true; date.Value=day; loading=false; } };
        string themePath=Path.Combine(folder,"theme.txt");
        string themeMode="system";
        try {
            if(File.Exists(themePath)) {
                string saved=File.ReadAllText(themePath).Trim();
                if(saved=="light" || saved=="dark") themeMode=saved;
            }
        } catch(IOException) { SetStatus("主题读取失败"); }
          catch(UnauthorizedAccessException) { SetStatus("主题读取失败"); }
        HeaderButton themeButton=new HeaderButton { Text="", AccessibleName=Lang.T("设置"), Font=new Font("Segoe MDL2 Assets",12), Size=new Size(28,28),
            Location=new Point(close.Left-32,close.Top), Anchor=AnchorStyles.Top|AnchorStyles.Right,
            Cursor=Cursors.Hand };
        Controls.Add(themeButton);
        ThemePopover themeMenu=new ThemePopover();
        Disposed += delegate { themeMenu.Dispose(); };
        themeButton.Click += delegate { themeMenu.Show(themeButton,new Point(themeButton.Width-themeMenu.PreferredSize.Width,themeButton.Height+Ui.D(4))); };
        MotionSwitchItem motionItem=new MotionSwitchItem(MotionSettings.Enabled);
        NoteScrollBar scroll=new NoteScrollBar(editor);
        scroll.SetBounds(editor.Right-16,editor.Top,16,editor.Height);   // 逻辑单位，ScaleLayout 统一放大
        scroll.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right;
        Controls.Add(scroll); scroll.BringToFront();
        bool? appliedDark=null;
        double themeMix=0;   // 0=浅色 1=深色，换肤时逐帧插值
        Action<double> paintTheme=delegate(double t) {
            Color paper=Ani.Mix(NoteTheme.Paper(false),NoteTheme.Paper(true),t);
            Color ink=Ani.Mix(NoteTheme.Ink(false),NoteTheme.Ink(true),t);
            Color muted=Ani.Mix(NoteTheme.Muted(false),NoteTheme.Muted(true),t);
            BackColor=paper;
            ForeColor=ink;
            editor.ApplyColors(paper,ink,muted);
            scroll.ApplyColors(paper,muted);
            themeMenu.ApplyColors(paper,ink);
            motionItem.BackColor=paper; motionItem.ForeColor=ink;
            bar.BackColor=paper;
            status.ForeColor=close.ForeColor=muted;
            themeButton.BackColor=paper;
            themeButton.ForeColor=muted;

            close.BackColor=paper;

            back.BackColor=Ani.Mix(Color.FromArgb(255,239,189),Color.FromArgb(83,65,39),t);
            back.ForeColor=Ani.Mix(Color.FromArgb(107,74,0),Color.Wheat,t);
            progressCount.ForeColor=muted;
            progressFill.BackColor=Ani.Mix(Color.FromArgb(171,111,34),Color.FromArgb(232,181,99),t);
            progressTrack.BackColor=Ani.Mix(Color.FromArgb(228,225,217),Color.FromArgb(76,73,67),t);
            date.Dark=t>=0.5;
            date.Blend=new Color[]{paper,ink,muted};
            
            
            date.Invalidate();
            Invalidate(true);
        };
        Action updateTheme=delegate {
            bool dark=themeMode=="dark" || (themeMode=="system" && NoteTheme.SystemDark());
            if(appliedDark==dark) return;
            bool first=appliedDark==null;
            appliedDark=dark;
            double to=dark?1:0;
            if(first || !Ani.Enabled) {
                Ani.Stop("theme");
                themeMix=to; paintTheme(to); date.Blend=null;
                return;
            }
            Ani.Start("theme",
                Ani.Value(delegate { return themeMix; }, delegate(double v) { themeMix=v; paintTheme(v); }, to, 240, 0, new EaseOutFluent()),
                Ani.Code(delegate { themeMix=to; paintTheme(to); date.Blend=null; }, 250));
        };
        string[] modes={"system","light","dark"};
        string[] labels={Lang.T("跟随系统"),Lang.T("浅色"),Lang.T("深色")};
        for(int i=0;i<modes.Length;i++) {
            string mode=modes[i];
            ToolStripMenuItem item=new ToolStripMenuItem(labels[i]) { Checked=mode==themeMode, AutoSize=false, Size=Ui.S(148,32) };
            themeMenu.Items.Add(item);
            item.Click += delegate {
                if(themeMode==mode) return;
                try {
                    string temp=themePath+".tmp";
                    File.WriteAllText(temp,mode);
                    if(File.Exists(themePath)) File.Replace(temp,themePath,null);
                    else File.Move(temp,themePath);
                } catch(IOException) { SetStatus("主题保存失败"); return; }
                  catch(UnauthorizedAccessException) { SetStatus("主题保存失败"); return; }
                themeMode=mode;
                for(int j=0;j<modes.Length;j++) ((ToolStripMenuItem)themeMenu.Items[j]).Checked=themeMenu.Items[j]==item;
                updateTheme();
            };
        }
        themeMenu.Items.Add(new ToolStripSeparator());
        ToolStripControlHost motionHost=new ToolStripControlHost(motionItem);
        motionHost.Margin=Padding.Empty; motionHost.Padding=Padding.Empty;
        motionHost.AutoSize=false;                 // 否则宿主按“首选尺寸”把控件压扁，文字会被挤没
        motionHost.Size=Ui.S(148,34);
        motionItem.Size=Ui.S(148,34);
        themeMenu.Items.Add(motionHost);
        motionItem.Click+=delegate {
            try { MotionSettings.SetEnabled(!MotionSettings.Enabled); }
            catch(IOException) { SetStatus("动效保存失败"); return; }
            catch(UnauthorizedAccessException) { SetStatus("动效保存失败"); return; }
            Ani.Enabled=true;                       // 开关自身这一下始终有动画
            motionItem.SlideTo(MotionSettings.Enabled);
            Ani.Enabled=MotionSettings.Enabled;     // 其余控件按设置生效
            if(!MotionSettings.Enabled) {
                Ani.Stop("progress");
                UpdateProgress();
                foreach(Panel row in editor.Controls) ((WidgetCheck)row.Controls[0]).FinishAnimation();
                themeMenu.FinishAnimation();
                scroll.FinishAnimation();
            }
        };
        themeMenu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem chinese=new ToolStripMenuItem("简体中文"), english=new ToolStripMenuItem("English");
        foreach(ToolStripMenuItem language in new[]{chinese,english}){
            language.AutoSize=false; language.Size=Ui.S(148,32); themeMenu.Items.Add(language);
        }
        Action refreshLanguage=delegate {
            Text=title.Text=Lang.T("每日计划"); close.AccessibleName=Lang.T("最小化到托盘");
            themeButton.AccessibleName=Lang.T("设置"); closeTip.SetToolTip(themeButton,themeButton.AccessibleName);
            closeTip.SetToolTip(close,Lang.T("最小化到托盘（真正退出请右键托盘图标）"));
            back.Text=Lang.T("今天"); pin.Text=Lang.T("置顶"); startup.Text=Lang.T("开机自启");
            tray.Text=Lang.T("每日计划 - 正在运行"); trayMenu.Items[0].Text=Lang.T("打开每日计划");trayMenu.Items[2].Text=Lang.T("退出每日计划");
            string[] keys={"跟随系统","浅色","深色"};
            for(int i=0;i<keys.Length;i++)themeMenu.Items[i].Text=Lang.T(keys[i]);
            motionItem.Text=motionItem.AccessibleName=Lang.T("动效");
            chinese.Checked=!Lang.English; english.Checked=Lang.English;
            date.RefreshLanguage(); editor.RefreshLanguage();
            progressCount.Left=title.Right+Ui.D(8); UpdateProgress();
            if(status.Tag is string) status.Text=Lang.T((string)status.Tag);
        };
        Action<bool> chooseLanguage=delegate(bool en){
            try { Lang.Choose(en); refreshLanguage(); }
            catch(IOException){ MessageBox.Show("语言设置保存失败 / Could not save language."); }
            catch(UnauthorizedAccessException){ MessageBox.Show("语言设置保存失败 / Could not save language."); }
        };
        chinese.Click+=delegate { chooseLanguage(false); }; english.Click+=delegate { chooseLanguage(true); };
        refreshLanguage();
        themeButton.ContextMenuStrip=themeMenu;
        updateTheme();
        clock.Tick += delegate { updateTheme(); };
        clock.Interval=1000; clock.Tick += delegate { DateTime now=DateTime.Today; if(now!=today) { if(day==today) date.Value=now; today=now; } }; clock.Start();
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if(!exiting && e.CloseReason==CloseReason.UserClosing) {
                e.Cancel=true;
                HideToTray();
                return;
            }
            if(!Save()) { e.Cancel=true; MessageBox.Show(Lang.T("保存失败，请检查磁盘空间或文件夹权限后重试。")); }
        };
        ScaleLayout(title);
        LoadDay(day); uiReady=true; Shown += delegate { editor.FocusFirst(); };
    }
    static float DetectScale() {
        try {
            using(Graphics screen=Graphics.FromHwnd(IntPtr.Zero)) {
                float dpi=screen.DpiX;
                if(dpi>=72f && dpi<=480f) return dpi/96f;
            }
        } catch(Exception) { }
        return 1f;
    }
    void ScaleTree(Control parent) {
        foreach(Control child in parent.Controls) {
            int w=child.AutoSize?child.Width:Ui.D(child.Width);
            int h=child.AutoSize?child.Height:Ui.D(child.Height);
            child.SetBounds(Ui.D(child.Left),Ui.D(child.Top),w,h);
            ScaleTree(child);
        }
    }
    void ScaleLayout(Label titleLabel) {
        if(Ui.Scale>1f) {
            ScaleTree(this);
            ClientSize=new Size(Ui.D(ClientSize.Width),Ui.D(ClientSize.Height));
            MinimumSize=new Size(Ui.D(MinimumSize.Width),Ui.D(MinimumSize.Height));
        }
        ResumeLayout(false);
        if(Ui.Scale>1f) {
            PerformLayout();
            progressCount.Location=new Point(titleLabel.Right+Ui.D(8),Ui.D(26));
        }
        if(restoreW>0 && restoreH>0)
            ClientSize=new Size(Math.Max(MinimumSize.Width,Ui.D(restoreW)),
                                Math.Max(MinimumSize.Height,Ui.D(restoreH)));
    }
    void LoadDay(DateTime selected) {
        loading=true; day=selected;
        try { editor.ReadDay(NotePath); editor.Enabled=true; SetStatus(File.Exists(NotePath)||File.Exists(NotePath+".items")?"已保存":"可直接输入"); }
        catch(IOException) { editor.Enabled=false; SetStatus("读取失败"); }
        finally { loading=false; dirty=false; UpdateProgress(); }
    }
    bool Save() {
        saveTimer.Stop(); if(!dirty) return true;
        try { string path=NotePath+".items", tmp=path+".tmp"; File.WriteAllLines(tmp,editor.SerializeRows()); if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
            dirty=false; SetStatus("已保存"); return true;
        } catch(Exception ex) { if(!(ex is IOException || ex is UnauthorizedAccessException)) throw; SetStatus("保存失败"); saveTimer.Start(); return false; }
    }
    [STAThread] static void Main() {
        bool first;
        using(Mutex guard=new Mutex(true,"Local\\DailyPlanNote_74b3",out first)) {
            if(!first) {
                try {
                    using(EventWaitHandle signal=EventWaitHandle.OpenExisting(WakeEventName)) signal.Set();
                } catch(WaitHandleCannotBeOpenedException) { }
                return;
            }
            EnableDpiAwareness(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(EventWaitHandle wake=new EventWaitHandle(false,EventResetMode.AutoReset,WakeEventName)) {
                Plan plan=new Plan();
                RegisteredWaitHandle registration=ThreadPool.RegisterWaitForSingleObject(wake,
                    delegate(object state,bool timedOut) {
                        if(plan.IsDisposed || plan.Disposing) return;
                        try {
                            plan.BeginInvoke((MethodInvoker)delegate { plan.RestoreFromTray(); });
                        } catch(InvalidOperationException) { }
                    },null,-1,false);
                Application.Run(plan);
                registration.Unregister(wake);
            }
        }
    }
}



















