using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using System.Net.Http;using System.Threading;using System.Threading.Tasks;
namespace ListeningStudio {
 public class TosUploadRecord {public string object_key="",fingerprint="",bucket="",region="",state="UPLOADING";public bool cleaned=false;}
 public static class TosSigner {
  static byte[] Hmac(byte[] key,string value){using(var h=new HMACSHA256(key))return h.ComputeHash(Encoding.UTF8.GetBytes(value));}
  static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
  static string Hash(string text){using(var h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(text)));}
  public static string Encode(string text){return Uri.EscapeDataString(text).Replace("%7E","~");}
  public static string Sign(string method,string host,string region,string objectKey,string accessKey,string secretKey,DateTime now,int expires){
   if(expires<1||expires>604800)throw new Exception("临时链接有效期不合法。");
   string date=now.ToUniversalTime().ToString("yyyyMMdd"),timestamp=now.ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),scope=date+"/"+region+"/s3/aws4_request";
   var query=new SortedDictionary<string,string>(StringComparer.Ordinal){{"X-Amz-Algorithm","AWS4-HMAC-SHA256"},{"X-Amz-Credential",accessKey+"/"+scope},{"X-Amz-Date",timestamp},{"X-Amz-Expires",expires.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"X-Amz-SignedHeaders","host"}};
   string path="/"+string.Join("/",objectKey.Split('/').Select(Encode));string canonicalQuery=string.Join("&",query.Select(p=>Encode(p.Key)+"="+Encode(p.Value)));
   string canonical=method+"\n"+path+"\n"+canonicalQuery+"\nhost:"+host.ToLowerInvariant()+"\n\nhost\nUNSIGNED-PAYLOAD";
   byte[] signing=Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4"+secretKey),date),region),"s3"),"aws4_request");
   string signature=Hex(Hmac(signing,"AWS4-HMAC-SHA256\n"+timestamp+"\n"+scope+"\n"+Hash(canonical)));
   return "https://"+host+path+"?"+canonicalQuery+"&X-Amz-Signature="+signature;
  }
 }
 public sealed class TosStorage {
  readonly Config config;readonly HttpClient http;
  public TosStorage(Config settings,HttpClient client){config=settings;http=client;}
  public static void Validate(Config c){
   if(!System.Text.RegularExpressions.Regex.IsMatch(c.tosBucket??"",@"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$"))throw new Exception("请先在首次配置中填写TOS私有桶名称（不是语音App ID）。");
   if(!new[]{"cn-beijing","cn-shanghai","cn-guangzhou","cn-hongkong","ap-southeast-1","ap-southeast-3"}.Contains(c.tosRegion))throw new Exception("请选择已创建存储桶的正确地域。");
   if(string.IsNullOrWhiteSpace(c.tosAccessKey)||string.IsNullOrWhiteSpace(c.tosSecretKey))throw new Exception("尚未配置TOS上传授权。请在存储设置填写专用AccessKey ID/Secret；它与豆包语音Key不同。");
  }
  string Url(string method,string key,int expires){Validate(config);return TosSigner.Sign(method,config.tosBucket+".tos-s3-"+config.tosRegion+".volces.com",config.tosRegion,key,config.tosAccessKey,config.tosSecretKey,DateTime.UtcNow,expires);}
  public async Task<string> Upload(string audio,string recordFile,Action<string> progress,CancellationToken cancel){
   Validate(config);FileInfo file=new FileInfo(audio);if(file.Length<=0||file.Length>512000000)throw new Exception("音频须非空且不超过512MB。");string fingerprint=Studio.Fingerprint(audio);
   TosUploadRecord record=File.Exists(recordFile)?Data.Load<TosUploadRecord>(recordFile):new TosUploadRecord{object_key="listening-studio/"+Guid.NewGuid().ToString("N")+Path.GetExtension(audio).ToLowerInvariant(),fingerprint=fingerprint,bucket=config.tosBucket,region=config.tosRegion};
   if(!System.Text.RegularExpressions.Regex.IsMatch(record.object_key??"",@"^listening-studio/[a-f0-9]{32}\.[a-z0-9]+$")||record.fingerprint!=fingerprint||record.bucket!=config.tosBucket||record.region!=config.tosRegion)throw new Exception("音频或TOS配置已改变，不能覆盖旧上传记录。请创建新材料。");
   Data.Save(recordFile,record);
   if(record.state!="UPLOADED"){
    progress("正在上传完整音频到火山TOS私有桶…");
    using(var stream=File.OpenRead(audio))using(var request=new HttpRequestMessage(HttpMethod.Put,Url("PUT",record.object_key,3600))){request.Content=new StreamContent(stream);request.Content.Headers.ContentLength=stream.Length;using(var response=await http.SendAsync(request,cancel)){if(!response.IsSuccessStatusCode)throw new Exception("TOS上传失败 HTTP "+(int)response.StatusCode+"。请核对私有桶、地域和专用上传授权；尚未提交豆包识别。");}}
    record.state="UPLOADED";Data.Save(recordFile,record);
   }else progress("复用已上传的同一音频，不重复上传…");
   return Url("GET",record.object_key,86400);
  }
  public async Task Cleanup(string recordFile,CancellationToken cancel){
   if(!File.Exists(recordFile))return;var record=Data.Load<TosUploadRecord>(recordFile);if(record.cleaned)return;
   if(record.bucket!=config.tosBucket||record.region!=config.tosRegion||!System.Text.RegularExpressions.Regex.IsMatch(record.object_key??"",@"^listening-studio/[a-f0-9]{32}\.[a-z0-9]+$"))throw new Exception("清理范围校验失败，未删除云端对象。");
   using(var response=await http.DeleteAsync(Url("DELETE",record.object_key,900),cancel)){if(!response.IsSuccessStatusCode&&response.StatusCode!=System.Net.HttpStatusCode.NotFound)throw new Exception("临时音频未清理成功，可在TOS的listening-studio目录核对。");}
   record.cleaned=true;Data.Save(recordFile,record);
  }
 }
 public static class DoubaoWorkflow {
  public static async Task<object> Process(string audio,Config config,string taskFile,Action<string> progress,CancellationToken cancel){
   string folder=Path.GetDirectoryName(taskFile),marker=Path.Combine(folder,"恢复云任务.txt"),uploadRecord=taskFile+".upload.json",resultFile=taskFile+".result.json";
   bool resume=File.Exists(taskFile)||File.Exists(resultFile);if(File.Exists(uploadRecord)&&Data.Load<TosUploadRecord>(uploadRecord).fingerprint!=Studio.Fingerprint(audio))throw new Exception("本地音频内容已改变，不能套用旧识别任务，请重新建立材料。");if(!resume){DoubaoStandardClient.ValidateFormat(Path.GetExtension(audio).TrimStart('.').ToLowerInvariant());TosStorage.Validate(config);if(string.IsNullOrWhiteSpace(config.key))throw new Exception("请填写豆包语音API Key。");}
   var storage=new TosStorage(config,Clouds.Http);string url=null;
   if(!resume){
    Directory.CreateDirectory(folder);
    // Manifest precedes marker; interrupted upload can safely replace only our own object.
    if(!File.Exists(uploadRecord)){var record=new TosUploadRecord{object_key="listening-studio/"+Guid.NewGuid().ToString("N")+Path.GetExtension(audio).ToLowerInvariant(),fingerprint=Studio.Fingerprint(audio),bucket=config.tosBucket,region=config.tosRegion};Data.Save(uploadRecord,record);}
    File.WriteAllText(marker,Path.GetFileName(taskFile));url=await storage.Upload(audio,uploadRecord,progress,cancel);
   }else File.WriteAllText(marker,Path.GetFileName(taskFile));
   object raw=await new DoubaoStandardClient(Clouds.Http).RunAsync(url,Path.GetExtension(audio).TrimStart('.').ToLowerInvariant(),config.key,config.language,taskFile,progress,cancel);
   // Persist valid recognition result before attempting optional cloud cleanup.
   Data.ParseRaw(raw,0);Data.Save(resultFile,raw);
   try{await storage.Cleanup(uploadRecord,cancel);}catch{File.WriteAllText(taskFile+".cleanup-pending.txt","识别已成功，临时音频尚未清理。请核对TOS listening-studio目录及上传授权。此记录不含密钥和下载链接。");}
   // Keep checkpoint until the caller has saved the final nonempty lesson.
   return raw;
  }
 }
}
