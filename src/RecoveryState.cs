using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace ListeningStudio {
public static class RecoveryState {
 static bool Valid(string folder,string name){name=Path.GetFileName(name);if(string.IsNullOrWhiteSpace(name))return false;string file=Path.Combine(folder,name);if(!File.Exists(file))return File.Exists(file+".upload.json")||File.Exists(file+".result.json");try{object job=Data.Json().DeserializeObject(File.ReadAllText(file));string state=Data.Str(job,"task_status");return Data.Str(job,"task_id")!=""&&state!="FAILED"&&state!="UNKNOWN";}catch{return false;}}
 public static string Find(string folder){string marker=Path.Combine(folder,"恢复云任务.txt");if(File.Exists(marker)){string name=Path.GetFileName(File.ReadAllText(marker));if(Valid(folder,name))return name;File.Move(marker,marker+".ignored-"+DateTime.Now.Ticks);}
  string project=Path.Combine(folder,"lesson.json");try{if(File.Exists(project)&&Data.Load<Lesson>(project).cues.Count>0)return "";}catch{}
  var abandoned=new HashSet<string>(Directory.GetFiles(folder,"恢复云任务.txt.abandoned-*").Select(f=>Path.GetFileName(File.ReadAllText(f))),StringComparer.OrdinalIgnoreCase);
  foreach(string file in Directory.GetFiles(folder,"*.json").OrderByDescending(File.GetLastWriteTimeUtc)){string name=Path.GetFileName(file);if(name.EndsWith(".upload.json"))name=name.Substring(0,name.Length-12);else if(name.EndsWith(".result.json"))name=name.Substring(0,name.Length-12);if(!abandoned.Contains(name)&&Valid(folder,name)){File.WriteAllText(marker,name);return name;}}return "";
 }
 public static void Complete(string folder,string name){string marker=Path.Combine(folder,"恢复云任务.txt"),project=Path.Combine(folder,"lesson.json");if(!File.Exists(marker)||string.IsNullOrEmpty(name)||!File.Exists(project))return;Data.Validate(Data.Load<Lesson>(project));if(Data.Load<Lesson>(project).cues.Count>0&&Path.GetFileName(File.ReadAllText(marker))==Path.GetFileName(name))File.Delete(marker);}
}
}
