using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Collections.Generic;
namespace Genevo {
// Read the installed mod's original artwork; no game assets are bundled with the launcher.
static class FactionIcons {
    sealed class Assets {public Bitmap Atlas;public Dictionary<string,Rectangle> Regions=new Dictionary<string,Rectangle>();}
    sealed class Entry {public string File;public long Offset,Length;}
    static readonly Dictionary<string,Assets> cache=new Dictionary<string,Assets>(StringComparer.OrdinalIgnoreCase);
    public static string Kind(string faction){string s=(faction??"").ToLowerInvariant();if(s.Contains("미국")||s.Contains("america")||s.Contains("usa"))return "USA";if(s.Contains("중국")||s.Contains("china"))return "CHINA";if(s.Contains("gla"))return "GLA";return "UNKNOWN";}
    public static string AssetName(string faction){string s=(faction??"").ToLowerInvariant(),kind=Kind(faction);
        if(kind=="USA"){if(s.Contains("공군")||s.Contains("airforce"))return "FactionAmericaAirforce";if(s.Contains("레이저")||s.Contains("laser")||s.Contains("lazr"))return "FactionAmericaLaser";if(s.Contains("슈퍼")||s.Contains("supw")||s.Contains("superweapon"))return "FactionAmericaSupW";return "FactionAmerica";}
        if(kind=="CHINA"){if(s.Contains("보병")||s.Contains("infantry"))return "FactionChinaInfantry";if(s.Contains("핵")||s.Contains("nuke")||s.Contains("nuclear"))return "FactionChinaNuke";if(s.Contains("탱크")||s.Contains("전차")||s.Contains("tank"))return "FactionChinaTank";return "FactionChina";}
        if(kind=="GLA"){if(s.Contains("독소")||s.Contains("toxin")||s.Contains("chem"))return "FactionGLAToxin";if(s.Contains("폭파")||s.Contains("demo"))return "FactionGLADemo";if(s.Contains("스텔스")||s.Contains("stealth"))return "FactionGLAStealth";return "FactionGLA";}
        return "";
    }
    static uint U(byte[] b,int p){if(p<0||p>b.Length-4)throw new InvalidDataException("Icon asset range");return BitConverter.ToUInt32(b,p);}
    static uint BE(BinaryReader r){var b=r.ReadBytes(4);if(b.Length!=4)throw new EndOfStreamException();return (uint)(b[0]<<24|b[1]<<16|b[2]<<8|b[3]);}
    static byte[] Read(Entry e,long offset,int length){if(offset<0||length<0||offset+length>e.Length)throw new InvalidDataException("Icon archive range");using(var f=File.OpenRead(e.File)){f.Position=e.Offset+offset;var bytes=new byte[length];int n=0;while(n<length){int got=f.Read(bytes,n,length-n);if(got==0)throw new EndOfStreamException();n+=got;}return bytes;}}
    static Dictionary<string,Entry> Find(string root){var result=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);foreach(string path in Directory.GetFiles(root,"*.big"))using(var r=new BinaryReader(File.OpenRead(path))){string magic=Encoding.ASCII.GetString(r.ReadBytes(4));if(magic!="BIG4"&&magic!="BIGF")continue;r.ReadUInt32();uint count=BE(r);BE(r);if(count>100000)throw new InvalidDataException("Icon archive count");for(uint n=0;n<count;n++){uint offset=BE(r),size=BE(r);var name=new List<byte>();byte c;while((c=r.ReadByte())!=0){if(name.Count>2048)throw new InvalidDataException();name.Add(c);}string key=Encoding.ASCII.GetString(name.ToArray()).Replace('\\','/');if(key!="data/mod.manifest"&&key!="data/mod.bin")continue;if((long)offset+size>r.BaseStream.Length)throw new InvalidDataException();result[key]=new Entry {File=path,Offset=offset,Length=size};}}return result;}
    static Assets Load(string root){var files=Find(root);Entry manifest,bin;if(!files.TryGetValue("data/mod.manifest",out manifest)||!files.TryGetValue("data/mod.bin",out bin))throw new FileNotFoundException("진영 아이콘 스트림이 없습니다.");if(manifest.Length>32*1024*1024)throw new InvalidDataException();byte[] m=Read(manifest,0,(int)manifest.Length);int count=checked((int)U(m,12));if(m.Length<48||m[0]!=0||m[2]!=6||count>100000||U(Read(bin,0,4),0)!=U(m,4))throw new InvalidDataException("Unsupported icon manifest");
        int names=checked(48+count*48+(int)U(m,32)+(int)U(m,36));int nameEnd=checked(names+(int)U(m,40));if(nameEnd>m.Length)throw new InvalidDataException();long position=4;var result=new Assets();byte[] texture=null;
        for(int i=0;i<count;i++){int record=48+i*48,start=checked(names+(int)U(m,record+24));if(start<names||start>=nameEnd)throw new InvalidDataException();int end=Array.IndexOf(m,(byte)0,start,nameEnd-start);if(end<0)throw new InvalidDataException();string name=Encoding.ASCII.GetString(m,start,end-start);int size=checked((int)U(m,record+32));if(position+size>bin.Length)throw new InvalidDataException();
            if(name=="Texture:GenEvo_FactionIcons"){if(size>8*1024*1024)throw new InvalidDataException();texture=Read(bin,position,size);}
            else if(name.StartsWith("PackedTextureImage:Faction",StringComparison.Ordinal)){if(size!=36)throw new InvalidDataException();byte[] d=Read(bin,position,size);var region=new Rectangle(checked((int)U(d,16)),checked((int)U(d,20)),checked((int)U(d,8)),checked((int)U(d,12)));result.Regions[name.Substring("PackedTextureImage:".Length)]=region;}
            position+=size;
        }
        if(texture==null)throw new InvalidDataException("Faction texture missing");result.Atlas=DecodeDxt5(texture,12);foreach(var region in result.Regions.Values)if(region.Width<1||region.Height<1||!new Rectangle(Point.Empty,result.Atlas.Size).Contains(region))throw new InvalidDataException("Faction icon rectangle");return result;
    }
    // SAGE's embedded DDS uses standard BC3/DXT5 blocks, including an independent alpha palette.
    internal static Bitmap DecodeDxt5(byte[] d,int offset){if(U(d,offset)!=0x20534444||U(d,offset+4)!=124||U(d,offset+84)!=0x35545844)throw new InvalidDataException("Expected DXT5 DDS");int h=checked((int)U(d,offset+12)),w=checked((int)U(d,offset+16));if(w<1||h<1||w>4096||h>4096||offset+128+((w+3)/4)*((h+3)/4)*16>d.Length)throw new InvalidDataException("DDS dimensions");var pixels=new byte[w*h*4];int p=offset+128;
        for(int by=0;by<h;by+=4)for(int bx=0;bx<w;bx+=4,p+=16){var alpha=new byte[8];alpha[0]=d[p];alpha[1]=d[p+1];if(alpha[0]>alpha[1])for(int k=1;k<=6;k++)alpha[k+1]=(byte)(((7-k)*alpha[0]+k*alpha[1])/7);else{for(int k=1;k<=4;k++)alpha[k+1]=(byte)(((5-k)*alpha[0]+k*alpha[1])/5);alpha[6]=0;alpha[7]=255;}ulong abits=0;for(int k=0;k<6;k++)abits|=(ulong)d[p+2+k]<<(8*k);int a=d[p+8]|d[p+9]<<8,b=d[p+10]|d[p+11]<<8;var color=new int[4,3];for(int k=0;k<2;k++){int v=k==0?a:b;color[k,0]=((v>>11)&31)*255/31;color[k,1]=((v>>5)&63)*255/63;color[k,2]=(v&31)*255/31;}for(int k=0;k<3;k++){color[2,k]=(2*color[0,k]+color[1,k])/3;color[3,k]=(color[0,k]+2*color[1,k])/3;}uint bits=U(d,p+12);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++){int index=y*4+x;if(bx+x>=w||by+y>=h)continue;int c=(int)((bits>>(index*2))&3),q=((by+y)*w+bx+x)*4;pixels[q]=(byte)color[c,2];pixels[q+1]=(byte)color[c,1];pixels[q+2]=(byte)color[c,0];pixels[q+3]=alpha[(int)((abits>>(index*3))&7)];}}
        var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);var data=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*4,IntPtr.Add(data.Scan0,y*data.Stride),w*4);}finally{bitmap.UnlockBits(data);}return bitmap;
    }
    public static bool Available(string root){return Get(root).Atlas!=null;}
    static Assets Get(string root){root=root??"";Assets assets;if(cache.TryGetValue(root,out assets))return assets;try{assets=Load(root);}catch(Exception ex){Program.Log(ex);assets=new Assets();}cache[root]=assets;return assets;}
    public static bool HasIcon(string root,string faction){var assets=Get(root);return assets.Atlas!=null&&assets.Regions.ContainsKey(AssetName(faction));}
    public static void Draw(Graphics g,Rectangle box,string faction,string root){var assets=Get(root);Rectangle source;if(assets.Atlas!=null&&assets.Regions.TryGetValue(AssetName(faction),out source)){var state=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(assets.Atlas,box,source,GraphicsUnit.Pixel);g.Restore(state);return;}using(var font=new Font("Segoe UI",14,FontStyle.Bold))g.DrawString("?",font,Brushes.Gray,box.X+box.Width/3,box.Y);}
}
}
