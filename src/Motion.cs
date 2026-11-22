// 动画底层：缓动函数 + 动画引擎。
// 公式与交互时序取自 PCL（Plain Craft Launcher 2，作者：龙腾猫跃），
// 这里只搬“怎么动”，配色仍用便签自己的暖色系。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

abstract class Ease {
    public abstract double At(double t);
    protected static double C(double t) { return t < 0 ? 0 : (t > 1 ? 1 : t); }
}
sealed class EaseLinear : Ease { public override double At(double t) { return C(t); } }
// 平滑结束 1-(1-t)^p：PCL 里用得最多的一条
sealed class EaseOutFluent : Ease {
    readonly double p;
    public EaseOutFluent() { p = 3; }
    public EaseOutFluent(int power) { p = power; }
    public override double At(double t) { return 1 - Math.Pow(1 - C(t), p); }
}
// 平滑开始 t^p：退场加速
sealed class EaseInFluent : Ease {
    readonly double p;
    public EaseInFluent() { p = 3; }
    public EaseInFluent(int power) { p = power; }
    public override double At(double t) { return Math.Pow(C(t), p); }
}
// 回弹结束 1-(1-t)^p·cos(1.5πt)：勾选弹出、开关滑块靠它
sealed class EaseOutBack : Ease {
    readonly double p;
    public EaseOutBack() { p = 1.5; }
    public EaseOutBack(int power) { p = 3 - power * 0.5; }
    public override double At(double t) { t = C(t); return 1 - Math.Pow(1 - t, p) * Math.Cos(1.5 * Math.PI * t); }
}
// 回弹开始：退场倾倒
sealed class EaseInBack : Ease {
    readonly double p;
    public EaseInBack() { p = 1.5; }
    public EaseInBack(int power) { p = 3 - power * 0.5; }
    public override double At(double t) { t = C(t); return Math.Pow(t, p) * Math.Cos(1.5 * Math.PI * (1 - t)); }
}

// 动画引擎。两条关键机制：
// 1) 同名组开始时会先停掉上一组 —— 鼠标快速反复进出不会排队抽搐；
// 2) 增量(delta)语义 —— 多条动画可叠加在同一属性上（按钮“先快缩再微沉”）。
static class Ani {
    public static bool Enabled = true;   // 与 MotionSettings 同步；关掉后全部直接跳终态

    public class Item {
        public Func<double> Get;
        public Action<double> Set;
        public Action<double> Add;
        public bool IsDelta, IsCode, Started;
        public double From, To, Total, Applied;
        public int Duration, Delay;
        public Ease Ease;
        public Action Done;
    }
    class Group {
        public Item[] Items;
        public long Start = Stopwatch.GetTimestamp();
    }
    static readonly Dictionary<string, Group> groups = new Dictionary<string, Group>();
    static Timer timer;
    static readonly double TicksPerMs = Stopwatch.Frequency / 1000.0;

    public static void Start(string name, params Item[] items) {
        if (items == null || items.Length == 0) { Stop(name); return; }
        if (!Enabled) {
            Stop(name);
            foreach (Item it in items) {
                if (it.IsCode) { if (it.Done != null) it.Done(); continue; }
                if (it.IsDelta) it.Add(it.Total); else it.Set(it.To);
                if (it.Done != null) it.Done();
            }
            return;
        }
        Group g = new Group { Items = items };

        groups[name] = g;
        if (timer == null) {
            timer = new Timer();
            timer.Interval = 10;
            timer.Tick += delegate { Frame(); };
        }
        if (!timer.Enabled) timer.Start();
    }
    public static void Stop(string name) {
        groups.Remove(name);
        if (groups.Count == 0 && timer != null) timer.Stop();
    }
    public static bool Running(string name) { return groups.ContainsKey(name); }
    public static void StopAll() {
        groups.Clear();
        if (timer != null) timer.Stop();
    }
    static void Frame() {
        if (groups.Count == 0) { timer.Stop(); return; }
        long now = Stopwatch.GetTimestamp();
        string[] names = new string[groups.Count];
        groups.Keys.CopyTo(names, 0);
        foreach (string name in names) {
            Group g;
            if (!groups.TryGetValue(name, out g)) continue;
            double ms = (now - g.Start) / TicksPerMs;
            bool all = true;
            foreach (Item it in g.Items) {
                double local = ms - it.Delay;
                if (local < 0) { all = false; continue; }
                if (it.IsCode) { if (it.Done != null) { it.Done(); it.Done = null; } continue; }
                double t = it.Duration <= 0 ? 1 : Math.Min(1, local / it.Duration);
                double k = it.Ease == null ? t : it.Ease.At(t);
                if (it.IsDelta) { it.Add((k - it.Applied) * it.Total); it.Applied = k; }
                else {
                    if (!it.Started) { it.Started = true; it.From = it.Get(); }
                    it.Set(it.From + (it.To - it.From) * k);
                }
                if (t < 1) all = false;
                else if (it.Done != null) { it.Done(); it.Done = null; }
            }
            if (all) groups.Remove(name);
        }
        if (groups.Count == 0) timer.Stop();
    }

    public static Item Value(Func<double> get, Action<double> set, double to, int ms) { return Value(get, set, to, ms, 0, null); }
    public static Item Value(Func<double> get, Action<double> set, double to, int ms, int delay) { return Value(get, set, to, ms, delay, null); }
    public static Item Value(Func<double> get, Action<double> set, double to, int ms, int delay, Ease ease) {
        return new Item { Get = get, Set = set, To = to, Duration = ms, Delay = delay, Ease = ease ?? new EaseOutFluent() };
    }
    public static Item Delta(Action<double> add, double total, int ms) { return Delta(add, total, ms, 0, null); }
    public static Item Delta(Action<double> add, double total, int ms, int delay) { return Delta(add, total, ms, delay, null); }
    public static Item Delta(Action<double> add, double total, int ms, int delay, Ease ease) {
        return new Item { IsDelta = true, Add = add, Total = total, Duration = ms, Delay = delay, Ease = ease ?? new EaseOutFluent() };
    }
    public static Item Color(Func<Color> get, Action<Color> set, Color to, int ms) { return Color(get, set, to, ms, 0, null); }
    public static Item Color(Func<Color> get, Action<Color> set, Color to, int ms, int delay, Ease ease) {
        Color from = get();
        return new Item {
            Get = delegate { return 0; },
            Set = delegate(double k) {
                set(System.Drawing.Color.FromArgb(
                    (int)Math.Round(from.A + (to.A - from.A) * k),
                    (int)Math.Round(from.R + (to.R - from.R) * k),
                    (int)Math.Round(from.G + (to.G - from.G) * k),
                    (int)Math.Round(from.B + (to.B - from.B) * k)));
            },
            To = 1, Duration = ms, Delay = delay, Ease = ease ?? new EaseOutFluent()
        };
    }
    public static Item Code(Action action, int delay) {
        return new Item { IsCode = true, Done = action, Delay = delay, Duration = 0 };
    }
    public static Color Mix(Color a, Color b, double t) {
        return System.Drawing.Color.FromArgb(
            (int)Math.Round(a.A + (b.A - a.A) * t),
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }
}
