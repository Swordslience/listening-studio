using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.IO.Compression;using System.Security.Cryptography;using System.Net;using System.Net.Http;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
namespace ListeningStudio {
 public class UpdateEnvelope {public string payload="",signature="";}
 public class UpdateRelease {public string product="",version="",url="",sha256="",notes="";public long size;}
 public static class UpdateCore {
  public const string CurrentVersion="2.4.0.0";
  public const string Feed=UpdateIdentity.Feed;
  public const long MaxBundle=20*1024*1024,MaxExpanded=50*1024*1024;
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=128*1024};}
  public static string Sha(string file){using(var s=File.OpenRead(file))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  public static string QuoteArgument(string text){string value=(text??"").Replace("\"","");int trailing=0;for(int i=value.Length-1;i>=0&&value[i]=='\\';i--)trailing++;return "\""+value+new string('\\',trailing)+"\"";}
  public static bool IsNewer(string version){Version v;if(!Version.TryParse(version,out v))throw new Exception("更新版本号无效。");return v>new Version(CurrentVersion);}
  public static UpdateRelease VerifyEnvelope(string envelope){
   if(envelope==null||envelope.Length>65536)throw new Exception("更新信息过大或为空。");
   var e=Json().Deserialize<UpdateEnvelope>(envelope);if(e==null||string.IsNullOrEmpty(e.payload)||string.IsNullOrEmpty(e.signature))throw new Exception("更新信息缺少签名。");byte[] data=Convert.FromBase64String(e.payload),signature=Convert.FromBase64String(e.signature);
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(UpdateIdentity.PublicKey);if(!rsa.VerifyData(data,CryptoConfig.MapNameToOID("SHA256"),signature))throw new Exception("更新签名不正确，已拒绝安装。");}
   var r=Json().Deserialize<UpdateRelease>(Encoding.UTF8.GetString(data));Version v;Uri uri;
   if(r==null||r.product!="ListeningStudio"||!Version.TryParse(r.version,out v)||r.size<=0||r.size>MaxBundle||!System.Text.RegularExpressions.Regex.IsMatch(r.sha256??"","^[0-9a-f]{64}$"))throw new Exception("更新信息格式无效。");
   if(!Uri.TryCreate(r.url,UriKind.Absolute,out uri)||uri.Scheme!="https"||(!string.IsNullOrWhiteSpace(Feed)&&uri.Host!=new Uri(Feed).Host)||!uri.AbsolutePath.StartsWith("/listening-studio/updates/",StringComparison.Ordinal)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.UserInfo))throw new Exception("更新地址不属于本软件的火山更新目录。");
   return r;
  }
  public static async Task<byte[]> Get(HttpClient http,string url,long limit,Action<long> progress,CancellationToken token){
   using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)){if(!response.IsSuccessStatusCode)throw new Exception("更新服务暂不可用（HTTP "+(int)response.StatusCode+"）。原程序仍可正常使用。");if(response.Content.Headers.ContentLength>limit)throw new Exception("下载文件超出限制。");using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream()){byte[] buffer=new byte[32768];int n;while((n=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0){if(output.Length+n>limit)throw new Exception("下载文件超出限制。");output.Write(buffer,0,n);if(progress!=null)progress(output.Length);}return output.ToArray();}}
  }
  public static HttpClient Client(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;return new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(90)};}
  public static async Task<string> Fetch(HttpClient http,CancellationToken token){if(string.IsNullOrWhiteSpace(Feed))throw new Exception("此公开版未配置在线更新源，请从项目发布页下载新版，或使用发布者提供的离线更新包。");return Encoding.UTF8.GetString(await Get(http,Feed,65536,null,token).ConfigureAwait(false)).TrimStart('\ufeff');}
  static void NoLinks(string directory){string full=Path.GetFullPath(directory);if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("更新路径包含文件链接。");for(var p=new DirectoryInfo(full);p!=null;p=p.Parent)if(p.Exists&&(p.Attributes&FileAttributes.ReparsePoint)!=0)throw new Exception("更新路径包含链接或重定向目录，请移到普通文件夹后更新。");}
  public static string NewStage(string home){NoLinks(home);string cache=Path.Combine(Path.GetFullPath(home),"更新缓存");NoLinks(cache);Directory.CreateDirectory(cache);string stage=Path.Combine(cache,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);return stage;}
  public static bool Allowed(string name){return new[]{"app.exe","更新助手.exe","NAudio.dll","NAudio.Core.dll","NAudio.WinMM.dll","NAudio.Wasapi.dll","NAudio.Asio.dll","NAudio.Midi.dll"}.Contains(name,StringComparer.Ordinal);}
  public static void VerifyBundle(string file,UpdateRelease release){if(new FileInfo(file).Length!=release.size||Sha(file)!=release.sha256)throw new Exception("更新文件不完整或校验失败，已保留原程序。");}
  public static void ExtractBundle(string file,string destination){
   NoLinks(destination);Directory.CreateDirectory(destination);using(var z=ZipFile.OpenRead(file)){long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var e in z.Entries){if(!Allowed(e.FullName)||!seen.Add(e.FullName))throw new Exception("更新包含不允许替换的文件或重复项。");total+=e.Length;if(e.Length<=0||total>MaxExpanded)throw new Exception("更新包展开大小无效。");}if(!seen.Contains("app.exe")||!seen.Contains("更新助手.exe"))throw new Exception("更新包缺少程序文件。");foreach(var e in z.Entries)e.ExtractToFile(Path.Combine(destination,e.FullName),false);}
  }
  public static string Prepare(string home,string envelope,string bundle){var r=VerifyEnvelope(envelope);VerifyBundle(bundle,r);string stage=NewStage(home);File.WriteAllText(Path.Combine(stage,"release.json"),envelope,new UTF8Encoding(false));File.Copy(bundle,Path.Combine(stage,"bundle.zip"));ExtractBundle(Path.Combine(stage,"bundle.zip"),Path.Combine(stage,"files"));return stage;}
  public static string ImportOffline(string home,string package){
   if(new FileInfo(package).Length>MaxBundle+65536)throw new Exception("离线更新包过大。");string stage=NewStage(home);using(var z=ZipFile.OpenRead(package)){if(z.Entries.Count!=2||z.Entries.Count(e=>e.FullName=="release.json")!=1||z.Entries.Count(e=>e.FullName=="bundle.zip")!=1)throw new Exception("请选择本软件的 .lsupdate 更新包。");var manifest=z.GetEntry("release.json");if(manifest.Length>65536)throw new Exception("版本信息过大。");string text;using(var reader=new StreamReader(manifest.Open(),Encoding.UTF8))text=reader.ReadToEnd();var release=VerifyEnvelope(text);var bundle=z.GetEntry("bundle.zip");if(bundle.Length!=release.size)throw new Exception("离线更新文件大小不符。");File.WriteAllText(Path.Combine(stage,"release.json"),text,new UTF8Encoding(false));bundle.ExtractToFile(Path.Combine(stage,"bundle.zip"));VerifyBundle(Path.Combine(stage,"bundle.zip"),release);ExtractBundle(Path.Combine(stage,"bundle.zip"),Path.Combine(stage,"files"));}return stage;
  }
  public static string Apply(string home,string stage,string targetName,Action<string> beforeReplace){
   home=Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar);stage=Path.GetFullPath(stage);NoLinks(home);NoLinks(stage);string cache=Path.Combine(home,"更新缓存")+Path.DirectorySeparatorChar;
   if(!stage.StartsWith(cache,StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(stage.Substring(cache.Length),"^[a-f0-9]{32}$")||!new[]{"英语听力工作台.exe","课堂播放器.exe"}.Contains(targetName))throw new Exception("更新目标路径无效。");
   var release=VerifyEnvelope(File.ReadAllText(Path.Combine(stage,"release.json"),Encoding.UTF8));if(new Version(release.version)<new Version(CurrentVersion))throw new Exception("拒绝使用旧版更新包覆盖当前更新程序。");string bundle=Path.Combine(stage,"bundle.zip");VerifyBundle(bundle,release);
   // Always re-extract verified bytes; never trust files left in a writable staging folder.
   string files=Path.Combine(stage,"verified-"+Guid.NewGuid().ToString("N"));ExtractBundle(bundle,files);
   string backup=Path.Combine(home,"更新备份",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));NoLinks(Path.GetDirectoryName(backup));Directory.CreateDirectory(backup);
   // Detect another running instance, read-only targets, and file locks before touching anything.
   foreach(string src in Directory.GetFiles(files)){string name=Path.GetFileName(src)=="app.exe"?targetName:Path.GetFileName(src),dst=Path.Combine(home,name);if(File.Exists(dst)&&Sha(dst)!=Sha(src)){NoLinks(dst);using(var locked=new FileStream(dst,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){} }}
   var changed=new List<string>();var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);try{
    foreach(string src in Directory.GetFiles(files)){string name=Path.GetFileName(src)=="app.exe"?targetName:Path.GetFileName(src),dst=Path.Combine(home,name);if(File.Exists(dst)&&Sha(dst)==Sha(src))continue;if(beforeReplace!=null)beforeReplace(name);if(File.Exists(dst)){NoLinks(dst);File.Copy(dst,Path.Combine(backup,name));existed.Add(name);}string temp=Path.Combine(home,name+".update-"+Guid.NewGuid().ToString("N"));File.Copy(src,temp);try{if(File.Exists(dst))File.Replace(temp,dst,null);else File.Move(temp,dst);changed.Add(name);}finally{if(File.Exists(temp))File.Delete(temp);}}
    File.WriteAllText(Path.Combine(backup,"更新结果.txt"),"已更新到 "+release.version+"；仅替换程序组件，配置和材料未改动。\r\n"+string.Join("\r\n",changed),Encoding.UTF8);return backup;
   }catch{foreach(string name in changed.AsEnumerable().Reverse()){string dst=Path.Combine(home,name);if(existed.Contains(name))File.Copy(Path.Combine(backup,name),dst,true);else if(File.Exists(dst))File.Delete(dst);}throw;}
  }
 }
}
