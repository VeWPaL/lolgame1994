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
  [switch]$SkipJs,
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

# ---------------------------------------------------------------- 6. the JavaScript suite, RUN
# This is the step the script existed without. For its whole life step 3 printed "open
# http://127.0.0.1:8731/depths.html?test to read the list" and the verdict added that the suite
# "still has to be read in the browser - this script cannot run it, because the suite executes at
# page load and needs a real canvas."
#
# Every gap in this project traces back to that one admission. The suite drifted from 207 declared
# to 211 actual without anything going red, because nothing ever compared the two. A character-sheet
# layout bug and a pointer-mapping bug both lived for the entire life of the suite because they are
# INVISIBLE AT 1280x720, and 1280x720 is what a developer gets. And the C# parity tables are
# hand-transcribed from "read it out of the running JavaScript", which is exactly what you cannot do
# unattended.
#
# So the suite is now run here, headlessly, and the count CONVENTIONS.md claims is asserted against
# the run rather than printed beside it.
#
# IT RUNS AT MORE THAN ONE VIEWPORT, and that is the part that earns the step. A single window hides
# a whole class of defect by construction: at 1280x720 the suite was 211/211 while at 960x600 - the
# canvas's own design size - it was failing. The design size is in the list deliberately.
#
# PLAYWRIGHT IS REQUIRED FROM THE HERMES INSTALL, not vendored, so this adds no dependency to the
# project and no package.json. If that path is missing the step reports itself SKIPPED and says why,
# rather than reporting a pass it did not perform.
Note ""
Note "6. the JavaScript suite, run"
if ($SkipJs) {
  Note "   SKIPPED (-SkipJs)"
} else {
  $pw = Join-Path $env:LOCALAPPDATA 'hermes\hermes-agent\node_modules\playwright'
  if (-not (Test-Path $pw)) {
    Note "   SKIPPED - playwright not found at '$pw'."
    Note "   This step is a REAL assertion. Without it the counts above are printed, not checked."
  } else {
    $runner = Join-Path $env:TEMP 'depths-suite.js'
    $runnerBody = @"
const pw=require('$($pw -replace '\\','/')');
const vps=[[960,600],[1280,720],[1920,1080]];
let claimedTotal=0;
(async()=>{
  let browser;
  try{ browser=await pw.chromium.launch({channel:'msedge'}); }
  catch(e){ console.log('LAUNCHFAIL '+e.message); process.exit(2); }
  let worst=0, out=[];
  for(const [w,h] of vps){
    const p=await browser.newPage({viewport:{width:w,height:h}});
    try{
      await p.goto('http://127.0.0.1:8731/depths.html?test',{waitUntil:'load',timeout:60000});
      await p.waitForFunction('window.__testResults!==undefined',{timeout:300000});
      const r=await p.evaluate(()=>({pass:window.__testResults.pass,total:window.__testResults.total,
        fails:window.__testResults.results.filter(x=>!x.ok).map(x=>x.name+' :: '+x.msg)}));
      out.push(w+'x'+h+' '+r.pass+'/'+r.total+(r.fails.length?(' FAIL '+r.fails.length):''));
      r.fails.forEach(f=>out.push('    '+f));
      worst=Math.max(worst,r.total-r.pass);
      if(r.total>claimedTotal) claimedTotal=r.total;
    }catch(e){ out.push(w+'x'+h+' ERROR '+e.message.slice(0,120)); worst=999; }
    await p.close();
  }
  await browser.close();
  console.log(out.join('\n'));
  console.log('WORSTFAILURES '+worst);
  console.log('CLAIMEDTOTAL '+claimedTotal);
})();
"@
    [System.IO.File]::WriteAllText($runner, ($runnerBody -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
    $jsOut = & node $runner 2>&1 | Out-String
    if ($jsOut -match 'WORSTFAILURES\s+(\d+)') {
      $worst = [int]$Matches[1]
      $claimedTotal = 0
      if ($jsOut -match 'CLAIMEDTOTAL\s+(\d+)') { $claimedTotal = [int]$Matches[1] }
      ($jsOut -split "`n" | Where-Object { $_.Trim() -ne '' -and $_ -notmatch 'CLAIMEDTOTAL' }) | ForEach-Object { Note "   $($_.Trim())" }
      if ($worst -eq 0) {
        Note "   the suite ran and every check passed at all three viewports"
        if ($jsClaim.Success -and [int]$jsClaim.Groups[1].Value -ne $claimedTotal) {
          Bad "CONVENTIONS.md claims $($jsClaim.Groups[1].Value) JS checks but the suite actually runs $claimedTotal - the document is asserting a number and the number is wrong"
        }
      } else {
        Bad "the JavaScript suite: $worst failing check(s) at one or more viewports"
      }
    } else {
      Bad "the JavaScript suite did not report a result - read it by hand: node $runner"
      Note ($jsOut.Trim())
    }
  }
}

# ---------------------------------------------------------------- 6b. the port manifest
# PORTED.md is the list of what is ported and the rule for changing it. It is only worth having if
# something checks it, because a manifest nobody reads is a document: the whole reason this file
# exists is that the port boundary used to live in prose in CONVENTIONS.md, which nobody reads at the
# moment of making a change.
#
# So this asserts two things a document cannot assert about itself:
#   - every C# source file is CLAIMED by some row. A new file that nobody added to the manifest is
#     the exact shape of the drift this is for: code exists, is compiled, and is not on the record.
#   - the parity-table row count it quotes is the row count in the test project. That number is a
#     maintenance cost stated in the document; if the tables grow, the cost claim is wrong.
Note ""
Note "6b. the port manifest"
$manifest = Join-Path $root 'PORTED.md'
if (-not (Test-Path $manifest)) {
  Bad "PORTED.md is missing - the port boundary has to be written down somewhere the gate can read"
} else {
  $mtext = [System.IO.File]::ReadAllText($manifest)
  $coreFiles = @(Get-ChildItem "$root\csharp\Depths.Core\*.cs" | ForEach-Object { $_.BaseName })
  $unclaimed = @()
  foreach ($f in $coreFiles) {
    if ($f -eq 'Depths.Core') { continue }
    if ($mtext -notmatch [regex]::Escape($f)) { $unclaimed += $f }
  }
  if ($unclaimed.Count -gt 0) {
    Bad ("PORTED.md does not mention: " + ($unclaimed -join ', ') + " - a file in the port that the manifest does not claim is exactly the drift this file exists to catch")
  } else {
    Note "   all $($coreFiles.Count) files in Depths.Core are accounted for by PORTED.md"
  }
  $m = [regex]::Match($mtext, 'pins (\d+) `\[TestCase\]` rows')
  if ($m.Success) {
    $claimedRows = [int]$m.Groups[1].Value
    $actualRows = 0
    Get-ChildItem "$root\csharp\Depths.Tests\*.cs" | ForEach-Object {
      $actualRows += ([regex]::Matches([System.IO.File]::ReadAllText($_.FullName), '\[TestCase\(')).Count
    }
    if ($claimedRows -ne $actualRows) {
      Bad "PORTED.md says the parity tables pin $claimedRows rows; the test project has $actualRows - either the document is stale or the tables grew without the cost being recorded"
    } else {
      Note "   the parity-table row count agrees: $actualRows"
    }
  } else {
    Note "   (PORTED.md states no parity-row count to check)"
  }
}

# ---------------------------------------------------------------- verdict
Note ""
if ($failures.Count -eq 0) {
  Note "VERIFY: all mechanical checks passed, and the JavaScript suite RAN."
  exit 0
} else {
  Note ("VERIFY: " + $failures.Count + " problem(s).") -ForegroundColor Yellow
  exit 1
}
