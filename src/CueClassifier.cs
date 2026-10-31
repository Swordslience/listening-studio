using System;using System.Linq;using System.Text.RegularExpressions;
namespace ListeningStudio {
// Classification is deliberately conservative: unmatched content is not an instruction.
public static class CueClassifier {
 static readonly Regex ChinesePrompt=new Regex(@"听下面.{0,40}(?:录音|对话|独白)|听(?:录音|对话|独白).{0,20}回答第|听第.{1,12}段.{0,30}(?:录音|对话|独白|回答)|(?:现在|接下来).{0,6}(?:你|考生).{0,18}(?:秒|分钟|时间).{0,25}(?:阅读|作答|答题|转涂)|听完.{0,30}(?:作答|回答|时间)|听每段.{0,30}阅读|每段(?:录音|对话|独白).{0,20}(?:播放|遍)|从题中所给|(?:从|在).{0,10}abc.{0,12}选项|(?:选出|选择).{0,12}(?:选项|答案)|^第[一二三四五六七八九十百零\d]+节(?:到此结束)?$|(?:这是|请看).{0,20}听力|(?:听力|试音).{0,20}(?:开始|结束)",RegexOptions.Compiled);
 const string Number=@"(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty)(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?";
 const string Refs=Number+@"(?:\s*(?:and|to|through|,|[-–—])\s*(?:questions?\s+)?"+Number+@")*";
 const string Noun=@"(?:recordings?|conversations?|dialogues?|passages?|talks?)";
 static readonly Regex EnglishLead=new Regex(@"^\s*(?:(?:now|please)[,\s]+)?(?:listen\s+to\s+|you\s+(?:will|are\s+going\s+to)\s+hear\s+|you\s+(?:now\s+)?have\s+.{0,35}(?:seconds?|minutes?)\s+to\s+(?:read|answer)\b|(?:this\s+is\s+)?(?:the\s+)?(?:end\s+of\s+(?:the\s+)?(?:listening|test)|listening\s+(?:test|section))\b|(?:section|part)\s+(?:one|two|three|four|[1-9])\b)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 static readonly Regex EnglishSentence=new Regex(@"^(?:(?:now|please)[,\s]+)?(?:listen\s+to\s+(?:the\s+)?(?:following\s+)?(?:(?:short|long)\s+)?"+Noun+@"(?:\s+"+Number+@")?(?:\s+and\s+answer\s+(?:the\s+)?questions?\s+"+Refs+@")?|you\s+(?:will|are\s+going\s+to)\s+hear\s+(?:(?:a|an|the|"+Number+@")\s+)?(?:(?:short|long|following)\s+)?"+Noun+@"(?:\s+(?:once|twice|again|two\s+times))?|you\s+(?:now\s+)?have\s+"+Number+@"\s+(?:seconds?|minutes?)\s+to\s+(?:read|answer)\s+(?:the\s+)?(?:(?:following|next)\s+)?questions?(?:\s+"+Refs+@")?|(?:this\s+is\s+)?(?:the\s+)?(?:end\s+of\s+(?:the\s+)?(?:listening(?:\s+test)?|test)|listening\s+(?:test|section))|(?:section|part)\s+(?:one|two|three|four|[1-9])\s*[:.]?)$",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 static bool PureEnglishInstruction(string text){var sentences=Regex.Split(text.Trim(),@"(?<=[.!?])\s+").Where(x=>x.Trim().Length>0).ToArray();return sentences.Length>0&&sentences.All(x=>EnglishSentence.IsMatch(x.Trim().TrimEnd('.','!','?')));}
 static bool HasHan(string s){return Regex.IsMatch(s,@"[\u4e00-\u9fff]");}
 static int EnglishWords(string s){return Regex.Matches(s,@"[A-Za-z]+(?:['’][A-Za-z]+)*").Cast<Match>().Count(m=>m.Value.Count(char.IsLetter)>1);}
 static bool ChineseSignal(string s){return ChinesePrompt.IsMatch(Data.Norm(s));}
 static bool ChineseInstruction(string s){string n=Data.Norm(s);Match m=ChinesePrompt.Match(n);return m.Success&&(m.Index==0||m.Index==1&&n[0]=='请');}
 public static bool Instruction(string text){text=text??"";if(HasHan(text))return EnglishWords(text)==0&&ChineseInstruction(text);return PureEnglishInstruction(text);}
 public static bool NeedsReview(string text){text=text??"";if(!HasHan(text))return EnglishLead.IsMatch(text)&&!PureEnglishInstruction(text);if(EnglishWords(text)>0)return ChineseSignal(text);return !Instruction(text);}
 public static bool StartsNewMaterial(string text){if(!Instruction(text))return false;if(HasHan(text))return Regex.IsMatch(Data.Norm(text),@"^请?(?:听下面|听第|听录音|听对话|听独白)");return Regex.IsMatch(text,@"^\s*(?:(?:now|please)[,\s]+)?(?:listen\s+to\b|you\s+(?:will|are\s+going\s+to)\s+hear\b)",RegexOptions.IgnoreCase);}
 public static string ReviewReason(string text){return EnglishWords(text)>0?"正文与考试提示混在同一段，无法可靠拆开，未自动归题。":"未识别出明确的考试提示格式，保留内容待确认。";}
}
}
