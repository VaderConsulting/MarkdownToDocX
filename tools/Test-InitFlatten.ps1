Add-Type -Path "c:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\src\MermaidRecognition\bin\Debug\net10.0\MermaidRecognition.dll"
$Input = @"
%%{init: {"flowchart": {"nodeSpacing": 80, "rankSpacing": 60}, "theme": "forest"}}%%
flowchart LR
"@
$Values = [MermaidRecognition.MermaidJsonConfigFlattener]::ParseLeadingInitBlocks($Input)
$Values.Keys | ForEach-Object { Write-Output "$_ = $($Values[$_])" }
