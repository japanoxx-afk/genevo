using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Genevo {
static class ModMetadata {
    static System.Collections.Concurrent.ConcurrentDictionary<string,string> cache=new System.Collections.Concurrent.ConcurrentDictionary<string,string>();
    public static string Faction(string name){switch(FactionIcons.AssetName(name)){
        case "FactionAmerica":return "미국";case "FactionAmericaAirforce":return "미국 공군";case "FactionAmericaLaser":return "미국 레이저";case "FactionAmericaSupW":return "미국 슈퍼무기";
        case "FactionChina":return "중국";case "FactionChinaInfantry":return "중국 보병";case "FactionChinaNuke":return "중국 핵";case "FactionChinaTank":return "중국 탱크";
        case "FactionGLA":return "GLA";case "FactionGLAToxin":return "GLA 독소";case "FactionGLADemo":return "GLA 폭파";case "FactionGLAStealth":return "GLA 스텔스";
        default:return Regex.Replace(name.Replace("Faction",""),"([a-z])([A-Z])","$1 $2");}}
    static uint BE(BinaryReader r){var b=r.ReadBytes(4);if(b.Length!=4)throw new EndOfStreamException();return (uint)(b[0]<<24|b[1]<<16|b[2]<<8|b[3]);}
    static byte[] Unpack(byte[] b){if(b.Length<13||Encoding.ASCII.GetString(b,0,4)!="EAR\0")return b;if(b[8]!=0x10||b[9]!=0xfb)throw new InvalidDataException("Unsupported map compression");int size=b[10]<<16|b[11]<<8|b[12];if(size<4||size>64*1024*1024)throw new InvalidDataException();var output=new byte[size];int p=13,q=0;
        while(p<b.Length){int c=b[p++],lit,len=0,off=0;if(c<0x80){int d=b[p++];lit=c&3;len=((c&0x1c)>>2)+3;off=((c&0x60)<<3)+d+1;}else if(c<0xc0){int d=b[p++],e=b[p++];lit=d>>6;len=(c&0x3f)+4;off=((d&0x3f)<<8)+e+1;}else if(c<0xe0){int d=b[p++],e=b[p++],f=b[p++];lit=c&3;len=((c&0xc)<<6)+f+5;off=((c&0x10)<<12)+(d<<8)+e+1;}else if(c<0xfc){lit=((c&0x1f)<<2)+4;}else{lit=c&3;Array.Copy(b,p,output,q,lit);q+=lit;break;}
            if(p+lit>b.Length||q+lit+len>size)throw new InvalidDataException();Array.Copy(b,p,output,q,lit);p+=lit;q+=lit;if(len>0&&off>q)throw new InvalidDataException();for(int i=0;i<len;i++){output[q]=output[q-off];q++;}}
        if(q!=size)throw new InvalidDataException("Map size mismatch");return output;
    }
    public static string ParseMap(byte[] packed){byte[] b=Unpack(packed);using(var r=new BinaryReader(new MemoryStream(b))){if(Encoding.ASCII.GetString(r.ReadBytes(4))!="CkMp")throw new InvalidDataException();int count=r.ReadInt32();if(count<0||count>10000)throw new InvalidDataException();var names=new Dictionary<uint,string>();for(int i=0;i<count;i++){int len=r.ReadByte();string name=Encoding.ASCII.GetString(r.ReadBytes(len));names[r.ReadUInt32()]=name;}
        while(r.BaseStream.Position+10<=b.Length){uint id=r.ReadUInt32();r.ReadUInt16();int size=r.ReadInt32();long end=r.BaseStream.Position+size;if(size<0||end>b.Length)throw new InvalidDataException();string label;if(names.TryGetValue(id,out label)&&label=="WorldInfo"){
            int properties=r.ReadUInt16();for(int n=0;n<properties;n++){uint key=r.ReadUInt32();int type=(int)(key&255);uint prop=key>>8;string value=null;if(type==3||type==5){int length=r.ReadUInt16();value=Encoding.UTF8.GetString(r.ReadBytes(length));}else if(type==1||type==2)r.ReadUInt32();else if(type==0)r.ReadByte();else break;if(value!=null&&names.TryGetValue(prop,out label)&&label=="mapName")return value;}
        }r.BaseStream.Position=end;}}
        return null;
    }
    public static string Map(string root,string raw){string key=root+"|"+raw,cached;if(cache.TryGetValue(key,out cached))return cached;
        string relative=Regex.Replace(raw.Replace('\\','/'),"^[0-9a-fA-F]{3}(?=data/)","");string target=relative.TrimEnd('/')+"/"+relative.Split('/').Last()+".map";string result=relative.Split('/').Last();
        try{foreach(string path in Directory.GetFiles(root,"*.big")){using(var r=new BinaryReader(File.OpenRead(path))){string magic=Encoding.ASCII.GetString(r.ReadBytes(4));if(magic!="BIG4"&&magic!="BIGF")continue;r.ReadUInt32();uint count=BE(r);BE(r);if(count>100000)continue;for(int i=0;i<count;i++){uint offset=BE(r),size=BE(r);var bytes=new System.Collections.Generic.List<byte>();byte c;while((c=r.ReadByte())!=0){if(bytes.Count>2048)throw new InvalidDataException();bytes.Add(c);}string name=Encoding.ASCII.GetString(bytes.ToArray()).Replace('\\','/');if(!String.Equals(name,target,StringComparison.OrdinalIgnoreCase))continue;if(size>64*1024*1024||(long)offset+size>r.BaseStream.Length)throw new InvalidDataException();r.BaseStream.Position=offset;result=ParseMap(r.ReadBytes((int)size))??result;cache[key]=result;return result;}}}}
        catch(Exception ex){Program.Log(ex);}cache[key]=result;return result;
    }
}
}
