using System;
using System.IO;
static class Lang {
    static string PathName {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DailyPlanNote","language.txt");}}
    public static bool English {get;private set;}
    public static void Load(){ English=File.Exists(PathName) && File.ReadAllText(PathName).Trim()=="en"; }
    public static void Choose(bool en){ string tmp=PathName+".tmp"; File.WriteAllText(tmp,en?"en":"zh"); if(File.Exists(PathName)) File.Replace(tmp,PathName,null); else File.Move(tmp,PathName); English=en; }
    public static string T(string s){ if(!English)return s; switch(s){
        case "每日计划": return "Daily Plan";
        case "保存失败": return "Save failed";
        case "布局保存失败": return "Layout not saved";
        case "保存失败，请检查磁盘空间或文件夹权限后重试。": return "Could not save. Check disk space and folder permissions, then try again.";
        case "已最小化到托盘，点击图标可恢复窗口；右键可真正退出。": return "Click the tray icon to reopen. Right-click it to quit.";
        case "最小化到托盘": return "Hide to tray";
        case "最小化到托盘（真正退出请右键托盘图标）": return "Hide to tray. Right-click the tray icon to quit.";
        case "打开每日计划": return "Open Daily Plan";
        case "退出每日计划": return "Quit Daily Plan";
        case "每日计划 - 正在运行": return "Daily Plan";
        case "今天": return "Today";
        case "置顶": return "Pin";
        case "开机自启": return "At login";
        case "保存中…": return "Saving…";
        case "主题读取失败": return "Theme unavailable";
        case "主题保存失败": return "Theme not saved";
        case "动效保存失败": return "Motion not saved";
        case "已保存": return "Saved";
        case "可直接输入": return "Ready";
        case "读取失败": return "Read failed";
        case "设置": return "Settings";
        case "动效": return "Motion";
        case "跟随系统": return "System";
        case "浅色": return "Light";
        case "深色": return "Dark";
        case "标记完成": return "Mark complete";
        case "计划内容": return "Plan text";
        case "选择计划日期": return "Choose date";
        case "计划日历": return "Calendar";
        case "上一页": return "Previous";
        case "下一页": return "Next";
        default:return s;
    }}
}