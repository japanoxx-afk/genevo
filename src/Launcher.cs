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

[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
namespace Genevo {
static class Program {
    public static string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenevoObserver");
    public static JavaScriptSerializer Json = new JavaScriptSerializer();
    [STAThread] static int Main(string[] args) {
        Directory.CreateDirectory(Data);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--self-test")) return Tests.Run();
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
    public string Replay = "";
    public float RadarX, RadarY, RadarW, RadarH;
    public static Settings Load() { try { return Program.Json.Deserialize<Settings>(File.ReadAllText(Path.Combine(Program.Data,"settings.json"))) ?? new Settings(); } catch { return new Settings(); } }
    public void Save() { File.WriteAllText(Path.Combine(Program.Data,"settings.json"),Program.Json.Serialize(this)); }
}
public class Player { public string Name; public string Faction; public int Slot; public Color Color; }
public class ReplayInfo {
    public string Map = "리플레이를 선택하세요"; public List<Player> Players = new List<Player>();
    public static ReplayInfo Read(string path) {
        byte[] bytes = new byte[65536]; int count;
        using (var stream = File.OpenRead(path)) count = stream.Read(bytes,0,bytes.Length);
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
public class Telemetry { public string Replay; public string CapturedUtc; public string GameTime; public List<TelemetryPlayer> Players; }
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
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    public static IntPtr Game() { foreach(var p in Process.GetProcesses()) { using(p) { try { if(p.ProcessName.StartsWith("ra3",StringComparison.OrdinalIgnoreCase) && p.MainWindowHandle!=IntPtr.Zero) return p.MainWindowHandle; } catch {} } } return IntPtr.Zero; }
    public static Rectangle Bounds(IntPtr h) { Rect r; Point pt=Point.Empty; if (!GetClientRect(h,out r) || !ClientToScreen(h,ref pt)) return Rectangle.Empty; return new Rectangle(pt.X,pt.Y,r.R-r.L,r.B-r.T); }
}
class Launcher : Form {
    Settings settings=Settings.Load(); TextBox game,mod,replay; Label status; Overlay overlay;
    public Launcher(bool preview) {
        Text="GENEVO / OBSERVER"; ClientSize=new Size(820,620); MinimumSize=new Size(836,659); BackColor=Color.FromArgb(13,22,30); ForeColor=Color.White; Font=new Font("Malgun Gothic",10); StartPosition=FormStartPosition.CenterScreen;
        Label title=new Label {Text="GENEVO",Font=new Font("Segoe UI",30,FontStyle.Bold),AutoSize=true,Location=new Point(30,20)}; Controls.Add(title);
        Controls.Add(new Label {Text="REPLAY OBSERVER  /  v0.1.0",ForeColor=Color.Turquoise,AutoSize=true,Location=new Point(34,82)});
        game=PathRow("RED ALERT 3",settings.GamePath,125,false); mod=PathRow("GENEVO MOD",settings.ModPath,202,false); replay=PathRow("REPLAY",settings.Replay,279,true);
        Button play=Button("GenEvo 공식 런처 실행",30,370,235); play.Click+=(s,e)=>Safe(()=> {Save(); string exe=Path.Combine(settings.ModPath,"GenEvoLauncher.exe"); if(!File.Exists(exe))throw new FileNotFoundException("GenEvoLauncher.exe 경로를 확인하세요."); Process.Start(new ProcessStartInfo(exe){WorkingDirectory=settings.ModPath,UseShellExecute=true});});
        Button open=Button("리플레이 폴더",280,370,180); open.Click+=(s,e)=>Safe(()=> {string p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"Red Alert 3\Replays"); Directory.CreateDirectory(p); Process.Start("explorer.exe",p);});
        Button start=Button("관전 HUD 켜기",30,424,180); start.Click+=(s,e)=>Safe(()=>ShowHud(false));
        Button test=Button("HUD 미리보기",225,424,180); test.Click+=(s,e)=>Safe(()=>ShowHud(true));
        Button radar=Button("미니맵 영역 지정",420,424,180); radar.Click+=(s,e)=>Safe(Calibrate);
        Button stop=Button("HUD 끄기",615,424,170); stop.Click+=(s,e)=> {if(overlay!=null)overlay.Close();};
        Controls.Add(new Label {Text="사용 순서: 공식 런처 → 게임에서 리플레이 재생 → 미니맵 영역 지정 → HUD 켜기\n창 모드/테두리 없는 창에서 사용하세요. F8: HUD 표시 전환\n플레이어 이름은 리플레이에서 읽습니다. 자원·진영명은 실시간 데이터 연결 대기 중입니다.",Location=new Point(30,490),Size=new Size(760,75),ForeColor=Color.Silver});
        status=new Label {Location=new Point(30,580),Size=new Size(760,30),ForeColor=Color.Turquoise,Text="업데이트 상태 확인 중"}; Controls.Add(status);
        string state=Path.Combine(Program.Data,"update-status.txt"); status.Text=File.Exists(state)?File.ReadAllText(state):"미리보기 · 업데이트 확인 생략";
        FormClosed+=(s,e)=> {if(overlay!=null)overlay.Close();}; if(preview) Shown+=(s,e)=>Safe(()=>ShowHud(true));
    }
    TextBox PathRow(string label,string value,int y,bool file) {
        Controls.Add(new Label {Text=label,Location=new Point(30,y),Size=new Size(750,22),ForeColor=Color.FromArgb(115,149,165)});
        TextBox t=new TextBox {Text=value,Location=new Point(30,y+27),Size=new Size(650,30),BackColor=Color.FromArgb(25,39,50),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle}; Controls.Add(t);
        Button b=Button("찾기",695,y+25,90); b.Click+=(s,e)=> {if(file) {using(var d=new OpenFileDialog {Filter="RA3 Replay|*.RA3Replay",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"Red Alert 3\Replays")}) if(d.ShowDialog()==DialogResult.OK)t.Text=d.FileName;} else {using(var d=new FolderBrowserDialog {SelectedPath=t.Text})if(d.ShowDialog()==DialogResult.OK)t.Text=d.SelectedPath;}}; return t;
    }
    Button Button(string text,int x,int y,int w) {var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(w,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(26,50,62),ForeColor=Color.White}; b.FlatAppearance.BorderColor=Color.FromArgb(51,92,106); Controls.Add(b);return b;}
    void Save() {settings.GamePath=game.Text;settings.ModPath=mod.Text;settings.Replay=replay.Text;settings.Save();}
    void Safe(Action a) {try {a();}catch(Exception ex){Program.Log(ex);MessageBox.Show(this,ex.Message,"Genevo");}}
    void ShowHud(bool preview) {Save();ReplayInfo info=String.IsNullOrWhiteSpace(settings.Replay)?new ReplayInfo():ReplayInfo.Read(settings.Replay); if(!preview && info.Players.Count==0)throw new Exception("관전할 리플레이 파일을 먼저 선택하세요."); if(overlay!=null)overlay.Close();overlay=new Overlay(settings,info,preview);overlay.Show();}
    void Calibrate() {
        Save();IntPtr h=Native.Game();if(h==IntPtr.Zero)throw new Exception("게임을 창 모드로 실행한 뒤 다시 시도하세요.");Rectangle bounds=Native.Bounds(h);if(bounds.Width<200)throw new Exception("게임 창이 최소화되어 있습니다.");
        if(overlay!=null)overlay.Close();using(var selector=new Selector(bounds)) {if(selector.ShowDialog()==DialogResult.OK){Rectangle r=selector.Selected;settings.RadarX=(float)r.X/bounds.Width;settings.RadarY=(float)r.Y/bounds.Height;settings.RadarW=(float)r.Width/bounds.Width;settings.RadarH=(float)r.Height/bounds.Height;settings.Save();status.Text="미니맵 영역 저장 완료";}}
    }
}
class Selector : Form {
    Point start; public Rectangle Selected; bool dragging;
    public Selector(Rectangle bounds) {Bounds=bounds;FormBorderStyle=FormBorderStyle.None;TopMost=true;BackColor=Color.Black;Opacity=.45;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)Close();};MouseDown+=(s,e)=>{start=e.Location;dragging=true;};MouseMove+=(s,e)=>{if(dragging){Selected=Rectangle.FromLTRB(Math.Min(start.X,e.X),Math.Min(start.Y,e.Y),Math.Max(start.X,e.X),Math.Max(start.Y,e.Y));Invalidate();}};MouseUp+=(s,e)=>{dragging=false;if(Selected.Width>=20&&Selected.Height>=20){DialogResult=DialogResult.OK;Close();}};}
    protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e);e.Graphics.DrawString("게임의 미니맵 영역을 드래그하세요 · ESC 취소",SystemFonts.CaptionFont,Brushes.White,25,25);e.Graphics.DrawRectangle(Pens.Cyan,Selected);}
}
class Overlay : Form {
    Settings settings;ReplayInfo info;bool preview,hidden;Timer timer=new Timer();Bitmap radar;IntPtr game;Rectangle gameBounds;string captureState="미니맵 영역을 지정하세요";bool f8Down;
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    public Overlay(Settings s,ReplayInfo i,bool p) {settings=s;info=i;preview=p;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=p;TopMost=true;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;StartPosition=FormStartPosition.Manual;
        if(p){Bounds=new Rectangle(100,100,1100,660);Text="HUD 미리보기 · ESC 닫기";KeyPreview=true;KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Escape)Close();};}
        timer.Interval=100;timer.Tick+=(o,e)=>Tick();timer.Start();FormClosed+=(o,e)=>{timer.Dispose();if(radar!=null)radar.Dispose();};}
    protected override bool ShowWithoutActivation {get{return !preview;}}
    protected override CreateParams CreateParams {get {var p=base.CreateParams;if(!preview)p.ExStyle|=0x08000000|0x20|0x80;return p;}}
    void Tick() {
        if(preview){Invalidate();return;}
        bool down=(GetAsyncKeyState(0x77)&0x8000)!=0;if(down&&!f8Down)hidden=!hidden;f8Down=down;
        game=Native.Game();gameBounds=Native.Bounds(game);
        bool active=game!=IntPtr.Zero&&!Native.IsIconic(game)&&Native.GetForegroundWindow()==game&&gameBounds.Width>200&&!hidden;
        if(!active){if(Visible)Hide();return;}
        Bounds=gameBounds;if(!Visible)Show();CaptureRadar();Invalidate();
    }
    void CaptureRadar() {
        if(settings.RadarW<=0||settings.RadarH<=0)return;
        Rectangle r=new Rectangle((int)(settings.RadarX*Width),(int)(settings.RadarY*Height),(int)(settings.RadarW*Width),(int)(settings.RadarH*Height));
        Rectangle destination=new Rectangle(12,Height-250,238,238);
        if(r.Width<20||r.Height<20||!ClientRectangle.Contains(r)||r.IntersectsWith(destination)){captureState="미니맵 원본 위치를 다시 지정하세요";if(radar!=null){radar.Dispose();radar=null;}return;}
        try{using(var b=new Bitmap(r.Width,r.Height)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(gameBounds.X+r.X,gameBounds.Y+r.Y,0,0,r.Size,CopyPixelOperation.SourceCopy);if(radar!=null)radar.Dispose();radar=new Bitmap(b);}captureState="LIVE RADAR";}catch{captureState="미니맵 캡처 실패";if(radar!=null){radar.Dispose();radar=null;}}
    }
    protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e);Render(e.Graphics,ClientSize);}
    public void Render(Graphics g,Size size) {
        int w=size.Width,h=size.Height;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(preview){g.Clear(Color.FromArgb(25,35,41));using(var f=new Font("Malgun Gothic",15))g.DrawString("관전 HUD 미리보기  /  ESC 닫기",f,Brushes.Gray,30,30);}
        int radarSize=Math.Min(238,Math.Max(130,w/5));Rectangle box=new Rectangle(12,h-radarSize-12,radarSize,radarSize);
        using(var bg=new SolidBrush(Color.FromArgb(10,23,31)))g.FillRectangle(bg,box);g.DrawRectangle(Pens.Teal,box);
        if(radar!=null)g.DrawImage(radar,Rectangle.Inflate(box,-4,-4));else using(var f=new Font("Malgun Gothic",9))g.DrawString(captureState,f,Brushes.Silver,new RectangleF(box.X+15,box.Y+box.Height/2-20,box.Width-30,70));
        int x=box.Right+8, width=Math.Max(100,Math.Min(w-x-12,860));int rows=Math.Max(2,info.Players.Count);int y=h-12-36-rows*36;
        using(var bg=new SolidBrush(Color.FromArgb(11,26,36)))g.FillRectangle(bg,x,y,width,36+rows*36);
        var feed=Feed.Read(settings.Replay);bool live=feed!=null;
        using(var small=new Font("Malgun Gothic",9))using(var normal=new Font("Malgun Gothic",10,FontStyle.Bold)) {
            string map=info.Map.Replace("283data/maps/","");Draw(g,map,small,Brushes.LightSteelBlue,new Rectangle(x+10,y+4,width-200,27));
            Draw(g,preview?"PREVIEW":live?"LIVE  "+feed.GameTime:"자원 데이터 연결 대기",small,live?Brushes.Turquoise:Brushes.Goldenrod,new Rectangle(x+width-190,y+4,180,27));
            for(int n=0;n<rows;n++) {int ry=y+36+n*36;Player player=n<info.Players.Count?info.Players[n]:null;
                using(var b=new SolidBrush(n%2==0?Color.FromArgb(23,42,55):Color.FromArgb(18,35,47)))g.FillRectangle(b,x,ry,width,35);
                Color c=player==null?Color.DimGray:player.Color;using(var b=new SolidBrush(c))g.FillRectangle(b,x,ry,5,35);
                TelemetryPlayer t=live&&feed.Players!=null&&player!=null?feed.Players.FirstOrDefault(z=>z.Slot==player.Slot):null;
                string faction=t!=null&&!String.IsNullOrWhiteSpace(t.Faction)?t.Faction:player==null?"진영 대기":player.Faction;
                Draw(g,faction,small,Brushes.Silver,new Rectangle(x+14,ry+7,100,25));
                Draw(g,player==null?"플레이어 대기":player.Name,normal,Brushes.White,new Rectangle(x+118,ry+6,Math.Max(75,width-440),25));
                Draw(g,"$ "+(t!=null&&t.Credits.HasValue?t.Credits.Value.ToString("N0"):"—"),normal,Brushes.White,new Rectangle(x+width-300,ry+6,105,25));
                Draw(g,"수입 "+(t!=null&&t.Income.HasValue?t.Income.Value.ToString("N0"):"—"),small,Brushes.LightSteelBlue,new Rectangle(x+width-190,ry+7,100,25));
                Draw(g,"전력 "+(t!=null&&t.PowerUsed.HasValue&&t.PowerTotal.HasValue?t.PowerUsed+"/"+t.PowerTotal:"—"),small,Brushes.LightSteelBlue,new Rectangle(x+width-90,ry+7,88,25));
            }
        }
    }
    static void Draw(Graphics g,string text,Font font,Brush brush,Rectangle r) {using(var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text??"",font,brush,r,format);}
}
static class Tests {
    public static int Run() {
        try {
            string temp=Path.GetTempFileName();
            try {File.WriteAllText(temp,"RA3 REPLAY HEADER;M=283data/maps/test;MC=0;S=HAlpha,0,0,TT,1,24,-1:HBravo,0,0,TT,4,25,-1:Hpost Commentator,0,0,TT,-1,12,-1:;");var r=ReplayInfo.Read(temp);if(r.Players.Count!=2||r.Players[0].Name!="Alpha"||r.Players[1].Faction!="진영 #25")throw new Exception("Replay parsing failed");File.WriteAllText(temp,"bad");bool rejected=false;try{ReplayInfo.Read(temp);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Bad replay accepted");} finally{File.Delete(temp);}
            string real=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"Red Alert 3\Replays\LazvsGLAV#2.RA3Replay");
            var info=File.Exists(real)?ReplayInfo.Read(real):new ReplayInfo();if(File.Exists(real)&&info.Players.Count!=2)throw new Exception("Real replay parsing failed");
            using(var form=new Overlay(new Settings(),info,true))using(var bitmap=new Bitmap(1330,736))using(var graphics=Graphics.FromImage(bitmap)){form.Render(graphics,bitmap.Size);bitmap.Save(Path.Combine(Program.Data,"hud-preview.png"));}
            File.WriteAllText(Path.Combine(Program.Data,"test-result.txt"),"PASS: replay metadata, malformed replay, real replay, HUD render");return 0;
        }catch(Exception ex){Program.Log(ex);File.WriteAllText(Path.Combine(Program.Data,"test-result.txt"),ex.ToString());return 1;}
    }
}
}
