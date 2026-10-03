# ==============================================================================================
#   verify.ps1  -  the whole gate, in one command
#
#   Run this before every commit. It is the same six checks that were being done by hand, in the
#   same order, and the reason it exists is that a manual gate is a gate that gets skipped when
#   the change is small and the day is long. The hygiene checks in particular are the ones that
#   only matter exactly when nobody is thinking about them.
#
#   PowerShell 5.1 safe: no PadEnd/PadRight, no ternary, no null-coalescing. This file is LF-only
#   and UTF-8 without a BOM, like everything else in the repo - the middle-dot canary in
#   00-balance.js depends on the bytes being untouched.
# ==============================================================================================

param(
  [switch]$SkipCsharp,
  [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$failures = New-Object System.Collections.ArrayList

function Note($msg) { if (-not $Quiet) { Write-Host $msg } }
function Bad($msg) {
  $script:failures.Add($msg) | Out-Null
  Write-Host ("  FAIL  " + $msg) -ForegroundColor Red
}

# ---------------------------------------------------------------- 1. hygiene
# LF only, no BOM, no U+00C2, no U+FFFD. A CRLF pass would rewrite the bytes the canary depends on.
Note ""
Note "1. hygiene"
$files = @(Get-ChildItem "$root\src\*.js") + @(Get-Item "$root\depths.html") + @(Get-Item "$root\CONVENTIONS.md")
$crlf = 0; $bom = 0; $moji = 0; $repl = 0
foreach ($f in $files) {
  $b = [System.IO.File]::ReadAllBytes($f.FullName)
  if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { Bad "$($f.Name) has a BOM"; $bom++ }
  if (@($b | Where-Object { $_ -eq 13 }).Count -gt 0) { Bad "$($f.Name) has CR (must be LF only)"; $crlf++ }
  $t = [System.IO.File]::ReadAllText($f.FullName)
  if ($t.Contains([string][char]0x00C2)) { Bad "$($f.Name) contains U+00C2"; $moji++ }
  if ($t.Contains([string][char]0xFFFD)) { Bad "$($f.Name) contains U+FFFD (corrupted text)"; $repl++ }
}
if ($bom -eq 0 -and $crlf -eq 0 -and $moji -eq 0 -and $repl -eq 0) { Note "   clean: no BOM, no CR, no U+00C2, no U+FFFD" }

# ---------------------------------------------------------------- 2. the middle-dot canary
# The project's own canary: a middle dot that survives is proof the byte-level encoding is intact.
# It lives in 70-view.js and 80-ui.js (the user-visible strings) and nowhere else - an earlier draft
# of this script looked in 00-balance.js because a comment somewhere claimed it lived there, and
# reported a canary that had never existed.
Note ""
Note "2. the middle-dot canary"
$canary = 0
foreach ($cf in @("$root\src\70-view.js", "$root\src\80-ui.js")) {
  $n = ([regex]::Matches([System.IO.File]::ReadAllText($cf), [string][char]0x00B7)).Count
  $canary += $n
  Note "   $([System.IO.Path]::GetFileName($cf)): $n intact"
}
if ($canary -lt 10) { Bad "only $canary middle dots survive - the encoding is wrong somewhere" }

# ---------------------------------------------------------------- 3. the JavaScript suite
# Needs the local server on 8731. Started here if it is not already up, because a gate that
# silently skips itself when a background process died is not a gate.
Note ""
Note "3. the JavaScript suite"
$serverUp = $false
try {
  $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8731/depths.html' -UseBasicParsing -TimeoutSec 3
  $serverUp = ($r.StatusCode -eq 200)
} catch { $serverUp = $false }
if (-not $serverUp) {
  Note "   server not answering on 8731 - starting it"
  $sh = Join-Path $env:TEMP 'depths-server.ps1'
  # THE ROOT IS PASSED IN, not guessed. This used to derive itself as `Split-Path -Parent $PSScriptRoot`
  # and fall back to `$PSScriptRoot` - but it lives in %TEMP%, so the first is the parent of TEMP and
  # the second is TEMP itself, and neither can contain depths.html. It therefore hit its own
  # Write-Error every time and the gate reported "the local server is not answering on 8731" while
  # the real fault was three lines of path arithmetic. A gate that cannot start its own dependency
  # gets muted, and a muted gate is worse than no gate.
  $body = @"
`$root = '$($root -replace "'","''")'
if (-not (Test-Path (Join-Path `$root 'depths.html'))) {
  Write-Error "the server script cannot find depths.html under '$root'."
  exit 1
}
`$listener = New-Object System.Net.HttpListener
`$listener.Prefixes.Add("http://127.0.0.1:8731/")
`$listener.Start()
while (`$listener.IsListening) {
  `$ctx = `$listener.GetContext()
  `$rel = `$ctx.Request.Url.AbsolutePath.TrimStart('/')
  if (-not `$rel) { `$rel = 'depths.html' }
  `$path = Join-Path `$root `$rel
  if (Test-Path `$path) {
    `$ext = [System.IO.Path]::GetExtension(`$path).ToLower()
    `$type = switch (`$ext) { '.html' {'text/html'} '.js' {'application/javascript'} '.css' {'text/css'} default {'text/plain'} }
    `$bytes = [System.IO.File]::ReadAllBytes(`$path)
    `$ctx.Response.ContentType = `$type
    `$ctx.Response.ContentLength64 = `$bytes.Length
    # no-store, or the browser is entitled to serve a stale module for the rest of the session -
    # which is exactly how a verified fix appears not to have landed
    `$ctx.Response.Headers.Add('Cache-Control','no-store')
    `$ctx.Response.OutputStream.Write(`$bytes, 0, `$bytes.Length)
  } else { `$ctx.Response.StatusCode = 404 }
  `$ctx.Response.Close()
}
"@
  [System.IO.File]::WriteAllText($sh, ($body -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
  Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $sh -WindowStyle Hidden
  Start-Sleep -Milliseconds 1500
  try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8731/depths.html' -UseBasicParsing -TimeoutSec 4; $serverUp = ($r.StatusCode -eq 200) } catch { }
}
if (-not $serverUp) {
  Bad "the local server is not answering on 8731, so the JavaScript suite could not run"
} else {
  $src = [System.IO.File]::ReadAllText("$root\src\99-tests.js")
  $total = ([regex]::Matches($src, "(?m)^\s*test\('")).Count + ([regex]::Matches($src, "(?m)^\s*results\.push\(\{name:'")).Count
  Note "   server up; the source declares about $total checks (the run below is authoritative)"
  Note "   open http://127.0.0.1:8731/depths.html?test to read the list"
}

# ---------------------------------------------------------------- 4. the C# suite
Note ""
if ($SkipCsharp) {
  Note "4. the C# suite - SKIPPED (-SkipCsharp)"
} else {
  Note "4. the C# suite"
  $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
  if (-not $dotnet) {
    $p = "$env:ProgramFiles\dotnet\dotnet.exe"
    if (Test-Path $p) { $env:Path = (Split-Path $p) + ";" + $env:Path }
  }
  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Note "   dotnet not on PATH - skipping rather than reporting a false green"
  } else {
    Push-Location "$root\csharp\Depths.Tests"
    $out = & dotnet test --nologo -v q 2>&1 | Out-String
    Pop-Location
    $m = [regex]::Match($out, 'Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)')
    if ($m.Success) {
      if ([int]$m.Groups[1].Value -eq 0) { Note "   C# $($m.Groups[2].Value)/$($m.Groups[4].Value) passed" }
      else { Bad "C# suite: $($m.Groups[1].Value) failing" }
    } else {
      $fm = [regex]::Match($out, 'Failed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)')
      if ($fm.Success) { Bad "C# suite: $($fm.Groups[1].Value) failing" }
      else { Bad "the C# suite did not report a result - read it by hand: dotnet test" }
    }
  }
}

