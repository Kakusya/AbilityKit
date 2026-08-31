param(
    [string]$SourceDir = "Docs\design",
    [string]$OutputDir = "artifacts\feishu-design-export",
    [ValidateSet("native", "board")]
    [string]$MermaidMode = "board",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

function Get-RelativePathCompat([string]$BasePath, [string]$TargetPath) {
    $baseFull = [System.IO.Path]::GetFullPath($BasePath)
    $targetFull = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not $baseFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFull += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [Uri]$baseFull
    $targetUri = [Uri]$targetFull
    $relative = $baseUri.MakeRelativeUri($targetUri).ToString()
    return [Uri]::UnescapeDataString($relative).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function ConvertTo-Slug([string]$relativePath) {
    $withoutExtension = [System.IO.Path]::ChangeExtension($relativePath, $null)
    $normalized = $withoutExtension -replace '\\', '/'
    $normalized = $normalized -replace '[^0-9A-Za-z\u4e00-\u9fa5/_-]+', '-'
    $normalized = $normalized -replace '/+', '/'
    $normalized = $normalized.Trim('/','-')
    if ([string]::IsNullOrWhiteSpace($normalized)) { return "document" }
    return $normalized
}

function Get-TitleFromMarkdown([string]$content, [string]$fallback) {
    $match = [regex]::Match($content, '(?m)^#\s+(.+?)\s*$')
    if ($match.Success) { return $match.Groups[1].Value.Trim() }
    return [System.IO.Path]::GetFileNameWithoutExtension($fallback)
}

function Get-FeishuTitle([string]$title) {
    $normalized = $title.Trim()
    if ([string]::IsNullOrWhiteSpace($normalized)) { return 'Untitled design document' }
    return $normalized
}

function Get-NewLine() {
    return [Environment]::NewLine
}

function Get-MarkdownFence() {
    return ([string][char]96) * 3
}

function Get-MarkdownStats([string]$content) {
    $fenceEscaped = [regex]::Escape((Get-MarkdownFence))
    $codePattern = '(?ms)^' + $fenceEscaped + '([^\r\n]*)[\r\n].*?^' + $fenceEscaped
    $mermaidPattern = '(?ms)^' + $fenceEscaped + 'mermaid[\r\n].*?^' + $fenceEscaped
    $codeBlocks = [regex]::Matches($content, $codePattern).Count
    $mermaidBlocks = [regex]::Matches($content, $mermaidPattern).Count
    $tables = [regex]::Matches($content, '(?m)^\|.*\|\s*$').Count
    $headings = [regex]::Matches($content, '(?m)^#{1,6}\s+').Count
    $links = [regex]::Matches($content, '\[[^\]]+\]\([^)]+\)').Count
    return [ordered]@{
        headings = $headings
        codeBlocks = $codeBlocks
        mermaidBlocks = $mermaidBlocks
        tableRows = $tables
        links = $links
    }
}

function ConvertTo-FeishuMarkdown([string]$content, [string]$relativePath, [string]$title, [string]$MermaidMode) {
    $lf = [string][char]10
    $nl = Get-NewLine
    $normalized = $content.Replace([string][char]13 + [string][char]10, $lf)
    $lines = $normalized.Split([char]10)
    $output = New-Object System.Collections.Generic.List[string]
    $diagrams = New-Object System.Collections.Generic.List[object]
    $output.Add('<!--')
    $output.Add('source: ' + $relativePath)
    $output.Add('title: ' + $title)
    $output.Add('generated_for: feishu_api_import')
    $output.Add('-->')
    $output.Add('')

    $inFence = $false
    $fenceLang = ''
    $diagramBuffer = New-Object System.Collections.Generic.List[string]
    $fencePattern = '^' + [regex]::Escape((Get-MarkdownFence)) + '(.*)\s*$'
    foreach ($line in $lines) {
        if ($line -match $fencePattern) {
            if (-not $inFence) {
                $fenceLang = $matches[1].Trim()
                $inFence = $true
                if ($fenceLang -eq 'mermaid' -and $MermaidMode -eq 'board') {
                    $diagramBuffer.Clear()
                }
                else {
                    $output.Add($line)
                }
            }
            else {
                if ($fenceLang -eq 'mermaid' -and $MermaidMode -eq 'board') {
                    $number = $diagrams.Count + 1
                    $marker = 'FEISHU_MERMAID_BOARD_' + $number.ToString('000')
                    $diagrams.Add([pscustomobject]@{
                        marker = $marker
                        source = ($diagramBuffer.ToArray() -join $lf).Trim()
                        index = $number
                    })
                    $output.Add($marker)
                }
                else {
                    $output.Add($line)
                }
                $inFence = $false
                $fenceLang = ''
            }
            continue
        }

        if ($inFence -and $fenceLang -eq 'mermaid' -and $MermaidMode -eq 'board') {
            $diagramBuffer.Add($line)
        }
        else {
            $output.Add($line)
        }
    }

    return [pscustomobject]@{
        content = ($output -join $nl)
        diagrams = $diagrams.ToArray()
    }
}

function HtmlEncode([string]$value) {
    if ($null -eq $value) { return '' }
    return [System.Net.WebUtility]::HtmlEncode($value)
}

function Convert-InlineMarkdownToHtml([string]$line) {
    $encoded = HtmlEncode $line
    $encoded = [regex]::Replace($encoded, '\*\*(.+?)\*\*', '<strong>$1</strong>')
    $tick = [regex]::Escape([string][char]96)
    $inlineCodePattern = $tick + '([^' + $tick + ']+)' + $tick
    $encoded = [regex]::Replace($encoded, $inlineCodePattern, '<code>$1</code>')
    $encoded = [regex]::Replace($encoded, '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2">$1</a>')
    return $encoded
}

function Close-List([System.Collections.Generic.List[string]]$body, [ref]$inList) {
    if ($inList.Value) {
        $body.Add('</ul>')
        $inList.Value = $false
    }
}

function Close-Table([System.Collections.Generic.List[string]]$body, [ref]$inTable) {
    if ($inTable.Value) {
        $body.Add('</tbody></table>')
        $inTable.Value = $false
    }
}

function Convert-MarkdownToHtml([string]$content, [string]$title, [string]$relativePath) {
    $lf = [string][char]10
    $normalized = $content.Replace([string][char]13 + [string][char]10, $lf)
    $lines = $normalized.Split([char]10)
    $body = New-Object System.Collections.Generic.List[string]
    $inFence = $false
    $fenceLang = ''
    $codeBuffer = New-Object System.Collections.Generic.List[string]
    $inList = $false
    $inTable = $false

    $fencePattern = '^' + [regex]::Escape((Get-MarkdownFence)) + '(.*)\s*$'
    foreach ($line in $lines) {
        if ($line -match $fencePattern) {
            if (-not $inFence) {
                Close-List $body ([ref]$inList)
                Close-Table $body ([ref]$inTable)
                $inFence = $true
                $fenceLang = $matches[1].Trim()
                $codeBuffer.Clear()
            }
            else {
                $class = ''
                if (-not [string]::IsNullOrWhiteSpace($fenceLang)) {
                    $class = ' class="language-' + (HtmlEncode $fenceLang) + '"'
                }
                if ($fenceLang -eq 'mermaid') {
                    $body.Add('<div class="diagram-note">Diagram widget: Mermaid source</div>')
                }
                $body.Add('<pre><code' + $class + '>' + (HtmlEncode (($codeBuffer.ToArray()) -join $lf)) + '</code></pre>')
                $inFence = $false
                $fenceLang = ''
            }
            continue
        }

        if ($inFence) {
            $codeBuffer.Add($line)
            continue
        }

        if ($line -match '^\s*$') {
            Close-List $body ([ref]$inList)
            Close-Table $body ([ref]$inTable)
            continue
        }

        if ($line -match '^(#{1,6})\s+(.+)$') {
            Close-List $body ([ref]$inList)
            Close-Table $body ([ref]$inTable)
            $level = $matches[1].Length
            $text = Convert-InlineMarkdownToHtml $matches[2].Trim()
            $body.Add('<h' + $level + '>' + $text + '</h' + $level + '>')
            continue
        }

        if ($line -match '^\s*[-*]\s+(.+)$') {
            Close-Table $body ([ref]$inTable)
            if (-not $inList) {
                $body.Add('<ul>')
                $inList = $true
            }
            $body.Add('<li>' + (Convert-InlineMarkdownToHtml $matches[1].Trim()) + '</li>')
            continue
        }

        if ($line -match '^\|.*\|\s*$') {
            Close-List $body ([ref]$inList)
            $cells = $line.Trim().Trim('|') -split '\|'
            $isDivider = $true
            foreach ($cell in $cells) {
                if ($cell.Trim() -notmatch '^:?-{3,}:?$') {
                    $isDivider = $false
                    break
                }
            }
            if ($isDivider) { continue }
            if (-not $inTable) {
                $body.Add('<table><tbody>')
                $inTable = $true
            }
            $htmlCells = $cells | ForEach-Object { '<td>' + (Convert-InlineMarkdownToHtml $_.Trim()) + '</td>' }
            $body.Add('<tr>' + ($htmlCells -join '') + '</tr>')
            continue
        }

        Close-List $body ([ref]$inList)
        Close-Table $body ([ref]$inTable)
        if ($line -match '^>\s*(.+)$') {
            $body.Add('<blockquote>' + (Convert-InlineMarkdownToHtml $matches[1].Trim()) + '</blockquote>')
        }
        else {
            $body.Add('<p>' + (Convert-InlineMarkdownToHtml $line.Trim()) + '</p>')
        }
    }

    Close-List $body ([ref]$inList)
    Close-Table $body ([ref]$inTable)

    $encodedTitle = HtmlEncode $title
    $encodedSource = HtmlEncode $relativePath
    $bodyHtml = $body -join $lf
    return @"
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>$encodedTitle</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Microsoft YaHei", sans-serif; line-height: 1.72; max-width: 1040px; margin: 32px auto; padding: 0 24px; color: #1f2937; }
    h1, h2, h3, h4, h5, h6 { line-height: 1.35; margin-top: 1.5em; }
    code { background: #f3f4f6; border-radius: 4px; padding: 0.1em 0.35em; }
    pre { background: #111827; color: #e5e7eb; border-radius: 8px; padding: 16px; overflow: auto; }
    pre code { background: transparent; padding: 0; }
    table { width: 100%; border-collapse: collapse; margin: 16px 0; }
    td, th { border: 1px solid #d1d5db; padding: 8px 10px; vertical-align: top; }
    blockquote { margin: 16px 0; padding: 8px 16px; border-left: 4px solid #9ca3af; background: #f9fafb; color: #4b5563; }
    .source { color: #6b7280; font-size: 13px; border-bottom: 1px solid #e5e7eb; padding-bottom: 12px; }
    .diagram-note { margin: 12px 0 6px; padding: 8px 12px; background: #eff6ff; border-left: 4px solid #2563eb; color: #1d4ed8; }
  </style>
</head>
<body>
  <div class="source">Source: $encodedSource</div>
$bodyHtml
</body>
</html>
"@
}

$sourceFull = [System.IO.Path]::GetFullPath($SourceDir)
$outputFull = [System.IO.Path]::GetFullPath($OutputDir)
$markdownOut = Join-Path $outputFull "markdown"
$htmlOut = Join-Path $outputFull "html"
$mermaidOut = Join-Path $outputFull "mermaid"

if (-not (Test-Path $sourceFull)) {
    throw "SourceDir not found: $SourceDir"
}

if ($Clean -and (Test-Path $outputFull)) {
    Get-ChildItem -LiteralPath $outputFull -Force | Where-Object {
        $_.Name -ne 'feishu-sync-state.local.json' -and
        $_.Name -ne 'feishu-user-token.local.json'
    } | Remove-Item -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $markdownOut | Out-Null
New-Item -ItemType Directory -Force -Path $htmlOut | Out-Null
New-Item -ItemType Directory -Force -Path $mermaidOut | Out-Null

$files = Get-ChildItem -Path $sourceFull -Filter "*.md" -Recurse -File | Sort-Object FullName
$manifest = New-Object System.Collections.Generic.List[object]
$indexLines = New-Object System.Collections.Generic.List[string]
$indexLines.Add("# AbilityKit Design Feishu Offline Import Manifest")
$indexLines.Add("")
$indexLines.Add("GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$indexLines.Add("")
$indexLines.Add("| Order | Title | Source | Markdown | HTML | CodeBlocks | Mermaid | TableRows |")
$indexLines.Add("|-------|-------|--------|----------|------|------------|---------|-----------|")

$order = 0
foreach ($file in $files) {
    $order++
    $relative = Get-RelativePathCompat $sourceFull $file.FullName
    $relativeUnix = $relative.Replace('\','/')
    $slug = ConvertTo-Slug $relative
    $content = Get-Content -Raw -LiteralPath $file.FullName -Encoding UTF8
    $title = Get-TitleFromMarkdown $content $file.Name
    $feishuTitle = Get-FeishuTitle $title
    $stats = Get-MarkdownStats $content

    $mdTarget = Join-Path $markdownOut ($slug + ".md")
    $htmlTarget = Join-Path $htmlOut ($slug + ".html")
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($mdTarget)) | Out-Null
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($htmlTarget)) | Out-Null

    $converted = ConvertTo-FeishuMarkdown $content $relativeUnix $title $MermaidMode
    $html = Convert-MarkdownToHtml $content $title $relativeUnix
    Set-Content -LiteralPath $mdTarget -Value $converted.content -Encoding UTF8
    Set-Content -LiteralPath $htmlTarget -Value $html -Encoding UTF8

    $diagramEntries = New-Object System.Collections.Generic.List[object]
    foreach ($diagram in $converted.diagrams) {
        $diagramBase = $slug + '/' + $diagram.index.ToString('000')
        $mmdTarget = Join-Path $mermaidOut ($diagramBase + '.mmd')
        New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($mmdTarget)) | Out-Null
        [System.IO.File]::WriteAllText($mmdTarget, [string]$diagram.source, (New-Object System.Text.UTF8Encoding($false)))
        $diagramEntries.Add([pscustomobject]@{
            marker = $diagram.marker
            source = (Get-RelativePathCompat $outputFull $mmdTarget).Replace('\','/')
        })
    }

    $mdRel = Get-RelativePathCompat $outputFull $mdTarget
    $htmlRel = Get-RelativePathCompat $outputFull $htmlTarget
    $entry = [ordered]@{
        order = $order
        title = $title
        source = $relativeUnix
        mermaidMode = $MermaidMode
        slug = $slug
        markdown = $mdRel.Replace('\','/')
        html = $htmlRel.Replace('\','/')
        stats = $stats
        diagrams = $diagramEntries.ToArray()
        feishuTitle = $feishuTitle
        suggestedFeishuTitle = $feishuTitle
        feishuNodeToken = $null
        feishuDocumentUrl = $null
    }
    $manifest.Add([pscustomobject]$entry)

    $indexLines.Add("| $order | $title | $relativeUnix | $($entry.markdown) | $($entry.html) | $($stats.codeBlocks) | $($stats.mermaidBlocks) | $($stats.tableRows) |")
}

$nl = Get-NewLine
$manifestJson = $manifest | ConvertTo-Json -Depth 8
Set-Content -LiteralPath (Join-Path $outputFull "manifest.json") -Value $manifestJson -Encoding UTF8
Set-Content -LiteralPath (Join-Path $outputFull "manifest.md") -Value ($indexLines -join $nl) -Encoding UTF8

$mermaidReadmeLines = if ($MermaidMode -eq 'board') {
    @(
        '- markdown/: Feishu-import-friendly Markdown. Mermaid source is replaced by stable Board placeholders.',
        '- mermaid/: Extracted Mermaid source converted into editable Board nodes during API post-processing.'
    )
}
else {
    @(
        '- markdown/: Feishu-import-friendly Markdown. Mermaid fenced source is preserved for native Feishu import.',
        '- mermaid/: Empty in native mode because Mermaid source remains embedded in Markdown.'
    )
}

$readmeLines = @(
    '# Feishu offline import package',
    '',
    ('This directory is generated by tools/export_design_docs_for_feishu.ps1 from source directory: {0}.' -f $SourceDir),
    ('Mermaid publication mode: {0}.' -f $MermaidMode),
    '',
    '## Contents',
    ''
) + $mermaidReadmeLines + @(
    '- html/: Static HTML previews for headings, paragraphs, lists, tables, code blocks, and Mermaid source blocks.',
    '- manifest.json: Machine-readable sync manifest. Later Feishu API sync can write back feishuNodeToken and feishuDocumentUrl.',
    '- manifest.md: Human-readable review manifest.',
    '',
    '## Suggested import flow',
    '',
    '1. Open manifest.md and review document order, titles, and stats.',
    '2. Create a root node in Feishu Wiki, for example AbilityKit Design Docs.',
    '3. Create page hierarchy according to each source path in manifest.json.',
    '4. Import the matching file under markdown/; open html/ when visual preview is needed.',
    '5. A later Feishu API tool should use source as the stable key, slug as the output path, and feishuNodeToken as the remote update target.'
)
Set-Content -LiteralPath (Join-Path $outputFull "README.md") -Value ($readmeLines -join $nl) -Encoding UTF8

Write-Host "Exported $($files.Count) documents to $OutputDir"
Write-Host "Manifest: $(Join-Path $OutputDir 'manifest.md')"
