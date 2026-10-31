using System;using System.Collections.Generic;using System.Linq;using System.Text;using System.Text.RegularExpressions;
namespace ListeningStudio {
// Local post-processing only. Never estimates word times from text length or calls a service.
public static class SegmentRefiner {
 public const int Version=1;
 class Token {public Word word;public int begin,finish;public string norm;}
 static bool Han(char c){return c>='\u4e00'&&c<='\u9fff';}
 static string Normalize(string s){return new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());}
 static int Language(string s){return s.Any(c=>c<128&&char.IsLetter(c))?1:s.Any(Han)?2:0;}
 static List<Token> Align(Cue cue){
  string normalized=Normalize(cue.text);var positions=new List<int>();for(int i=0;i<cue.text.Length;i++)if(char.IsLetterOrDigit(cue.text[i]))positions.Add(i);
  var tokens=new List<Token>();int cursor=0;double last=-1;
  foreach(var w in cue.words){if(double.IsNaN(w.start)||double.IsNaN(w.end)||w.start<0||w.end<=w.start||string.IsNullOrWhiteSpace(w.text))continue;
   string n=Normalize(w.text);if(n.Length==0)continue;int at=normalized.IndexOf(n,cursor,StringComparison.Ordinal);if(at!=cursor||at+n.Length>positions.Count||w.start<last-.03)return null;
   tokens.Add(new Token{word=w,norm=n,begin=positions[at],finish=positions[at+n.Length-1]+1});cursor=at+n.Length;last=w.start;
  }
  return tokens.Count>0&&cursor==normalized.Length?tokens:null;
 }
 static bool SentenceEnd(string text){
  text=text.Trim();if(!Regex.IsMatch(text,@"[.!?。！？][""'”’\)\]]*$"))return false;
  if(Regex.IsMatch(text,@"\b(?:Mr|Mrs|Ms|Dr|Prof|St|vs|etc)\.$",RegexOptions.IgnoreCase)||Regex.IsMatch(text,@"\b(?:[A-Za-z]\.){1,4}$"))return false;
  return true;
 }
 static bool QuietGap(Lesson lesson,double from,double to){
  if(to-from<.60)return false;if(lesson.peaks==null||lesson.peaks.Count==0||lesson.peakStep<=0)return true;
  int a=Math.Max(0,(int)Math.Ceiling((from+.04)/lesson.peakStep)),b=Math.Min(lesson.peaks.Count-1,(int)Math.Floor((to-.04)/lesson.peakStep));
  int silent=0;for(int i=a;i<=b;i++){if(lesson.peaks[i]<650)silent++;else silent=0;if(silent*lesson.peakStep>=.15)return true;}return false;
 }
 static List<Cue> Split(Cue source,List<Token> tokens,SortedSet<int> cuts){
  cuts.Add(0);cuts.Add(tokens.Count);var result=new List<Cue>();var edges=cuts.ToArray();
  for(int k=0;k+1<edges.Length;k++){int a=edges[k],b=edges[k+1];int begin=a==0?0:tokens[a].begin,finish=b==tokens.Count?source.text.Length:tokens[b].begin;
   var c=new Cue{text=source.text.Substring(begin,finish-begin).Trim(),start=tokens[a].word.start,end=tokens[b-1].word.end,note=source.note,words=tokens.Skip(a).Take(b-a).Select(t=>new Word{start=t.word.start,end=t.word.end,text=t.word.text}).ToList()};
   if(c.text.Length>0&&c.end>c.start)result.Add(c);
  }return result;
 }
 public static bool SameSpokenSentence(string a,string b){string x=Normalize(a),y=Normalize(b);if(x==y)return true;if(Math.Min(x.Length,y.Length)<20||Math.Min(x.Length,y.Length)<Math.Max(x.Length,y.Length)*.85)return false;int[] previous=Enumerable.Range(0,y.Length+1).ToArray();for(int i=1;i<=x.Length;i++){int[] current=new int[y.Length+1];current[0]=i;for(int j=1;j<=y.Length;j++)current[j]=Math.Min(Math.Min(previous[j]+1,current[j-1]+1),previous[j-1]+(x[i-1]==y[j-1]?0:1));previous=current;}return previous[y.Length]<=Math.Max(x.Length,y.Length)*.10;}
 public static int Refine(Lesson lesson){
  int before=lesson.cues.Count;var first=new List<Cue>();
  foreach(Cue source in lesson.cues){var tokens=Align(source);if(tokens==null){if(source.end-source.start>15)source.note="词级时间无法完整对应文本，保留原段，请人工核对。";first.Add(source);continue;}
   var cuts=new SortedSet<int>();int lastLanguage=Language(tokens[0].word.text);bool hasEnglish=Regex.Matches(source.text,@"[A-Za-z]").Count>=12;for(int i=1;i<tokens.Count;i++){
    string between=source.text.Substring(tokens[i-1].finish,tokens[i].begin-tokens[i-1].finish);
    int nextLanguage=Language(tokens[i].word.text);bool language=hasEnglish&&lastLanguage>0&&nextLanguage>0&&lastLanguage!=nextLanguage;if(nextLanguage>0)lastLanguage=nextLanguage;
    bool end=SentenceEnd(tokens[i-1].word.text+between);
    double gap=tokens[i].word.start-tokens[i-1].word.end;
    if(language||end||gap>=.65&&QuietGap(lesson,tokens[i-1].word.end,tokens[i].word.start))cuts.Add(i);
   }
   if(cuts.Count==0){first.Add(source);continue;}first.AddRange(Split(source,tokens,cuts));
  }
  // A clean occurrence of the same spoken sentence can supply a boundary, not new text/times.
  var templates=first.Where(c=>!Data.Instruction(c)&&!c.text.Any(Han)&&c.end-c.start<=15&&SentenceEnd(c.text)).Select(c=>new{cue=c,norm=Normalize(c.text)}).Where(t=>t.norm.Length>=12).OrderByDescending(t=>t.norm.Length).ToList();
  var refined=new List<Cue>();foreach(Cue source in first){var tokens=Align(source);if(tokens==null||tokens.Count<4||source.text.Any(Han)){refined.Add(source);continue;}string all=Normalize(source.text);var starts=new Dictionary<int,int>();int offset=0;for(int i=0;i<tokens.Count;i++){starts[offset]=i;offset+=tokens[i].norm.Length;}starts[offset]=tokens.Count;var cuts=new SortedSet<int>();
   foreach(var template in templates){if(template.norm.Length>=all.Length)continue;int scan=0;while(scan<all.Length){int at=all.IndexOf(template.norm,scan,StringComparison.Ordinal);if(at<0)break;int a,b;if(starts.TryGetValue(at,out a)&&starts.TryGetValue(at+template.norm.Length,out b)&&b>a&&b-a>=3){double length=tokens[b-1].word.end-tokens[a].word.start,reference=template.cue.end-template.cue.start;if(length<=reference*1.6+.5&&length>=reference*.55){cuts.Add(a);cuts.Add(b);}}scan=at+template.norm.Length;}
   }
   if(cuts.Any(c=>c>0&&c<tokens.Count))refined.AddRange(Split(source,tokens,cuts));else refined.Add(source);
  }
  lesson.cues=refined.OrderBy(c=>c.start).ToList();for(int i=0;i<lesson.cues.Count;i++){var c=lesson.cues[i];c.id="r"+(i+1);if(c.end-c.start>18&&string.IsNullOrEmpty(c.note))c.note="较长片段，请试听确认是否需要进一步分句。";}
  for(int i=1;i<lesson.cues.Count;i++){Cue a=lesson.cues[i-1],b=lesson.cues[i];if(a.end>b.start+.005){string warning="自动边界有重叠，请试听核对。";if(!a.note.Contains(warning))a.note+=(a.note.Length>0?" ":"")+warning;if(!b.note.Contains(warning))b.note+=(b.note.Length>0?" ":"")+warning;}}
  lesson.segmentationVersion=Version;return lesson.cues.Count-before;
 }
}
}
