// Doubao recorded-file 2.0 task transport. Storage upload is deliberately separate.
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
namespace ListeningStudio {
 public class StandardAsrJob { public string provider="豆包2.0",task_id="",task_status="SUBMITTING"; }
 public sealed class DoubaoStandardClient {
  readonly HttpClient http;readonly TimeSpan delay;
  public DoubaoStandardClient(HttpClient client,TimeSpan? pollDelay=null){http=client;delay=pollDelay??TimeSpan.FromSeconds(3);}
  static string Header(HttpResponseMessage response,string name){IEnumerable<string> values;return response.Headers.TryGetValues(name,out values)?values.FirstOrDefault()??"":"";}
  async Task<Tuple<string,object>> Request(string operation,string key,string taskId,object payload,CancellationToken cancel){
   using(var message=new HttpRequestMessage(HttpMethod.Post,"https://openspeech.bytedance.com/api/v3/auc/bigmodel/"+operation)){
    message.Headers.Add("X-Api-Key",key);message.Headers.Add("X-Api-Resource-Id","volc.seedasr.auc");message.Headers.Add("X-Api-Request-Id",taskId);message.Headers.Add("X-Api-Sequence","-1");message.Content=new StringContent(Data.Dump(payload),Encoding.UTF8,"application/json");
    using(var response=await http.SendAsync(message,cancel)){
     string code=Header(response,"X-Api-Status-Code");string body=await response.Content.ReadAsStringAsync();
     if(!response.IsSuccessStatusCode)throw new Exception("豆包2.0查询或提交 HTTP "+(int)response.StatusCode+"；任务编号已保留，请核对服务权限或稍后继续查询。");
     if(string.IsNullOrEmpty(code))throw new Exception("豆包2.0响应缺少状态，任务编号已保留，未自动重新提交。");
     object raw=null;if(!string.IsNullOrWhiteSpace(body))try{raw=Data.Json().DeserializeObject(body);}catch{throw new Exception("豆包2.0响应暂时无法解析，编号已保留，请稍后继续查询。");}
     return Tuple.Create(code,raw);
    }
   }
  }
  public static void ValidateFormat(string format){if(!new[]{"mp3","wav","ogg","spx","amr","aac","m4a"}.Contains(format))throw new Exception("豆包2.0不支持此格式，请先准备MP3或WAV音频。尚未上传或提交识别。");}
  public async Task<object> RunAsync(string audioUrl,string format,string key,string language,string taskFile,Action<string> progress,CancellationToken cancel){
   string resultFile=taskFile+".result.json";
   if(File.Exists(resultFile)){object saved=Data.Json().DeserializeObject(File.ReadAllText(resultFile));Data.ParseRaw(saved,0);return saved;}
   if(string.IsNullOrWhiteSpace(key))throw new Exception("请先填写豆包语音 API Key。");
   bool existing=File.Exists(taskFile);StandardAsrJob job=existing?Data.Load<StandardAsrJob>(taskFile):new StandardAsrJob{task_id=Guid.NewGuid().ToString()};
   if(existing&&string.IsNullOrWhiteSpace(job.task_id))throw new Exception("本地任务编号缺失，请检查记录，未重新提交。");
   if(job.task_status=="FAILED")throw new Exception("该云任务已明确失败，请检查已保存记录后再创建新任务。");
   if(!existing){
    Uri uri;if(!Uri.TryCreate(audioUrl,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new Exception("上传环节尚未返回有效的HTTPS音频地址，未提交识别。");
    ValidateFormat(format);
    Directory.CreateDirectory(Path.GetDirectoryName(taskFile));Data.Save(taskFile,job);
    progress("正在提交豆包2.0任务…");
    var audio=new Dictionary<string,object>{{"url",audioUrl},{"format",format}};if(!string.IsNullOrWhiteSpace(language))audio["language"]=language=="en"?"en-US":language=="zh"?"zh-CN":language;
    var response=await Request("submit",key,job.task_id,new{audio=audio,request=new{model_name="bigmodel",show_utterances=true,enable_punc=true,enable_itn=false,enable_ddc=false}},cancel);
    if(response.Item1!="20000000"){if(response.Item1.StartsWith("45")){job.task_status="FAILED";Data.Save(taskFile,job);}throw new Exception("豆包2.0未确认提交成功，状态 "+response.Item1+"；编号已保留，未自动重新提交。");}
    job.task_status="RUNNING";Data.Save(taskFile,job);
   }
   DateTime deadline=DateTime.UtcNow.AddHours(3.5);
   while(DateTime.UtcNow<deadline){
    cancel.ThrowIfCancellationRequested();progress("正在查询豆包2.0原任务，不重复上传或提交…");
    var response=await Request("query",key,job.task_id,new{},cancel);
    if(response.Item1=="20000000"){
     Data.ParseRaw(response.Item2,0);Data.Save(resultFile,response.Item2);job.task_status="SUCCEEDED";Data.Save(taskFile,job);return response.Item2;
    }
    if(response.Item1=="20000001"||response.Item1=="20000002"){await Task.Delay(delay,cancel);continue;}
    if(response.Item1=="20000003"||new[]{"45000002","45000131","45000132","45000151"}.Contains(response.Item1)){job.task_status="FAILED";Data.Save(taskFile,job);}
    throw new Exception("豆包2.0任务状态 "+response.Item1+"。记录已保留；请检查音频或稍后恢复查询，不要重复提交。");
   }
   throw new Exception("豆包2.0等待超时，任务编号已保留，可稍后恢复查询。");
  }
 }
}
