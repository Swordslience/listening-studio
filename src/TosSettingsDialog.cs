using System;using System.Windows.Forms;using System.Drawing;using System.Diagnostics;
namespace ListeningStudio {
 public sealed class TosSettingsDialog:Form {
  public Config Value;
  public TosSettingsDialog(Config c){Value=Data.Json().Deserialize<Config>(Data.Dump(c));Text="火山TOS · 首次配置后自动上传";Size=new Size(760,480);Font=new Font("Microsoft YaHei UI",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2};p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(p);
   var bucket=new TextBox{Text=c.tosBucket,Dock=DockStyle.Fill};var region=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};region.Items.AddRange(new object[]{"cn-beijing","cn-shanghai","cn-guangzhou","cn-hongkong","ap-southeast-1","ap-southeast-3"});region.SelectedItem=c.tosRegion;
   var ak=new TextBox{Text=c.tosAccessKey,Dock=DockStyle.Fill,UseSystemPasswordChar=true};var sk=new TextBox{Text=c.tosSecretKey,Dock=DockStyle.Fill,UseSystemPasswordChar=true};Control[] fields={bucket,region,ak,sk};string[] labels={"私有桶名称","桶所在地域","TOS AccessKey ID","TOS SecretAccessKey"};for(int i=0;i<fields.Length;i++){p.RowStyles.Add(new RowStyle(SizeType.Absolute,44));p.Controls.Add(new Label{Text=labels[i],AutoSize=true,Padding=new Padding(0,5,0,0)},0,i);p.Controls.Add(fields[i],1,i);}
   var note=new Label{Text="只需配置一次。上传和临时链接由软件自动完成。\r\n请使用标准存储、私有桶，专用授权只需本桶 listening-studio/ 目录的上传、读取和清理。\r\n不要填语音 App ID 或语音 API Key。存储按用量计费。\r\n勾选上级窗口“记住密钥”后，授权会在本机加密保存；不会导出到学校。",AutoSize=true,MaximumSize=new Size(690,150)};p.Controls.Add(note,0,4);p.SetColumnSpan(note,2);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var save=new Button{Text="保存存储配置",AutoSize=true};save.Click+=(x,y)=>{var candidate=Data.Json().Deserialize<Config>(Data.Dump(Value));candidate.tosBucket=bucket.Text.Trim();candidate.tosRegion=Convert.ToString(region.SelectedItem);candidate.tosAccessKey=ak.Text.Trim();candidate.tosSecretKey=sk.Text.Trim();try{TosStorage.Validate(candidate);Value=candidate;DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"存储配置尚未完整");}};var console=new Button{Text="打开火山TOS控制台",AutoSize=true};console.Click+=(x,y)=>Process.Start("https://console.volcengine.com/tos");buttons.Controls.Add(save);buttons.Controls.Add(console);p.Controls.Add(buttons,0,5);p.SetColumnSpan(buttons,2);
  }
 }
}
