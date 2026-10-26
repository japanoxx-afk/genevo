using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Genevo {
// External, read-only reader. This profile was verified against the local Steam 1.12 executable.
sealed class LiveGame : IDisposable {
    const string VerifiedBuild="29AA6B66FC298188FA42DF7218A1DEA7E181A946A54D0D8841000906F9F4D98E";
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr count,out UIntPtr read);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    IntPtr handle; uint pid;long imageBase;long contextAddress;DateTime nextAttach;DateTime nextSample;Telemetry cached;
    public string Status="게임 자원 연결 대기";
    public void Dispose(){if(handle!=IntPtr.Zero)CloseHandle(handle);handle=IntPtr.Zero;pid=0;cached=null;}
    byte[] Read(long address,int size){if(address<0x10000||address>uint.MaxValue||size<1||size>4096)throw new InvalidDataException("Invalid memory range");var b=new byte[size];UIntPtr n;if(!ReadProcessMemory(handle,new IntPtr(address),b,(UIntPtr)size,out n)||n.ToUInt64()!=(ulong)size)throw new IOException("게임 메모리 읽기 실패");return b;}
    uint U32(long p){return BitConverter.ToUInt32(Read(p,4),0);}
    string Text(uint p,bool unicode){if(p<0x10000)return "";byte[] b=Read(p,128);if(unicode){int end=0;while(end+1<b.Length&&(b[end]!=0||b[end+1]!=0))end+=2;return Encoding.Unicode.GetString(b,0,end);}int n=Array.IndexOf(b,(byte)0);return Encoding.ASCII.GetString(b,0,n<0?b.Length:n);}
    bool Attach(IntPtr window){uint current;Native.GetWindowThreadProcessId(window,out current);if(current==pid&&handle!=IntPtr.Zero)return true;
        Dispose();if(DateTime.UtcNow<nextAttach)return false;nextAttach=DateTime.UtcNow.AddSeconds(3);
        using(var process=Process.GetProcessById((int)current)){
            string path=process.MainModule.FileName;string hash;using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            if(hash!=VerifiedBuild){Status="이 게임 빌드는 자원 수집 미지원";return false;}
            imageBase=process.MainModule.BaseAddress.ToInt64();handle=OpenProcess(0x410,false,current);if(handle==IntPtr.Zero){Status="게임 읽기 권한 없음 · 게임과 런처 권한을 맞추세요";return false;}pid=current;
            // Verified instruction: mov eax,[PlayerList]; ... mov edx,[eax+28]; mov eax,[edx+20].
            byte[] signature=Read(imageBase+0x141188,25);if(signature[0]!=0xA1||signature[19]!=0x8B||signature[20]!=0x50||signature[21]!=0x28||signature[22]!=0x8B||signature[23]!=0x42||signature[24]!=0x20){Dispose();Status="게임 구조 검증 실패";return false;}
            contextAddress=BitConverter.ToUInt32(signature,1);return true;
        }
    }
    string Faction(uint player){uint template=U32(player+0x10);byte[] b=Read(template,256);for(int off=0;off<b.Length;off+=4){uint p=BitConverter.ToUInt32(b,off);if(p<0x10000||p>=0x80000000)continue;try{string s=Text(p,false);if(s.StartsWith("INI:Faction",StringComparison.Ordinal)){return ModMetadata.Faction(s.Substring(4));}}catch(IOException){}}return null;}
    float F32(long p){return BitConverter.ToSingle(Read(p,4),0);}
    static bool Finite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
    public static string Clock(uint frames){uint seconds=frames/15;return seconds>=3600?(seconds/3600).ToString("00")+":"+((seconds/60)%60).ToString("00")+":"+(seconds%60).ToString("00"):(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
    string GameTime(){try{uint logic=U32(imageBase+0x8dde84);string path=Text(U32(logic+0x98),false);if(!path.EndsWith(".map",StringComparison.OrdinalIgnoreCase))return null;uint frame=U32(logic+0x50);if(frame>15*60*60*24)return null;return Clock(frame);}catch{return null;}}
    UnitSelection Selection(Dictionary<uint,string> owners,Dictionary<string,TelemetryPlayer> players){try{uint manager=U32(imageBase+0x8e08dc);int count=(int)U32(manager+0x5c);if(count<=0||count>2048)return null;uint node=U32(manager+0x50),drawable=U32(node+8),thing=U32(drawable+0x138);if(U32(thing)!=imageBase+0x854bb0)return null;uint owner=U32(thing+0x418);string ownerName;if(!owners.TryGetValue(owner,out ownerName))return null;
        string name=Text(U32(U32(thing+4)+0x1c),false);if(name.Length<2||name.Length>100||!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[A-Za-z0-9_]+$"))return null;
        uint hp=U32(thing+0x33c);float current=F32(hp+4),max=F32(hp+12);if(!Finite(current)||!Finite(max)||current<0||max<=0||max>100000000)return null;
        return new UnitSelection {Count=count,Name=PrettyUnit(name),Type=name,Owner=ownerName,Faction=players[ownerName].Faction,Description=UnitDescription(name),Health=current,MaxHealth=max};}catch{return null;}}
    static string PrettyUnit(string name){string[] suffixes={"WarFactory","CommandCenter","Barracks","PowerPlant","SupplyCenter","SupplyStash"};string[] labels={"군수공장","사령부","병영","발전소","보급센터","보급창고"};for(int n=0;n<suffixes.Length;n++)if(name.EndsWith(suffixes[n],StringComparison.Ordinal))return (name.StartsWith("Lazr_",StringComparison.Ordinal)?"레이저 ":"")+labels[n];return System.Text.RegularExpressions.Regex.Replace(name.Replace('_',' '),"([a-z])([A-Z])","$1 $2");}
    static string UnitDescription(string name){if(name.EndsWith("WarFactory",StringComparison.Ordinal))return "전차와 차량을 생산하는 시설";if(name.EndsWith("Barracks",StringComparison.Ordinal))return "보병을 생산하는 시설";if(name.EndsWith("PowerPlant",StringComparison.Ordinal))return "기지에 전력을 공급하는 시설";if(name.EndsWith("SupplyCenter",StringComparison.Ordinal)||name.EndsWith("SupplyStash",StringComparison.Ordinal))return "수집한 자원을 보관하는 보급 시설";return null;}
    public Telemetry Poll(IntPtr window,ReplayInfo info,string replay){
        if(DateTime.UtcNow<nextSample)return cached;nextSample=DateTime.UtcNow.AddMilliseconds(200);cached=null;
        if(window==IntPtr.Zero||info.Players.Count==0)return null;
        try{
            if(!Attach(window))return null;uint context=U32(contextAddress),local=U32(context+0x28);
            if(!Text(U32(local+0x18),false).StartsWith("ReplayObserver",StringComparison.Ordinal)){Status="리플레이 관전자 모드에서만 자원 수집";return null;}
            int count=(int)U32(context+0x2c);if(count<3||count>18)throw new InvalidDataException("Invalid player count");
            var found=new Dictionary<string,TelemetryPlayer>(StringComparer.Ordinal);var colors=new Dictionary<string,Color>(StringComparer.Ordinal);var owners=new Dictionary<uint,string>();
            for(int index=0;index<count;index++){
                uint player=U32(context+0x30+index*4);if(U32(player+0x20)!=index)throw new InvalidDataException("Player index mismatch");string name=Text(U32(player+0x14),true);
                var expected=info.Players.FirstOrDefault(p=>p.Name==name);if(expected==null)continue;if(found.ContainsKey(name))throw new InvalidDataException("Duplicate player name");
                uint begin=U32(player+0xe4),end=U32(player+0xe8);if(end-begin!=4)throw new InvalidDataException("Unsupported resource count");uint money=U32(begin);byte[] cash=Read(money,24);long credits=(long)BitConverter.ToInt32(cash,4)+BitConverter.ToInt32(cash,20);if(BitConverter.ToUInt32(cash,12)!=index||credits<0||credits>100000000)throw new InvalidDataException("Resource validation failed");
                uint power=U32(player+0x74);int total=(int)U32(power+4),used=(int)U32(power+8);if(total<0||used<0||total>1000000||used>1000000)throw new InvalidDataException("Power validation failed");
                found[name]=new TelemetryPlayer {Slot=expected.Slot,Name=name,Faction=Faction(player),Credits=credits,PowerUsed=used,PowerTotal=total};colors[name]=Color.FromArgb(unchecked((int)U32(player+0x80)));
                owners[player]=name;
            }
            if(found.Count!=info.Players.Count){Status="재생 중인 플레이어와 선택한 리플레이가 다릅니다";return null;}
            foreach(var p in info.Players){p.Color=colors[p.Name];if(found[p.Name].Faction!=null)p.Faction=found[p.Name].Faction;}
            Status="LIVE · 게임 자금 직접 수집";cached=new Telemetry {Replay=Path.GetFileName(replay),CapturedUtc=DateTime.UtcNow.ToString("o"),GameTime=GameTime(),Selection=Selection(owners,found),Players=found.Values.OrderBy(p=>p.Slot).ToList()};return cached;
        }catch(Exception ex){Status="자원 연결 중단 · "+ex.Message;return null;}
    }
}
sealed class CursorFence : IDisposable {
    [DllImport("user32.dll")]static extern bool ClipCursor(ref Native.Rect rect);
    [DllImport("user32.dll",EntryPoint="ClipCursor")]static extern bool ClearClip(IntPtr rect);
    bool owns;
    public void Update(Rectangle bounds,bool active){if(!active||bounds.Width<200){Dispose();return;}var r=new Native.Rect {L=bounds.Left,T=bounds.Top,R=bounds.Right,B=bounds.Bottom};owns=ClipCursor(ref r)||owns;}
    public void Dispose(){if(owns){ClearClip(IntPtr.Zero);owns=false;}}
}
}
