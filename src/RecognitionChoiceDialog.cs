using System;using System.Drawing;using System.Windows.Forms;
namespace ListeningStudio {
 public static class RecognitionChoiceDialog {
  public static DialogResult Choose(IWin32Window owner,string provider){using(var f=new Form{Text="材料已有识别结果",Size=new Size(730,245),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=new Font("Microsoft YaHei UI",10)}){
   var label=new Label{Dock=DockStyle.Top,Height=105,Padding=new Padding(20),Text="这份材料已经有字幕。请选择这次要做什么：\r\n使用已有结果：保留文字和时间，只整理分组。\r\n重新识别：使用 "+provider+" 生成新字幕，会使用云服务额度。"};f.Controls.Add(label);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=75,Padding=new Padding(14)};f.Controls.Add(buttons);
   foreach(var item in new[]{Tuple.Create("使用已有结果（不计费）",DialogResult.Yes),Tuple.Create("重新识别（调用云服务）",DialogResult.No),Tuple.Create("取消",DialogResult.Cancel)}){var b=new Button{Text=item.Item1,DialogResult=item.Item2,AutoSize=true,MinimumSize=new Size(85,36)};buttons.Controls.Add(b);if(item.Item2==DialogResult.Cancel)f.CancelButton=b;}
   return f.ShowDialog(owner);
  }}
 }
}
