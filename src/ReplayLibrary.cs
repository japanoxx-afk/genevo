using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace Genevo {
sealed class ReplayEntry {public string Path,Name,Players,Map;public DateTime Modified;public bool Valid;}
static class ReplayLibrary {
    public static List<ReplayEntry> Read(string folder){var result=new List<ReplayEntry>();if(!Directory.Exists(folder))return result;foreach(var file in new DirectoryInfo(folder).GetFiles("*.RA3Replay").OrderByDescending(f=>f.LastWriteTimeUtc)){var entry=new ReplayEntry {Path=file.FullName,Name=file.Name,Modified=file.LastWriteTime,Players="",Map=""};try{var info=ReplayInfo.Read(file.FullName);entry.Players=String.Join(" vs ",info.Players.Select(p=>p.Name));entry.Map=info.Map;entry.Valid=info.Players.Count>0;}catch{entry.Players="읽을 수 없는 리플레이";}result.Add(entry);}return result;}
}
}
