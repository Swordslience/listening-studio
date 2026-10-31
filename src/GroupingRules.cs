using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace ListeningStudio {
public static class GroupingRules {
 const string CN=@"[一二三四五六七八九十百零\d]+";
 public static List<int> QuestionNumbers(string text){
  string refs="";bool english=false;Match m=Regex.Match(text,@"回答(?<refs>.*?)(?:小题|题目|题)");if(m.Success){refs=Regex.Replace(m.Groups["refs"].Value,@"\s+","");refs=Regex.Replace(refs,@"(?:两|[一二三四五六七八九十]|\d+)(?:个|道)$","");}
  else{m=Regex.Match(text,@"\bquestions?\s+(?<refs>\d+(?:\s*(?:and|to|through|,|[-–—])\s*(?:questions?\s+)?\d+)*)",RegexOptions.IgnoreCase);if(m.Success){english=true;refs=m.Groups["refs"].Value;}}
  if(refs.Length==0)return new List<int>();string pattern=english?@"\d+":CN;var nums=Regex.Matches(refs,pattern).Cast<Match>().Select(v=>Data.ChineseNumber(v.Value)).ToList();
  if(nums.Count==0||nums.Any(n=>n<1||n>100)||nums.Zip(nums.Skip(1),(a,b)=>b<=a).Any(b=>b))return new List<int>();
  string rest=Regex.Replace(refs,pattern,"");rest=Regex.Replace(rest,english?@"\s|questions?|and|to|through|,|[-–—]":@"第|和|及|至|到|、|,|，|[-–—]","",RegexOptions.IgnoreCase);return rest.Length==0?nums:new List<int>();
 }
 public static string Label(string text,int index,out bool confirmed){var numbers=QuestionNumbers(text);confirmed=numbers.Count>0;if(!confirmed)return "材料 "+index+"（题号待确认）";if(numbers.Count==1)return "第 "+numbers[0]+" 题";Match reference=Regex.Match(text,@"回答(?<refs>.*?)(?:小题|题目|题)|questions?\s+(?<refs>\d+(?:\s*(?:and|to|through|,|[-–—])\s*\d+)*)",RegexOptions.IgnoreCase);bool range=Regex.IsMatch(reference.Groups["refs"].Value,@"至|到|[-–—]|\b(?:to|through)\b",RegexOptions.IgnoreCase)||numbers.Last()-numbers.First()+1==numbers.Count;return range?"第 "+numbers.First()+"—"+numbers.Last()+" 题（同一材料）":"第 "+string.Join("、",numbers)+" 题（同一材料）";}
 public static int FindRepeat(List<Cue> body){
  for(int k=1;k<body.Count;k++){double left=body[k-1].end-body[0].start,right=body.Last().end-body[k].start;if(left<=0||right<=0||Math.Min(left,right)<Math.Max(left,right)*.6||body[k].start-body[0].start<1.0)continue;
   string a=string.Join(" ",body.Take(k).Select(c=>c.text)),b=string.Join(" ",body.Skip(k).Select(c=>c.text));int al=Data.Norm(a).Length,bl=Data.Norm(b).Length;if(Math.Min(al,bl)<Math.Max(al,bl)*.85)continue;
   if(SegmentRefiner.SameSpokenSentence(a,b))return k;
  }return -1;
 }
}
}
