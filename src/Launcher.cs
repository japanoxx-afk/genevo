using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Collections.Generic;

[assembly: System.Reflection.AssemblyVersion("0.4.1.0")]
namespace Genevo {
static class Program {
    public static string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenevoObserver");
    public static JavaScriptSerializer Json = new JavaScriptSerializer();
    [STAThread] static int Main(string[] args) {
        Directory.CreateDirectory(Data);
        Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--self-test")) return Tests.Run();
        if (args.Contains("--diagnose-live")) {var s=Settings.Load();var i=ReplayInfo.Read(s.Replay);i.Map=ModMetadata.Map(s.ModPath,i.Map);using(var live=new LiveGame()){var feed=live.Poll(Native.Game(),i,s.Replay);File.WriteAllText(Path.Combine(Data,"live-diagnostic.json"),Json.Serialize(new {Status=live.Status,Map=i.Map,Feed=feed}));}return 0;}
        if (!args.Contains("--checked") && !args.Contains("--preview")) {
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "bootstrap.ps1");
            if (!File.Exists(script)) { MessageBox.Show("scripts/bootstrap.ps1 파일이 없습니다."); return 1; }
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -WaitForPid " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true });
            return 0;
        }
        Application.Run(new Launcher(args.Contains("--preview"))); return 0;
    }
    public static void Log(Exception ex) { File.AppendAllText(Path.Combine(Data,"launcher.log"), DateTime.UtcNow.ToString("o") + " " + ex + Environment.NewLine); }
}
public class Settings {
    public string GamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Command and Conquer Red Alert 3";
    public string ModPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Downloads\GenEvo_Beta_033.1\GenEvo_Beta_033\GenEvo");
    public string Replay = ""; public string ReplayFolder="";
    public bool LockMouse=true;
    public static Settings Load() { try { return Program.Json.Deserialize<Settings>(File.ReadAllText(Path.Combine(Program.Data,"settings.json"))) ?? new Settings(); } catch { return new Settings(); } }
    public void Save() { File.WriteAllText(Path.Combine(Program.Data,"settings.json"),Program.Json.Serialize(this)); }
}
public class Player { public string Name; public string Faction; public int Slot; public Color Color; }
public class ReplayInfo {
    public string Map = "리플레이를 선택하세요";public List<Player> Players = new List<Player>();
    public static ReplayInfo Read(string path) {
        byte[] bytes = new byte[65536]; int count;
        using (var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) count = stream.Read(bytes,0,bytes.Length);
        string text = Encoding.UTF8.GetString(bytes,0,count);
        if (!text.StartsWith("RA3 REPLAY HEADER")) throw new InvalidDataException("RA3 리플레이 형식이 아닙니다.");
        var map = Regex.Match(text,@"M=([^;]+);MC=");
        var players = Regex.Match(text,@";S=([^;]+);");
        if (!map.Success || !players.Success) throw new InvalidDataException("리플레이 헤더를 읽을 수 없습니다.");
        ReplayInfo info = new ReplayInfo(); info.Map = map.Groups[1].Value;
        int index = 0; Color[] colors = {Color.FromArgb(176,104,250),Color.FromArgb(235,192,66),Color.FromArgb(71,196,220),Color.FromArgb(235,96,86),Color.LightGreen,Color.Orange};
        foreach (string entry in players.Groups[1].Value.Split(':')) {
            string[] p = entry.Split(','); int faction;
            if (p.Length < 6 || !(p[0].StartsWith("H") || p[0].StartsWith("C")) || !int.TryParse(p[5],out faction)) continue;
            if (faction == 1 || faction == 3 || p[0].IndexOf("Commentator",StringComparison.OrdinalIgnoreCase)>=0) continue;
            // GenEvo faction IDs differ from vanilla RA3. Keep the raw ID until verified.
            info.Players.Add(new Player {Name=p[0].Substring(1), Faction="진영 #"+faction, Slot=index,Color=colors[index%colors.Length]}); index++;
        }
        return info;
    }
}
public class TelemetryPlayer { public int Slot; public string Name; public string Faction; public long? Credits; public long? Income; public int? PowerUsed; public int? PowerTotal; }
public class UnitSelection {public int Count;public string Name,Type,Owner,Faction,Description;public float Health,MaxHealth;}
public class Telemetry { public string Replay; public string CapturedUtc; public string GameTime; public List<TelemetryPlayer> Players;public UnitSelection Selection; }
static class Feed {
    public static Telemetry Read(string replay) {
        string path=Path.Combine(Program.Data,"telemetry.json");
        try {
            if (!File.Exists(path) || new FileInfo(path).Length>65536) return null;
            var feed=Program.Json.Deserialize<Telemetry>(File.ReadAllText(path)); DateTime stamp;
            if (feed==null || !DateTime.TryParse(feed.CapturedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out stamp) || Math.Abs((DateTime.UtcNow-stamp.ToUniversalTime()).TotalSeconds)>3 || !String.Equals(feed.Replay,Path.GetFileName(replay),StringComparison.OrdinalIgnoreCase)) return null;
            return feed;
        } catch { return null; }
    }
}
static class Native {
    static IntPtr cachedGame;static DateTime nextWindowScan;
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    delegate bool EnumProc(IntPtr h,IntPtr arg);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr arg);
    public static bool IsGameProcess(string name) {return Regex.IsMatch(name,@"^ra3(?:_\d+\.\d+\.game)?$",RegexOptions.IgnoreCase);}
    public static bool SameProcess(IntPtr a,IntPtr b) {uint x,y;return a!=IntPtr.Zero&&b!=IntPtr.Zero&&GetWindowThreadProcessId(a,out x)!=0&&GetWindowThreadProcessId(b,out y)!=0&&x==y;}
    public static IntPtr Game() {
        if(DateTime.UtcNow<nextWindowScan&&(cachedGame==IntPtr.Zero||IsWindow(cachedGame)))return cachedGame;nextWindowScan=DateTime.UtcNow.AddMilliseconds(400);
        var ids=new HashSet<uint>();foreach(var p in Process.GetProcesses())using(p){try{if(IsGameProcess(p.ProcessName))ids.Add((uint)p.Id);}catch{}}
        cachedGame=FindGameWindow(ids);return cachedGame;
    }
    public static IntPtr FindGameWindow(HashSet<uint> ids) {
        IntPtr best=IntPtr.Zero;long score=long.MinValue;IntPtr front=GetForegroundWindow();
        EnumWindows(delegate(IntPtr h,IntPtr ignored){uint pid;GetWindowThreadProcessId(h,out pid);if(!ids.Contains(pid)||!IsWindowVisible(h))return true;
            Rectangle r=Bounds(h);long s=(long)Math.Max(0,r.Width)*Math.Max(0,r.Height);if(!IsIconic(h)&&r.Width>=640&&r.Height>=360)s+=1000000000L;if(h==front)s+=100000000L;
            if(s>score){best=h;score=s;}return true;},IntPtr.Zero);return best;
    }
    public static Rectangle Bounds(IntPtr h) { Rect r; Point pt=Point.Empty; if (!GetClientRect(h,out r) || !ClientToScreen(h,ref pt)) return Rectangle.Empty; return new Rectangle(pt.X,pt.Y,r.R-r.L,r.B-r.T); }
}
class Launcher : Form {
    Settings settings=Settings.Load();TextBox game,mod,replay;Label status,libraryTitle;ListView library;Overlay overlay;
    Timer watch=new Timer(),motion=new Timer(),reload=new Timer();FileSystemWatcher files;int generation;
    bool hudEnabled=true;string updateStatus;CursorFence fence=new CursorFence();EdgeScroll edge=new EdgeScroll();CheckBox lockMouse;
    public Launcher(bool preview) {
        Text="GENEVO / OBSERVER";ClientSize=new Size(1020,860);MinimumSize=new Size(1036,899);BackColor=Color.FromArgb(13,22,30);ForeColor=Color.White;Font=new Font("Malgun Gothic",10);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(new Label {Text="GENEVO",Font=new Font("Segoe UI",30,FontStyle.Bold),AutoSize=true,Location=new Point(30,20)});
        Controls.Add(new Label {Text="REPLAY OBSERVER / v0.4.1",ForeColor=Color.Turquoise,AutoSize=true,Location=new Point(34,82)});
        game=PathRow("RED ALERT 3",settings.GamePath,125);mod=PathRow("GENEVO MOD",settings.ModPath,202);
        libraryTitle=new Label {Text="리플레이 · 더블클릭하면 창 모드로 실행",Location=new Point(30,280),Size=new Size(560,25),ForeColor=Color.Turquoise};Controls.Add(libraryTitle);
        var refresh=Button("새로고침",740,270,110);refresh.Click+=(o,e)=>RefreshReplays();var folder=Button("폴더 변경",865,270,125);folder.Click+=(o,e)=>{using(var dialog=new FolderBrowserDialog {SelectedPath=settings.ReplayFolder})if(dialog.ShowDialog()==DialogResult.OK){settings.ReplayFolder=dialog.SelectedPath;settings.Save();WatchFiles();RefreshReplays();}};
        library=new ListView {Location=new Point(30,313),Size=new Size(960,245),View=View.Details,ShowItemToolTips=true,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=Color.FromArgb(20,34,44),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
        library.Columns.Add("파일",250);library.Columns.Add("플레이어",280);library.Columns.Add("맵",230);library.Columns.Add("수정 시간",170);Controls.Add(library);
        library.SelectedIndexChanged+=(o,e)=>{if(library.SelectedItems.Count==1){replay.Text=(string)library.SelectedItems[0].Tag;Save();}};
        library.ItemActivate+=(o,e)=>Safe(LaunchSelectedReplay);
        Controls.Add(new Label {Text="선택한 리플레이",Location=new Point(30,568),Size=new Size(950,23),ForeColor=Color.Silver});
        replay=new TextBox {Text=settings.Replay,ReadOnly=true,Location=new Point(30,593),Size=new Size(960,30),BackColor=Color.FromArgb(25,39,50),ForeColor=Color.White};Controls.Add(replay);
        Button("선택 리플레이 실행",30,642,235).Click+=(o,e)=>Safe(LaunchSelectedReplay);
        Button("GenEvo 공식 런처",280,642,220).Click+=(o,e)=>Safe(()=>{Save();string exe=Path.Combine(settings.ModPath,"GenEvoLauncher.exe");if(!File.Exists(exe))throw new FileNotFoundException("GenEvoLauncher.exe 경로를 확인하세요.");Process.Start(new ProcessStartInfo(exe){WorkingDirectory=settings.ModPath,UseShellExecute=true});});
        Button("HUD 켜기",515,642,150).Click+=(o,e)=>Safe(()=>ShowHud(false));Button("미리보기",680,642,150).Click+=(o,e)=>Safe(()=>ShowHud(true));Button("HUD 끄기",845,642,145).Click+=(o,e)=>{hudEnabled=false;if(overlay!=null)overlay.Close();};
        lockMouse=new CheckBox {Text="마우스 고정 및 가장자리 화면 이동 (F6 / Alt+Tab 해제)",Checked=settings.LockMouse,AutoSize=true,Location=new Point(30,699)};lockMouse.CheckedChanged+=(o,e)=>{settings.LockMouse=lockMouse.Checked;settings.Save();if(!lockMouse.Checked){edge.Dispose();fence.Dispose();}};Controls.Add(lockMouse);
        Controls.Add(new Label {Text="더블클릭 / Enter: 창 모드 실행 · END: 기본 게임 HUD 숨김 · F8: 관전 HUD\n하단: 진영 아이콘·게임 시간·자금·전력 · 유닛 선택 정보: 오른쪽 아래",Location=new Point(30,739),Size=new Size(960,68),ForeColor=Color.Silver});
        status=new Label {Location=new Point(30,813),Size=new Size(960,40),ForeColor=Color.Turquoise};Controls.Add(status);string state=Path.Combine(Program.Data,"update-status.txt");updateStatus=File.Exists(state)?File.ReadAllText(state).Trim():"업데이트 상태 없음";status.Text=updateStatus;
        if(String.IsNullOrWhiteSpace(settings.ReplayFolder)||!Directory.Exists(settings.ReplayFolder)){string selected=String.IsNullOrWhiteSpace(settings.Replay)?null:Path.GetDirectoryName(settings.Replay);settings.ReplayFolder=selected!=null&&Directory.Exists(selected)?selected:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"Red Alert 3\Replays");Directory.CreateDirectory(settings.ReplayFolder);}
        reload.Interval=700;reload.Tick+=(o,e)=>{reload.Stop();RefreshReplays();};watch.Interval=250;watch.Tick+=(o,e)=>Watch();motion.Interval=20;motion.Tick+=(o,e)=>MoveCamera();if(!preview){watch.Start();motion.Start();WatchFiles();}
        AppDomain.CurrentDomain.ProcessExit+=(o,e)=>{edge.Dispose();fence.Dispose();};FormClosed+=(o,e)=>{generation++;watch.Dispose();motion.Dispose();reload.Dispose();if(files!=null)files.Dispose();edge.Dispose();fence.Dispose();if(overlay!=null)overlay.Close();};Shown+=(o,e)=>{RefreshReplays();if(preview)Safe(()=>ShowHud(true));};
    }
    TextBox PathRow(string label,string value,int y){Controls.Add(new Label {Text=label,Location=new Point(30,y),Size=new Size(950,22),ForeColor=Color.FromArgb(115,149,165)});var text=new TextBox {Text=value,Location=new Point(30,y+27),Size=new Size(840,30),BackColor=Color.FromArgb(25,39,50),ForeColor=Color.White};Controls.Add(text);Button("찾기",885,y+25,105).Click+=(o,e)=>{using(var dialog=new FolderBrowserDialog {SelectedPath=text.Text})if(dialog.ShowDialog()==DialogResult.OK)text.Text=dialog.SelectedPath;};return text;}
    Button Button(string text,int x,int y,int width){var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(width,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(26,50,62),ForeColor=Color.White};b.FlatAppearance.BorderColor=Color.FromArgb(51,92,106);Controls.Add(b);return b;}
    void Safe(Action action){try{action();}catch(Exception ex){Program.Log(ex);MessageBox.Show(this,ex.Message,"Genevo");}}
    void Save(){settings.GamePath=game.Text;settings.ModPath=mod.Text;settings.Replay=replay.Text;settings.Save();}
    void WatchFiles(){if(files!=null)files.Dispose();if(!Directory.Exists(settings.ReplayFolder))return;files=new FileSystemWatcher(settings.ReplayFolder,"*.RA3Replay"){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};FileSystemEventHandler changed=(o,e)=>QueueRefresh();files.Changed+=changed;files.Created+=changed;files.Deleted+=changed;files.Renamed+=(o,e)=>QueueRefresh();files.EnableRaisingEvents=true;}
    void QueueRefresh(){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(IsDisposed)return;reload.Stop();reload.Start();}));}catch(InvalidOperationException){}}
    void RefreshReplays(){if(IsDisposed||library==null)return;int version=++generation;string folder=settings.ReplayFolder,modPath=mod.Text;libraryTitle.Text="리플레이를 읽는 중…";
        System.Threading.Tasks.Task.Run(()=>{var entries=ReplayLibrary.Read(folder);foreach(var item in entries)if(item.Valid)item.Map=ModMetadata.Map(modPath,item.Map);return entries;}).ContinueWith(task=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(IsDisposed||version!=generation)return;if(task.IsFaulted){libraryTitle.Text="리플레이 폴더를 읽지 못했습니다.";return;}string selected=replay.Text;library.BeginUpdate();library.Items.Clear();ListViewItem match=null;foreach(var entry in task.Result){var row=new ListViewItem(new[]{entry.Name,entry.Players,entry.Map,entry.Modified.ToString("yyyy-MM-dd HH:mm")}){Tag=entry.Path,ToolTipText=entry.Name+"\n"+entry.Players+"\n"+entry.Map+"\n"+entry.Modified.ToString("yyyy-MM-dd HH:mm")+"\n"+entry.Path};if(!entry.Valid)row.ForeColor=Color.Gray;library.Items.Add(row);if(String.Equals(selected,entry.Path,StringComparison.OrdinalIgnoreCase))match=row;}if(match!=null){match.Selected=true;match.EnsureVisible();}else if(library.Items.Count>0)library.Items[0].Selected=true;else replay.Text="";library.EndUpdate();libraryTitle.Text="리플레이 "+library.Items.Count+"개 · 더블클릭하면 창 모드로 실행";}));}catch(InvalidOperationException){}});
    }
    void LaunchSelectedReplay(){if(library.SelectedItems.Count==1)replay.Text=(string)library.SelectedItems[0].Tag;LaunchReplay();}
    internal static string LaunchArguments(string sku,string path,int width,int height){return "-win -xres "+width+" -yres "+height+" -modConfig \""+sku+"\" -replayGame \""+Path.GetFullPath(path)+"\"";}
    void LaunchReplay(){Save();if(Native.Game()!=IntPtr.Zero)throw new Exception("현재 게임을 종료한 뒤 리플레이를 실행하세요. 실행 중인 게임은 자동 종료하지 않습니다.");string exe=Path.Combine(settings.GamePath,"RA3.exe");if(!File.Exists(exe))throw new FileNotFoundException("RA3.exe 경로를 확인하세요.");if(!File.Exists(settings.Replay))throw new FileNotFoundException("목록에서 리플레이를 선택하세요.");ReplayInfo.Read(settings.Replay);var definitions=Directory.GetFiles(settings.ModPath,"*.skudef");if(definitions.Length!=1)throw new Exception("GenEvo 폴더에 .skudef 파일이 하나 있어야 합니다.");Rectangle area=Screen.FromControl(this).WorkingArea;int width=Math.Max(1024,Math.Min(1600,area.Width-40)),height=Math.Max(720,Math.Min(900,area.Height-100));hudEnabled=true;Process.Start(new ProcessStartInfo(exe,LaunchArguments(definitions[0],settings.Replay,width,height)){WorkingDirectory=settings.GamePath,UseShellExecute=true});status.Text="창 모드 리플레이를 실행하는 중…";}
    void ShowHud(bool preview){Save();var info=String.IsNullOrWhiteSpace(settings.Replay)?new ReplayInfo():ReplayInfo.Read(settings.Replay);if(!preview&&info.Players.Count==0)throw new Exception("리플레이를 먼저 선택하세요.");info.Map=ModMetadata.Map(settings.ModPath,info.Map);if(overlay!=null)overlay.Close();hudEnabled=!preview;overlay=new Overlay(settings,info,preview);if(preview)overlay.Show();}
    void Watch(){IntPtr h=Native.Game();Rectangle r=Native.Bounds(h);status.Text=updateStatus+"\n"+(h==IntPtr.Zero?"게임 창을 기다리는 중":Native.IsIconic(h)?"게임 최소화됨":r.Width<640?"게임 창 크기를 확인하세요":"게임 감지 "+r.Width+"×"+r.Height+" · "+edge.Status);if(hudEnabled&&(overlay==null||overlay.IsDisposed)&&Native.SameProcess(h,Native.GetForegroundWindow())&&!Native.IsIconic(h)&&File.Exists(replay.Text)){try{ShowHud(false);}catch(Exception ex){hudEnabled=false;Program.Log(ex);status.Text=ex.Message;}}}
    void MoveCamera(){IntPtr h=Native.Game();Rectangle r=Native.Bounds(h);bool active=settings.LockMouse&&h!=IntPtr.Zero&&!Native.IsIconic(h)&&Native.SameProcess(h,Native.GetForegroundWindow())&&(Native.GetAsyncKeyState(0x12)&0x8000)==0;fence.Update(r,active);edge.Update(h,r,active);}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Native.RegisterHotKey(Handle,700,0x4000,0x75);Native.RegisterHotKey(Handle,702,0x4000,0x77);}
    protected override void OnHandleDestroyed(EventArgs e){edge.Dispose();fence.Dispose();Native.UnregisterHotKey(Handle,700);Native.UnregisterHotKey(Handle,702);base.OnHandleDestroyed(e);}
    protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==700&&lockMouse!=null)lockMouse.Checked=!lockMouse.Checked;if(m.WParam.ToInt32()==702){hudEnabled=!hudEnabled;if(!hudEnabled&&overlay!=null)overlay.Close();if(hudEnabled)Safe(()=>ShowHud(false));}}base.WndProc(ref m);}
}
class Overlay : Form {
    Settings settings;ReplayInfo info;bool preview;Timer timer=new Timer();IntPtr game;Rectangle gameBounds;
    LiveGame live=new LiveGame();Telemetry liveFeed;
    Func<IntPtr> gameWindow,foregroundWindow;
    public Overlay(Settings s,ReplayInfo i,bool p,Func<IntPtr> windowProvider=null,Func<IntPtr> foregroundProvider=null) {settings=s;info=i;preview=p;gameWindow=windowProvider??Native.Game;foregroundWindow=foregroundProvider??Native.GetForegroundWindow;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=p;TopMost=true;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;StartPosition=FormStartPosition.Manual;
        if(p){Bounds=new Rectangle(100,100,1100,660);Text="HUD 미리보기 · ESC 닫기";KeyPreview=true;KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Escape)Close();};}
        timer.Interval=100;timer.Tick+=(o,e)=>RefreshState();timer.Start();FormClosed+=(o,e)=>timer.Dispose();}
    protected override bool ShowWithoutActivation {get{return !preview;}}
    protected override void WndProc(ref Message m) {if(!preview&&m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    protected override void OnActivated(EventArgs e) {base.OnActivated(e);if(!preview&&game!=IntPtr.Zero)Native.SetForegroundWindow(game);}
    protected override void Dispose(bool disposing) {if(disposing){timer.Dispose();live.Dispose();}base.Dispose(disposing);}
    protected override CreateParams CreateParams {get {var p=base.CreateParams;if(!preview)p.ExStyle|=0x08000000|0x20|0x80;return p;}}
    public void RefreshState() {
        if(preview){Invalidate();return;}
        game=gameWindow();gameBounds=Native.Bounds(game);
        bool active=game!=IntPtr.Zero&&!Native.IsIconic(game)&&Native.SameProcess(foregroundWindow(),game)&&gameBounds.Width>=640&&gameBounds.Height>=360;
        if(!active){if(Visible)Hide();return;}
        liveFeed=live.Poll(game,info,settings.Replay);Bounds=gameBounds;if(!Visible)Show();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e);Render(e.Graphics,ClientSize);}
    public void Render(Graphics g,Size size) {
        int w=size.Width,h=size.Height;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(preview){g.Clear(Color.FromArgb(25,35,41));using(var f=new Font("Malgun Gothic",15))g.DrawString("관전 HUD 미리보기  /  ESC 닫기",f,Brushes.Gray,30,30);}
        var feed=preview?null:liveFeed??Feed.Read(settings.Replay);bool connected=feed!=null;
        int reserve=feed!=null&&feed.Selection!=null?Math.Min(330,w/3)+12:0;
        int x=12, width=Math.Max(460,Math.Min(w-x-12-reserve,860));int rows=Math.Max(2,info.Players.Count);int y=h-12-36-rows*36;
        using(var bg=new SolidBrush(Color.FromArgb(11,26,36)))g.FillRectangle(bg,x,y,width,36+rows*36);
        using(var small=new Font("Malgun Gothic",9))using(var normal=new Font("Malgun Gothic",10,FontStyle.Bold)) {
            string map=info.Map.Replace("283data/maps/","");int mapWidth=Math.Max(70,width-340);Draw(g,map,small,Brushes.LightSteelBlue,new Rectangle(x+10,y+4,mapWidth,27));Draw(g,feed!=null&&!String.IsNullOrEmpty(feed.GameTime)?feed.GameTime:"--:--",normal,Brushes.White,new Rectangle(x+mapWidth+16,y+3,90,27));
            Draw(g,preview?"PREVIEW":connected?"LIVE · 자금 수집 중":live.Status,small,connected?Brushes.Turquoise:Brushes.Goldenrod,new Rectangle(x+width-240,y+4,230,27));
            for(int n=0;n<rows;n++) {int ry=y+36+n*36;Player player=n<info.Players.Count?info.Players[n]:null;
                using(var b=new SolidBrush(n%2==0?Color.FromArgb(23,42,55):Color.FromArgb(18,35,47)))g.FillRectangle(b,x,ry,width,35);
                Color c=player==null?Color.DimGray:player.Color;using(var b=new SolidBrush(c))g.FillRectangle(b,x,ry,5,35);
                TelemetryPlayer t=connected&&feed.Players!=null&&player!=null?feed.Players.FirstOrDefault(z=>z.Slot==player.Slot):null;
                string faction=t!=null&&!String.IsNullOrWhiteSpace(t.Faction)?t.Faction:player==null?"진영 대기":player.Faction;
                FactionIcons.Draw(g,new Rectangle(x+13,ry+5,48,24),faction,settings.ModPath);Draw(g,faction,small,Brushes.Silver,new Rectangle(x+68,ry+7,86,25));
                Draw(g,player==null?"플레이어 대기":player.Name,normal,Brushes.White,new Rectangle(x+160,ry+6,Math.Max(75,width-482),25));
                Draw(g,"$ "+(t!=null&&t.Credits.HasValue?t.Credits.Value.ToString("N0"):"—"),normal,Brushes.White,new Rectangle(x+width-300,ry+6,105,25));
                Draw(g,t!=null&&t.Income.HasValue?"수입 "+t.Income.Value.ToString("N0"):"수입 미지원",small,Brushes.LightSteelBlue,new Rectangle(x+width-190,ry+7,100,25));
                Draw(g,"전력 "+(t!=null&&t.PowerUsed.HasValue&&t.PowerTotal.HasValue?t.PowerUsed+"/"+t.PowerTotal:"—"),small,Brushes.LightSteelBlue,new Rectangle(x+width-90,ry+7,88,25));
            }
        }
        if(feed!=null&&feed.Selection!=null)DrawSelection(g,size,feed.Selection);
    }
    static void DrawSelection(Graphics g,Size size,UnitSelection unit){int width=Math.Min(330,size.Width/3),height=170;var panel=new Rectangle(size.Width-width-12,size.Height-height-12,width,height);using(var bg=new SolidBrush(Color.FromArgb(12,29,39)))g.FillRectangle(bg,panel);g.DrawRectangle(Pens.Teal,panel);using(var title=new Font("Malgun Gothic",11,FontStyle.Bold))using(var small=new Font("Malgun Gothic",9)){Draw(g,unit.Name+(unit.Count>1?" 외 "+(unit.Count-1)+"개":""),title,Brushes.White,new Rectangle(panel.X+12,panel.Y+12,width-24,29));Draw(g,"소유자  "+unit.Owner+" · "+unit.Faction,small,Brushes.LightSteelBlue,new Rectangle(panel.X+12,panel.Y+51,width-24,24));Draw(g,"체력  "+unit.Health.ToString("N0")+" / "+unit.MaxHealth.ToString("N0"),small,Brushes.White,new Rectangle(panel.X+12,panel.Y+80,width-24,24));var bar=new Rectangle(panel.X+12,panel.Y+115,width-24,12);using(var bg=new SolidBrush(Color.FromArgb(44,60,66)))g.FillRectangle(bg,bar);using(var hp=new SolidBrush(Color.FromArgb(83,204,125)))g.FillRectangle(hp,bar.X,bar.Y,(float)(bar.Width*Math.Min(1,Math.Max(0,unit.Health/unit.MaxHealth))),bar.Height);Draw(g,unit.Description??("선택 "+unit.Count+"개"),small,Brushes.Silver,new Rectangle(panel.X+12,panel.Y+137,width-24,24));}}
    static void Draw(Graphics g,string text,Font font,Brush brush,Rectangle r) {using(var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text??"",font,brush,r,format);}
}
static class Tests {
    public static int Run() {
        try {
            if(LiveGame.Clock(0)!="00:00"||LiveGame.Clock(15*61)!="01:01"||LiveGame.Clock(15*3661)!="01:01:01")throw new Exception("Replay clock formatting regression");
            var edgeBounds=new Rectangle(100,200,1600,900);if(EdgeScroll.Directions(edgeBounds,new Point(900,650),10).Length!=0||!EdgeScroll.Directions(edgeBounds,new Point(1699,650),10).SequenceEqual(new[]{0x27})||!EdgeScroll.Directions(edgeBounds,new Point(100,200),10).SequenceEqual(new[]{0x25,0x26})||EdgeScroll.Directions(edgeBounds,new Point(1700,650),10).Length!=0)throw new Exception("Edge-scroll direction regression");
            if(!Native.IsGameProcess("ra3_1.12.game")||Native.IsGameProcess("ra3-helper")||Native.IsGameProcess("GenevoLauncher"))throw new Exception("Game process filter regression");
            using(var fixture=new Form {Text="Genevo test game",ClientSize=new Size(900,600),StartPosition=FormStartPosition.Manual,Location=new Point(10,10)})using(var helper=new Form {ClientSize=new Size(100,25),ShowInTaskbar=false}) {
                fixture.Show();helper.Show();Application.DoEvents();var ids=new HashSet<uint>{(uint)Process.GetCurrentProcess().Id};if(Native.FindGameWindow(ids)!=fixture.Handle)throw new Exception("Small helper window selected instead of game");
                fixture.WindowState=FormWindowState.Minimized;Application.DoEvents();Native.ShowWindowAsync(fixture.Handle,9);for(int wait=0;wait<20&&Native.IsIconic(fixture.Handle);wait++){Application.DoEvents();System.Threading.Thread.Sleep(25);}if(Native.IsIconic(fixture.Handle)||Native.Bounds(fixture.Handle).Width<640)throw new Exception("Minimized window restore failed");
                IntPtr previousFocus=Native.GetForegroundWindow();using(var hud=new Overlay(new Settings(),new ReplayInfo(),false,()=>fixture.Handle,()=>fixture.Handle)){hud.RefreshState();Application.DoEvents();if(!hud.Visible||hud.Bounds!=Native.Bounds(fixture.Handle))throw new Exception("HUD did not appear over active game window");if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"&&Native.GetForegroundWindow()==hud.Handle)throw new Exception("HUD stole foreground focus");fixture.WindowState=FormWindowState.Minimized;Application.DoEvents();hud.RefreshState();if(hud.Visible)throw new Exception("HUD not hidden for minimized game");hud.Close();}
                helper.Close();fixture.Close();
            }
            string temp=Path.GetTempFileName();
            try {File.WriteAllText(temp,"RA3 REPLAY HEADER;M=283data/maps/test;MC=0;S=HAlpha,0,0,TT,1,24,-1:HBravo,0,0,TT,4,25,-1:Hpost Commentator,0,0,TT,-1,12,-1:;");var r=ReplayInfo.Read(temp);if(r.Players.Count!=2||r.Players[0].Name!="Alpha"||r.Players[1].Faction!="진영 #25")throw new Exception("Replay parsing failed");File.WriteAllText(temp,"bad");bool rejected=false;try{ReplayInfo.Read(temp);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Bad replay accepted");} finally{File.Delete(temp);}
            string libraryFolder=Path.Combine(Path.GetTempPath(),"genevo-replay-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(libraryFolder);
            try {
                string good=Path.Combine(libraryFolder,"valid.RA3Replay"),bad=Path.Combine(libraryFolder,"broken.RA3Replay"),other=Path.Combine(libraryFolder,"ignore.txt");
                File.WriteAllText(good,"RA3 REPLAY HEADER;M=283data/maps/test;MC=0;S=HAlpha,0,0,TT,1,24,-1:HBravo,0,0,TT,4,25,-1:;");
                File.WriteAllText(bad,"bad");File.WriteAllText(other,"ignore");File.SetLastWriteTimeUtc(good,DateTime.UtcNow.AddHours(-1));
                var entries=ReplayLibrary.Read(libraryFolder);
                if(entries.Count!=2||entries[0].Path!=bad||entries[0].Valid||!entries[1].Valid||entries[1].Players!="Alpha vs Bravo")throw new Exception("Replay library filtering/order/metadata regression");
                string arguments=Launcher.LaunchArguments(@"C:\Gen Evo\mod.skudef",good,1600,900);
                if(arguments!="-win -xres 1600 -yres 900 -modConfig \"C:\\Gen Evo\\mod.skudef\" -replayGame \""+Path.GetFullPath(good)+"\"")throw new Exception("Windowed replay arguments regression");
                File.Delete(good);File.Delete(bad);File.Delete(other);
            }finally{foreach(string file in Directory.GetFiles(libraryFolder))File.Delete(file);Directory.Delete(libraryFolder);}
            if(FactionIcons.Kind("GLA 독소")!="GLA"||FactionIcons.Kind("미국 레이저")!="USA"||FactionIcons.Kind("중국")!="CHINA"||FactionIcons.Kind("진영 대기")!="UNKNOWN")throw new Exception("Faction icon mapping regression");
            string real=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"Red Alert 3\Replays\LazvsGLAV#2.RA3Replay");
            var info=File.Exists(real)?ReplayInfo.Read(real):new ReplayInfo();if(File.Exists(real)&&info.Players.Count!=2)throw new Exception("Real replay parsing failed");
            if(File.Exists(real)&&Directory.Exists(Settings.Load().ModPath)&&info.Map.EndsWith("genevo033_aymcam_skrm_01")){string display=ModMetadata.Map(Settings.Load().ModPath,info.Map);if(display!="[GenEvo-Dev] Cold Drops")throw new Exception("Real BIG/map metadata decode failed");info.Map=display;}
            if(ModMetadata.Faction("FactionGLAToxinGeneral")!="GLA 독소"||ModMetadata.Faction("FactionAmerica")!="미국")throw new Exception("Faction metadata decode failed");
            string[] factions={"미국","미국 레이저","미국 슈퍼무기","미국 공군","중국","중국 핵","중국 탱크","중국 보병","GLA","GLA 독소","GLA 폭파","GLA 스텔스"};
            if(FactionIcons.AssetName("FactionAmericaAirForceGeneral")!="FactionAmericaAirforce"||FactionIcons.AssetName("FactionGLAToxinGeneral")!="FactionGLAToxin"||FactionIcons.AssetName("중국 핵")!="FactionChinaNuke")throw new Exception("Subfaction icon mapping regression");
            byte[] dds=new byte[144];Action<int,uint> put=(offset,value)=>Array.Copy(BitConverter.GetBytes(value),0,dds,offset,4);put(0,0x20534444);put(4,124);put(12,4);put(16,4);put(84,0x35545844);dds[128]=255;dds[129]=0;dds[136]=0;dds[137]=248;dds[138]=224;dds[139]=7;dds[140]=0xe4;
            using(var decoded=FactionIcons.DecodeDxt5(dds,0)){if(decoded.GetPixel(0,0).ToArgb()!=Color.Red.ToArgb()||decoded.GetPixel(1,0).ToArgb()!=Color.Lime.ToArgb()||decoded.GetPixel(2,0).R!=170||decoded.GetPixel(2,0).G!=85)throw new Exception("BC3 color palette decode regression");}
            dds[128]=0;dds[129]=128;dds[130]=7;
            using(var decoded=FactionIcons.DecodeDxt5(dds,0)){if(decoded.GetPixel(0,0).A!=255||decoded.GetPixel(1,0).A!=0)throw new Exception("BC3 transparency decode regression");}
            string modPath=Settings.Load().ModPath;
            if(Directory.Exists(modPath)){
                if(!FactionIcons.Available(modPath)||factions.Any(f=>!FactionIcons.HasIcon(modPath,f)))throw new Exception("Installed GenEvo faction artwork missing");
                using(var sheet=new Bitmap(600,40*factions.Length))using(var g=Graphics.FromImage(sheet))using(var font=new Font("Malgun Gothic",11)){
                    g.Clear(Color.FromArgb(18,35,47));for(int n=0;n<factions.Length;n++){FactionIcons.Draw(g,new Rectangle(12,n*40+4,64,32),factions[n],modPath);g.DrawString(factions[n]+" · "+FactionIcons.AssetName(factions[n]),font,Brushes.White,90,n*40+8);}sheet.Save(Path.Combine(Program.Data,"faction-icons-preview.png"));}
            }
            using(var form=new Overlay(new Settings(),info,true))using(var bitmap=new Bitmap(1330,736))using(var graphics=Graphics.FromImage(bitmap)){form.Render(graphics,bitmap.Size);bitmap.Save(Path.Combine(Program.Data,"hud-preview.png"));}
            using(var launcher=new Launcher(false)){launcher.Show();Application.DoEvents();using(var bitmap=new Bitmap(launcher.Width,launcher.Height)){launcher.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Program.Data,"launcher-preview.png"));}launcher.Close();}
            File.WriteAllText(Path.Combine(Program.Data,"test-result.txt"),"PASS: game window selection, minimized window restore, HUD visibility/focus/bounds, replay metadata, malformed replay, real replay, replay library, windowed launch arguments, faction icons, HUD render");return 0;
        }catch(Exception ex){Program.Log(ex);File.WriteAllText(Path.Combine(Program.Data,"test-result.txt"),ex.ToString());return 1;}
    }
}
}
