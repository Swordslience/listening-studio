using System;using System.IO;using System.Diagnostics;using System.Windows.Forms;using ListeningStudio;
class UpdateHelper {
 [STAThread]static void Main(string[] args){try{if(args.Length!=6||args[0]!="apply")throw new Exception("请在听力软件内点击“检查更新”。");string home=Path.GetFullPath(args[1]),stage=Path.GetFullPath(args[2]),targetName=args[3],project=args[5];int pid=int.Parse(args[4]);
  if(pid<=0)throw new Exception("原程序进程无效。");
  // Only wait for the exact application that requested this update; never terminate it.
  try{using(var parent=Process.GetProcessById(pid)){if(!string.Equals(parent.MainModule.FileName,Path.Combine(home,targetName),StringComparison.OrdinalIgnoreCase))throw new Exception("原程序路径不符。");if(!parent.WaitForExit(60000))throw new Exception("请先关闭原程序，稍后重新更新。");}}catch(ArgumentException){}
  string backup=UpdateCore.Apply(home,stage,targetName,null);
  var start=new ProcessStartInfo(Path.Combine(home,targetName)){UseShellExecute=false,WorkingDirectory=home};if(!string.IsNullOrEmpty(project)&&File.Exists(project)&&project.EndsWith(".json",StringComparison.OrdinalIgnoreCase))start.Arguments=UpdateCore.QuoteArgument(project);Process.Start(start);
 }catch(Exception ex){MessageBox.Show("更新未完成："+ex.Message+"\r\n原配置和材料保留。请重新打开软件；若文件被占用，关闭其他同目录窗口后重试。","听力软件更新",MessageBoxButtons.OK,MessageBoxIcon.Information);Environment.ExitCode=1;}}
 static string Quote(string value){return "\""+value.Replace("\"","")+"\"";}
}
