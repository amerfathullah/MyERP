# convert-mat-tables.ps1
# Converts Angular Material mat-table to Bootstrap <table class="table table-hover"> with @for loops

$modified = @()

Get-ChildItem -Path "src/app" -Recurse -Include "*.html" |
  Where-Object { $_.FullName -notmatch "proxy|node_modules" } |
  ForEach-Object {
    $filePath = $_.FullName
    $content = Get-Content $filePath -Raw

    if ($content -notmatch 'mat-table') { return }

    $original = $content

    # Process all mat-table blocks
    # Match from <table mat-table ...> to </table>
    while ($content -match '(?s)(<table\s+mat-table\s+\[dataSource\]="([^"]+)"([^>]*)>)(.*?)(</table>)') {
      $fullMatch = $Matches[0]
      $dataSource = $Matches[2]
      $tableAttrs = $Matches[3]
      $tableBody = $Matches[4]

      # Extract additional classes from the table tag (preserve w-100, small, etc.)
      $extraClasses = ""
      if ($tableAttrs -match 'class="([^"]*)"') {
        $extraClasses = " " + $Matches[1]
      }

      # Extract all ng-container matColumnDef blocks
      $columns = @()
      $colPattern = '(?s)<ng-container\s+matColumnDef="([^"]+)">\s*(.*?)\s*</ng-container>'
      $colMatches = [regex]::Matches($tableBody, $colPattern)

      foreach ($col in $colMatches) {
        $colName = $col.Groups[1].Value
        $colContent = $col.Groups[2].Value

        # Extract th content and attributes
        $thContent = ""
        $thAttrs = ""
        if ($colContent -match '(?s)<th\s+mat-header-cell\s+\*matHeaderCellDef([^>]*)>(.*?)</th>') {
          $thAttrs = $Matches[1].Trim()
          $thContent = $Matches[2].Trim()
        }

        # Extract td content and attributes
        $tdContent = ""
        $tdAttrs = ""
        $rowVar = "row"
        if ($colContent -match '(?s)<td\s+mat-cell\s+\*matCellDef="let\s+(\w+)"([^>]*)>(.*?)</td>') {
          $rowVar = $Matches[1]
          $tdAttrs = $Matches[2].Trim()
          $tdContent = $Matches[3].Trim()
        } elseif ($colContent -match '(?s)<td\s+mat-cell\s+\*matCellDef="let\s+(\w+)"([^>]*)>\s*([\s\S]*?)\s*</td>') {
          # multiline td content (buttons, etc.)
          $rowVar = $Matches[1]
          $tdAttrs = $Matches[2].Trim()
          $tdContent = $Matches[3].Trim()
        }

        $columns += @{
          Name = $colName
          ThContent = $thContent
          ThAttrs = $thAttrs
          TdContent = $tdContent
          TdAttrs = $tdAttrs
          RowVar = $rowVar
        }
      }

      if ($columns.Count -eq 0) {
        # Skip if we couldn't parse columns (avoid infinite loop)
        break
      }

      # Determine the row variable name (use the first column's variable)
      $rowVarName = $columns[0].RowVar
      if (-not $rowVarName) { $rowVarName = "row" }

      # Detect indentation of the original table
      $indentMatch = [regex]::Match($content, '(?m)^(\s*)<table\s+mat-table')
      $baseIndent = if ($indentMatch.Success) { $indentMatch.Groups[1].Value } else { "          " }
      $i1 = $baseIndent + "  "  # thead/tbody indent
      $i2 = $i1 + "  "          # tr indent
      $i3 = $i2 + "  "          # th/td indent

      # Build thead
      $theadLines = @()
      $theadLines += "$i1<thead>"
      $theadLines += "$i2<tr>"
      foreach ($col in $columns) {
        $attrs = $col.ThAttrs
        if ($attrs) { $attrs = " $attrs" }
        $theadLines += "$i3<th$attrs>$($col.ThContent)</th>"
      }
      $theadLines += "$i2</tr>"
      $theadLines += "$i1</thead>"

      # Build tbody
      $tbodyLines = @()
      $tbodyLines += "$i1<tbody>"
      $tbodyLines += "$i2@for ($rowVarName of $dataSource; track $($rowVarName).id ?? `$index) {"
      $tbodyLines += "$i3<tr>"
      foreach ($col in $columns) {
        $attrs = $col.TdAttrs
        if ($attrs) { $attrs = " $attrs" }
        $tdContent = $col.TdContent

        # Check if content is multiline
        if ($tdContent -match "`n") {
          $tbodyLines += "$i3  <td$attrs>"
          # Indent multiline content
          $tdContent -split "`n" | ForEach-Object {
            $tbodyLines += "$i3    $($_.Trim())"
          }
          $tbodyLines += "$i3  </td>"
        } else {
          $tbodyLines += "$i3  <td$attrs>$tdContent</td>"
        }
      }
      $tbodyLines += "$i3</tr>"
      $tbodyLines += "$i2}"
      $tbodyLines += "$i1</tbody>"

      # Build replacement table
      $newTable = "$baseIndent<table class=`"table table-hover$extraClasses`">`n"
      $newTable += ($theadLines -join "`n") + "`n"
      $newTable += ($tbodyLines -join "`n") + "`n"
      $newTable += "$baseIndent</table>"

      $content = $content.Replace($fullMatch, $newTable)
    }

    # Remove mat-header-row and mat-row lines (in case any were missed)
    $content = $content -replace '(?m)^\s*<tr\s+mat-header-row[^>]*>\s*</tr>\s*\r?\n', ''
    $content = $content -replace '(?m)^\s*<tr\s+mat-row[^>]*>\s*</tr>\s*\r?\n', ''

    # Remove <mat-paginator> blocks (single or multiline)
    $content = $content -replace '(?s)\s*<mat-paginator[^>]*>\s*</mat-paginator>', ''
    # Also handle self-closing or multiline mat-paginator
    $content = $content -replace '(?s)\s*<mat-paginator\s[^/]*?/>', ''
    # Multiline mat-paginator with attributes on separate lines
    $content = $content -replace '(?s)\s*<mat-paginator\s+[^>]*?>\s*</mat-paginator>', ''

    if ($content -ne $original) {
      Set-Content $filePath $content -NoNewline
      $modified += $_.Name
      Write-Output "Modified: $($_.FullName -replace [regex]::Escape((Get-Location).Path + '\'), '')"
    }
  }

Write-Output ""
Write-Output "Total files modified: $($modified.Count)"
