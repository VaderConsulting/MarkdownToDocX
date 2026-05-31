$ErrorActionPreference = 'Stop'
$MarkdigDll = Join-Path $env:USERPROFILE '.nuget\packages\markdig\0.40.0\lib\net9.0\Markdig.dll'
if (-not (Test-Path $MarkdigDll)) {
    $MarkdigDll = Join-Path $env:USERPROFILE '.nuget\packages\markdig\0.37.0\lib\net8.0\Markdig.dll'
}
if (-not (Test-Path $MarkdigDll)) {
    $MarkdigDll = (Get-ChildItem -Path (Join-Path $env:USERPROFILE '.nuget\packages\markdig') -Recurse -Filter 'Markdig.dll' | Select-Object -First 1).FullName
}
Add-Type -Path $MarkdigDll

$SpecPath = Join-Path $PSScriptRoot '..\spec.md'
$Markdown = [System.IO.File]::ReadAllText($SpecPath)

$BuilderType = [Markdig.MarkdownPipelineBuilder]
$Pipeline = $BuilderType::new().UseAdvancedExtensions().Build()
$Document = [Markdig.Markdown]::Parse($Markdown, $Pipeline)

$TableCount = 0
$PipeParagraphs = New-Object System.Collections.Generic.List[string]

foreach ($Block in $Document)
{
    if ($Block -is [Markdig.Extensions.Tables.Table])
    {
        $TableCount++
        continue
    }

    if ($Block -is [Markdig.Syntax.ParagraphBlock])
    {
        $Writer = New-Object System.IO.StringWriter
        $Renderer = New-Object Markdig.Renderers.HtmlRenderer($Writer)
        $BlockLines = New-Object System.Collections.Generic.List[string]
        $LineIndex = 0
        foreach ($Line in $Block.Lines.Lines)
        {
            $Slice = $Line.Slice
            $Text = $Markdown.Substring($Slice.Start, $Slice.Length)
            $BlockLines.Add($Text)
        }
        $Joined = ($BlockLines -join ' ').Trim()
        if ($Joined.StartsWith('|') -and $Joined.Contains('|'))
        {
            [void]$PipeParagraphs.Add($Joined.Substring(0, [Math]::Min(140, $Joined.Length)))
        }
    }
}

Write-Host "Markdig tables: $TableCount"
Write-Host "Pipe paragraphs (likely broken tables): $($PipeParagraphs.Count)"
foreach ($Item in $PipeParagraphs)
{
    Write-Host "  $Item"
}
