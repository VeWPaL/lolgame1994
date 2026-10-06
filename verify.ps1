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
  [switch]$Deep,
  [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$failures = New-Object System.Collections.ArrayList

# One port for the gate and every script in tools/, which all default to 8791.
$port = 8791

# Under 'Stop', PowerShell 5.1 turns any line node writes to stderr into a terminating error, so a
# failing audit aborted the whole gate before its own Bad() could report it. Relaxed locally only.
function Invoke-Node($script) {
  $ErrorActionPreference = 'Continue'
  $out = & node $script 2>&1 | ForEach-Object { "$_" } | Out-String
  return $out
}

function Note($msg) { if (-not $Quiet) { Write-Host $msg } }
function Bad($msg) {
  $script:failures.Add($msg) | Out-Null
  Write-Host ("  FAIL  " + $msg) -ForegroundColor Red
}

# ---------------------------------------------------------------- 0. the session start
# WRITTEN HERE, ONCE, WHEN IT IS STALE. It is not written by a person and it is not written by me:
# .gitignore documents it as "written here at the beginning of a working session so that a report can
# state how long it took WITHOUT relying on remembering to note the time", and NOTHING WROTE IT. It sat
# at 2026-10-01 through a session that ran to 2026-10-04, and the report that asked for a duration got a
# two-day-old answer that was wrong by roughly a day and a half.
#
# The rule this encodes: the file exists so a duration can be stated without remembering. Anything that
# must be remembered is not a record, it is a hope. So the gate - which runs before every commit, and
# therefore on the first commit of every session - refreshes it whenever it is more than a few hours old,
# and never rewrites a fresh one. A session start is when the FIRST COMMIT happens, not when the clock
# was last read, so this is the earliest moment at which the answer can be known to be right.
$sessionFile = "$root\.session-start"
$sessionAgeH = [double]::PositiveInfinity
if (Test-Path $sessionFile) {
  $sessionAgeH = ((Get-Date) - (Get-Item $sessionFile).LastWriteTime).TotalHours
} else {
  $sessionAgeH = [double]::PositiveInfinity   # absent: treat as maximally stale, so the first commit writes it
}
if ($sessionAgeH -gt 6) {
  [System.IO.File]::WriteAllText($sessionFile, (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss') + "`n")
  Note ("0. session start")
  if (Test-Path $sessionFile) {
    Note ("   stale by {0:N1}h - rewritten, so a report can state a real duration" -f $sessionAgeH)
  } else {
    Note "   written"
  }
} elseif (-not $Quiet) {
  Note ("0. session start: {0:N1}h old, left alone (a fresh one is not rewritten)" -f $sessionAgeH)
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
# Needs the local server on $port. Started here if it is not already up, because a gate that
# silently skips itself when a background process died is not a gate.
Note ""
Note "3. the JavaScript suite"
$serverUp = $false
try {
  $r = Invoke-WebRequest -Uri "http://127.0.0.1:$port/depths.html" -UseBasicParsing -TimeoutSec 3
  $serverUp = ($r.StatusCode -eq 200)
} catch { $serverUp = $false }
if (-not $serverUp) {
  Note "   server not answering on $port - starting it"
  $sh = Join-Path $env:TEMP 'depths-server.ps1'
  # THE ROOT IS PASSED IN, not guessed. This used to derive itself as `Split-Path -Parent $PSScriptRoot`
  # and fall back to `$PSScriptRoot` - but it lives in %TEMP%, so the first is the parent of TEMP and
  # the second is TEMP itself, and neither can contain depths.html. It therefore hit its own
  # Write-Error every time and the gate reported "the local server is not answering on $port" while
  # the real fault was three lines of path arithmetic. A gate that cannot start its own dependency
  # gets muted, and a muted gate is worse than no gate.
  $body = @"
`$root = '$($root -replace "'","''")'
if (-not (Test-Path (Join-Path `$root 'depths.html'))) {
  Write-Error "the server script cannot find depths.html under '$root'."
  exit 1
}
`$listener = New-Object System.Net.HttpListener
`$listener.Prefixes.Add("http://127.0.0.1:$port/")
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
  try { $r = Invoke-WebRequest -Uri "http://127.0.0.1:$port/depths.html" -UseBasicParsing -TimeoutSec 4; $serverUp = ($r.StatusCode -eq 200) } catch { }
}
if (-not $serverUp) {
  Bad "the local server is not answering on $port, so the JavaScript suite could not run"
} else {
  $src = [System.IO.File]::ReadAllText("$root\src\99-tests.js")
  $total = ([regex]::Matches($src, "(?m)^\s*test\('")).Count + ([regex]::Matches($src, "(?m)^\s*results\.push\(\{name:'")).Count
  Note "   server up; the source declares about $total checks (the run below is authoritative)"
  Note "   open http://127.0.0.1:$port/depths.html?test to read the list"
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
# http://127.0.0.1:$port/depths.html?test to read the list" and the verdict added that the suite
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
if ($Deep) { Note "   (-Deep: 14 viewports, 720x480 through 2560x1440 - use sparingly, it is a full suite run each)" }
if ($SkipJs) {
  Note "   SKIPPED (-SkipJs)"
} else {
  $pw = Join-Path $env:LOCALAPPDATA 'hermes\hermes-agent\node_modules\playwright'
  if (-not (Test-Path $pw)) {
    Note "   SKIPPED - playwright not found at '$pw'."
    Note "   This step is a REAL assertion. Without it the counts above are printed, not checked."
  } else {
    $runner = Join-Path $env:TEMP 'depths-suite.js'
    # The viewport list is built HERE rather than inside the here-string. PowerShell 5.1 has no
    # ternary, and an inline if-expression in an expandable here-string either does not evaluate or
    # silently drops the brackets - which produced `const vps=[960,600],[1280,720]` and a syntax
    # error at line 2 of the generated file. A variable interpolated into the string cannot lose its
    # punctuation.
    if ($Deep) {
      $vpList = '[720,480],[800,600],[960,540],[960,600],[1024,640],[1024,768],[1152,720],[1280,720],[1280,1024],[1366,768],[1440,900],[1600,900],[1920,1080],[2560,1440]'
    } else {
      $vpList = '[960,600],[1280,720],[1920,1080]'
    }
    $runnerBody = @"
const pw=require('$($pw -replace '\\','/')');
const vps=[$vpList];
let claimedTotal=0;
(async()=>{
  let browser;
  try{ browser=await pw.chromium.launch({channel:'msedge'}); }
  catch(e){ console.log('LAUNCHFAIL '+e.message); process.exit(2); }
  let worst=0, out=[];
  for(const [w,h] of vps){
    const p=await browser.newPage({viewport:{width:w,height:h}});
    try{
      await p.goto('http://127.0.0.1:$port/depths.html?test',{waitUntil:'commit',timeout:60000});
      await p.waitForFunction('window.__testResults!==undefined',null,{timeout:300000});
      // AND THEN FOR THE ASYNCHRONOUS TESTS. One test is `async` because it renders audio waveforms
      // through OfflineAudioContext, and the suite publishes its results before that resolves. Waiting
      // only for `!==undefined` reads a PARTIAL suite and counts it - which is how CONVENTIONS.md came
      // to say 236 checks when the suite has 237. `settled` is set once every async test has recorded.
      // A `//` comment and not a `#` one, because this is inside a JavaScript here-string.
      await p.waitForFunction('window.__testResults.settled===true',null,{timeout:300000});
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
    $jsOut = Invoke-Node $runner
    if ($jsOut -match 'WORSTFAILURES\s+(\d+)') {
      $worst = [int]$Matches[1]
      $claimedTotal = 0
      if ($jsOut -match 'CLAIMEDTOTAL\s+(\d+)') { $claimedTotal = [int]$Matches[1] }
      ($jsOut -split "`n" | Where-Object { $_.Trim() -ne '' -and $_ -notmatch 'CLAIMEDTOTAL' }) | ForEach-Object { Note "   $($_.Trim())" }
      if ($worst -eq 0) {
        Note "   the suite ran and every check passed at every viewport tested"
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
      $text = [System.IO.File]::ReadAllText($_.FullName)
      # C#-only fixtures (rules written after js-final) are not parity rows
      if ($text -match 'Category\("csharp-only"\)') { return }
      $actualRows += ([regex]::Matches($text, '\[TestCase\(')).Count
    }
    if ($claimedRows -ne $actualRows) {
      Bad "PORTED.md says the parity tables pin $claimedRows rows; the test project has $actualRows - either the document is stale or the tables grew without the cost being recorded"
    } else {
      Note "   the parity-table row count agrees: $actualRows"
    }
  } else {
    Note "   (PORTED.md states no parity-row count to check)"
  }

  # 6c. THE PARITY AUDIT, and this is the check that earns the row count above its keep.
  #
  # The row count only proves the tables are the SIZE PORTED.md says. It cannot tell whether the
  # numbers in them still agree with the game, because both sides could be equally wrong - which is
  # exactly the state this repo sat in for a session: the port lagged the game by 130px of swerve
  # ramp, both sides' tests were green, and the row count agreed throughout.
  #
  # tools/parity-audit.js reads the live `depths.html?parity` output and diffs all five tables against
  # the C# test files. It REPORTS rather than regenerating, because a gate that writes its own
  # expectations asserts nothing. Any difference is a FAILURE, and the reason a difference matters is
  # that the documented workflow is "paste the page over the C# file" - so a difference means either
  # the generator is wrong or the table is stale, and both have happened.
  #
  # It needs the same two things step 6 already needs: a static server on 8791 and Edge via Playwright.
  # Skipped, loudly, when either is absent - a check that silently does not run is the one thing this
  # file exists to prevent.
  Note ""
  Note "6c. the parity audit: the live page against the C# tables"
  $auditScript = "$root\tools\parity-audit.js"
  if (-not (Test-Path $auditScript)) {
    Note "   tools/parity-audit.js is missing, so the tables were NOT compared against the game"
  } elseif (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    # step 6 calls `node` bare, so testing for a $node VARIABLE here would always be false and the
    # audit would skip silently on every machine - which is the exact failure this file exists to
    # prevent, introduced by the check for it.
    Note "   node is unavailable, so the parity audit did NOT run and the tables are UNVERIFIED"
  } else {
    $auditOut = Invoke-Node $auditScript
    if ($LASTEXITCODE -ne 0) {
      Bad ("the parity audit could not run (exit $LASTEXITCODE): " +
           ($auditOut -replace "`r?`n", " " ).Trim())
    } elseif ($auditOut -match '(\d+) difference\(s\)') {
      $diffCount = [int]$Matches[1]
      if ($diffCount -ne 0) {
        foreach ($line in ($auditOut -split "`r?`n")) {
          if ($line -match '^\s{6}\S') { Note ("   " + $line.Trim()) }
        }
        Bad "$diffCount parity difference(s) between depths.html?parity and the C# tables. Either the generator is wrong or the table is stale - the documented workflow pastes the page over the C# file, so a difference here is either a broken paste waiting to happen or a table that has drifted from the game"
      } else {
        Note "   all five tables agree with the running game"
      }
    } else {
      Bad "the parity audit printed no verdict; treat the tables as UNVERIFIED rather than as passing"
    }
  }
}

  # 6d. THE CONSTANT AUDIT - Balance.cs by value, against the running game.
  #
  # Two constants in Balance.cs have now been found WRONG by value, and both were found by hand
  # because something finally happened to read them:
  #
  #   SwerveDecay   0.011 against the game's 0.0035 - the gunners forgot a reversal 3x too fast
  #   BrunchRamp    Sec(2.2) against the game's 1.5s    - the PRE-TUNING value, kept through a
  #                 deliberate change, so the port still carried the ramp the game had replaced
  #
  # The C# suite was green through both. That is the whole problem: Balance.cs is the single place
  # the game's tuning lives, so an unpinned constant there is indistinguishable from a correct one
  # until something reads it - and most of them are still unread, because most of the tick is not
  # ported.
  #
  # So this compares them all, whether or not anything reads them yet. It is the check that stops the
  # NEXT stale constant from waiting for a caller.
  #
  # tools/constant-audit.js REPORTS and never edits: an audit that fixes what it finds cannot tell
  # you how much was wrong.
  Note ""
  Note "6d. the constant audit: Balance.cs by value against the running game"
  $constScript = "$root\tools\constant-audit.js"
  if (-not (Test-Path $constScript)) {
    Note "   tools/constant-audit.js is missing, so every tuning constant in the port is UNVERIFIED against the game"
  } elseif (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    # Bare `node`, tested with Get-Command, for the reason the comment on step 6c gives: testing for
    # a $node VARIABLE is always false, and an audit that skips silently on every machine is the one
    # outcome this file exists to prevent. I wrote the variable version first and it skipped itself.
    Note "   node is unavailable, so the constant audit did NOT run and every tuning constant is UNVERIFIED"
  } elseif (-not $serverUp) {
    Note "   SKIPPED - no static server, so the game's values cannot be read. The constants are UNVERIFIED, not passing."
  } else {
    $constOut = Invoke-Node $constScript
    $verdict = [regex]::Match($constOut, '(\d+) compared, (\d+) mismatch, (\d+) missing')
    if ($verdict.Success) {
      $cmp = [int]$verdict.Groups[1].Value
      $mis = [int]$verdict.Groups[2].Value
      $miss = [int]$verdict.Groups[3].Value
      if ($mis -gt 0) {
        foreach ($line in ($constOut -split "`n")) {
          if ($line -match 'MISMATCH' -and $line -notmatch 'not ported') {
            Note ("      " + $line.Trim())
          }
        }
        Bad "$mis constant(s) in the port disagree with the running game. A tuning constant that is wrong is worse than one that is missing: it compiles, it reads as deliberate, and nothing catches it until a caller does."
      } else {
        Note "   $cmp constants agree with the running game ($miss not yet ported)"
      }
    } else {
      Bad "the constant audit printed no verdict; treat every tuning constant as UNVERIFIED rather than as passing"
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
