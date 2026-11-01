param()
$ErrorActionPreference = "Continue"
$cli  = "dotnet run --project apps/Math.Fuzzy.Cli --"
# datasets/ at the repository root, two levels above src/dotnet
$data = Join-Path $PSScriptRoot "..\..\datasets"

$results = @()

function Run-Test {
    param($Label, $Output, $Pattern)
    $joined = $Output -join "`n"
    if ($joined -match $Pattern) {
        Write-Host ("  [PASS] " + $Label) -ForegroundColor Green
        $script:results += "PASS"
    } else {
        Write-Host ("  [FAIL] " + $Label + "  (wanted: " + $Pattern + ")") -ForegroundColor Red
        $script:results += "FAIL"
    }
}

function Show-Warn {
    param($Label, $Note)
    Write-Host ("  [WARN] " + $Label + " -- " + $Note) -ForegroundColor Yellow
    $script:results += "WARN"
}

# ---- CH2 EXPERIMENTS --------------------------------------------------------
Write-Host ""
Write-Host "=== CH2 EXPERIMENTS (tbl-experiments) ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "[Iris / D-AFC(3) Euclidean]"
$out = Invoke-Expression ("$cli cluster --algorithm dafc --input `"$data\iris_labeled.csv`" --clusters 3 --distance euclidean 2>&1")
$out | Where-Object { $_ -match "Purity|Adjusted|Best" } | Write-Host
Run-Test "Purity >= 0.95"              $out "Purity\s+0\.9[5-9]"
Run-Test "Adjusted Rand Index >= 0.88" $out "Adjusted Rand Index\s+0\.(8[89]|9\d)"

Write-Host ""
Write-Host "[Seeds / D-AFC(3) Euclidean]"
$out = Invoke-Expression ("$cli cluster --algorithm dafc --input `"$data\seeds_labeled.csv`" --clusters 3 --distance euclidean 2>&1")
$out | Where-Object { $_ -match "Purity|Adjusted|Best" } | Write-Host
Run-Test "Purity >= 0.91"              $out "Purity\s+0\.9[1-9]"
Run-Test "Adjusted Rand Index >= 0.84" $out "Adjusted Rand Index\s+0\.(8[4-9]|9\d)"

Write-Host ""
Write-Host "[Ruspini / D-PAFC Euclidean]"
$nRuspini = (Get-Content "$data\ruspini.csv").Count - 1
if ($nRuspini -ne 75) { Show-Warn "Ruspini n" ("file has " + $nRuspini + " rows, book says 75") }
$out = Invoke-Expression ("$cli cluster --algorithm dpafc --input `"$data\ruspini.csv`" --distance euclidean 2>&1")
$out | Select-Object -First 8 | Write-Host
Run-Test "c=4 solution found" $out "c=4|Best: c=4"

# ---- CH7 WORKED EXAMPLES ----------------------------------------------------
Write-Host ""
Write-Host "=== CH7 WORKED EXAMPLES ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "[Sneath-Sokal / D-AFC(2) Euclidean]"
$nSS = (Get-Content "$data\sneath_sokal.csv").Count - 1
if ($nSS -ne 17) { Show-Warn "Sneath-Sokal n" ("file has " + $nSS + " rows, book says 17") }
$out = Invoke-Expression ("$cli cluster --algorithm dafc --input `"$data\sneath_sokal.csv`" --clusters 2 --distance euclidean 2>&1")
$out | Select-Object -First 12 | Write-Host
Run-Test "c=2 solution found"  $out "Best: c=2|Best:.*c=2"
Run-Test "alpha* near 0.929"   $out "0\.92[0-9]"

Write-Host ""
Write-Host "[Iris / D-AFC(3) alpha* check for FIS chapter]"
$out = Invoke-Expression ("$cli cluster --algorithm dafc --input `"$data\iris_labeled.csv`" --clusters 3 --distance euclidean 2>&1")
Run-Test "alpha* in 0.7x range" $out "Best:.*0\.7"

# ---- CH3 PROTOTYPE ----------------------------------------------------------
Write-Host ""
Write-Host "=== CH3 PROTOTYPE (D-AFC-TC / D-PAFC-TC) ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "[Iris / D-AFC-TC Euclidean]"
$out = Invoke-Expression ("$cli cluster --algorithm dafc-tc --input `"$data\iris_labeled.csv`" --distance euclidean 2>&1")
$out | Where-Object { $_ -match "Purity|Adjusted|NMI|Best" } | Write-Host
Run-Test "Purity >= 0.88"  $out "Purity\s+0\.(8[89]|9\d)"

Write-Host ""
Write-Host "[Iris / D-PAFC-TC Euclidean]"
$out = Invoke-Expression ("$cli cluster --algorithm dpafc-tc --input `"$data\iris_labeled.csv`" --distance euclidean 2>&1")
$out | Where-Object { $_ -match "Purity|Adjusted|NMI|Best" } | Write-Host
Run-Test "Returns a result" $out "Best:"

# ---- CH9 PERFORMANCE --------------------------------------------------------
Write-Host ""
Write-Host "=== CH9 PERFORMANCE (no budget error) ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "[Iris / D-AFC-TC c=5 -- previously threw budget error]"
$out = Invoke-Expression ("$cli cluster --algorithm dafc-tc --input `"$data\iris.csv`" --clusters 5 --distance euclidean 2>&1")
if (($out -join "`n") -match "exceed|InvalidOperation|safety limit") {
    Write-Host ("  [FAIL] Budget error still thrown") -ForegroundColor Red
    $script:results += "FAIL"
} else {
    Write-Host ("  [PASS] No budget error") -ForegroundColor Green
    $script:results += "PASS"
}

# ---- SUMMARY ----------------------------------------------------------------
Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
$pass = ($results | Where-Object { $_ -eq "PASS" }).Count
$fail = ($results | Where-Object { $_ -eq "FAIL" }).Count
$warn = ($results | Where-Object { $_ -eq "WARN" }).Count
Write-Host ("PASS: " + $pass + "   FAIL: " + $fail + "   WARN: " + $warn)
if ($fail -gt 0) { exit 1 }