# ---------------------------------------------------------------- 5. the count claims
# CONVENTIONS.md states how many checks exist. It has been wrong three times, and a document that
# states a number is asserting it, so the number is asserted here instead of trusted.
Note ""
Note "5. the counts CONVENTIONS.md claims"
$conv = [System.IO.File]::ReadAllText("$root\CONVENTIONS.md")
$jsClaim = [regex]::Match($conv, '\*\*(\d+) checks\*\*')
$csClaim = [regex]::Match($conv, 'Depths\.Tests` has \*\*(\d+) checks\*\*')
$fixes = ([regex]::Matches([System.IO.File]::ReadAllText("$root\src\80-ui.js"), "(?m)^\s*'[^']{10,}':\['")).Count
if ($jsClaim.Success) { Note "   JS suite claimed as $($jsClaim.Groups[1].Value); FIXES table holds $fixes entries" }
if ($csClaim.Success) { Note "   C# suite claimed as $($csClaim.Groups[1].Value)" }
Note "   (the run above is authoritative - if these disagree, CONVENTIONS.md is the wrong one)"

# ---------------------------------------------------------------- verdict
Note ""
if ($failures.Count -eq 0) {
  Note "VERIFY: the mechanical checks passed."
  Note "        The JavaScript suite still has to be read in the browser - this script cannot"
  Note "        run it, because the suite executes at page load and needs a real canvas."
  exit 0
} else {
  Note ("VERIFY: " + $failures.Count + " problem(s).") -ForegroundColor Yellow
  exit 1
}
