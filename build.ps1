param([switch]$RunTests)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$build=Join-Path $root 'dist\家里处理器'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw '需要Windows .NET Framework 4.8（64位）编译器'}
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Net.Http.dll','/reference:System.Security.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
$facade=Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\netstandard\v4.0_2.0.0.0__cc7b13ffcd2ddd51\netstandard.dll'
if(Test-Path -LiteralPath $facade){$refs+=('/reference:'+$facade)}
foreach($dll in Get-ChildItem -LiteralPath (Join-Path $root 'lib') -Filter '*.dll'){Copy-Item -LiteralPath $dll.FullName -Destination $build -Force;$refs+=('/reference:'+$dll.FullName)}
$src=Join-Path $root 'src'
$sources=Get-ChildItem -LiteralPath $src -Filter '*.cs' | Where-Object Name -ne 'UpdateHelper.cs' | ForEach-Object FullName
& $compiler @('/nologo','/target:winexe','/optimize+',('/out:'+(Join-Path $build '英语听力工作台.exe'))) @refs @sources
if($LASTEXITCODE -ne 0){throw '主程序编译失败'}
$sources=@('UpdateCore.cs','UpdateIdentity.cs','UpdateHelper.cs','AppVersion.cs') | ForEach-Object {Join-Path $src $_}
& $compiler @('/nologo','/target:winexe','/optimize+',('/out:'+(Join-Path $build '更新助手.exe'))) @refs @sources
if($LASTEXITCODE -ne 0){throw '更新助手编译失败'}
Copy-Item -LiteralPath (Join-Path $root 'third_party\NAudio-LICENSE.txt') -Destination (Join-Path $build '开源许可.txt') -Force
if(Test-Path -LiteralPath (Join-Path $root 'tools\ffmpeg.exe')){New-Item -ItemType Directory -Path (Join-Path $build '工具') -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $root 'tools\ffmpeg.exe') -Destination (Join-Path $build '工具\ffmpeg.exe') -Force}
$school=Join-Path $root 'dist\课堂播放器';New-Item -ItemType Directory -Path $school -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build '英语听力工作台.exe') -Destination (Join-Path $school '课堂播放器.exe') -Force
foreach($file in Get-ChildItem -LiteralPath $build -File | Where-Object {$_.Name -ne '英语听力工作台.exe'}){Copy-Item -LiteralPath $file.FullName -Destination $school -Force}
if($RunTests){$test=Join-Path $root 'dist\GenericGroupingTests.exe';& $compiler @('/nologo','/target:exe',('/out:'+$test),('/reference:'+(Join-Path $build '英语听力工作台.exe'))) @refs (Join-Path $root 'tests\GenericGroupingTests.cs');if($LASTEXITCODE -ne 0){throw '测试编译失败'};Copy-Item -LiteralPath (Join-Path $build '英语听力工作台.exe') -Destination (Join-Path $root 'dist') -Force;Get-ChildItem -LiteralPath $build -Filter '*.dll'|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $root 'dist') -Force};& $test;if($LASTEXITCODE -ne 0){throw '测试失败'}}
Write-Output ('构建完成：'+$build)
