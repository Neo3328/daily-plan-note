using System.IO;
static class MotionSettings {
    static string disabledFile;
    public static bool Enabled { get; private set; }
    public static void Load(string folder) {
        disabledFile=Path.Combine(folder,"motion-disabled");
        Enabled=!File.Exists(disabledFile);
    }
    public static void SetEnabled(bool enabled) {
        if(enabled) File.Delete(disabledFile);
        else File.WriteAllText(disabledFile,"");
        Enabled=enabled;
    }
}
