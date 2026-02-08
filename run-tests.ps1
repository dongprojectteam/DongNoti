# DongNoti Unit Test Runner with Coverage
# Usage: .\run-tests.ps1
# Options:
#   -Coverage: Generate coverage report (default: true)
#   -Html: Generate HTML report (requires ReportGenerator)
#   -Verbose: Show detailed output

param(
    [switch]$Coverage = $true,
    [switch]$Html = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$TestProject = "$ProjectRoot\DongNoti.Tests\DongNoti.Tests.csproj"
$ResultsDir = "$ProjectRoot\TestResults"
$CoverageReportDir = "$ProjectRoot\CoverageReport"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DongNoti Unit Test Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean previous results
if (Test-Path $ResultsDir) {
    Write-Host "Cleaning previous test results..." -ForegroundColor Yellow
    Remove-Item -Path $ResultsDir -Recurse -Force
}

# Build test project
Write-Host "Building test project..." -ForegroundColor Yellow
dotnet build $TestProject -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded." -ForegroundColor Green
Write-Host ""

# Run tests
Write-Host "Running tests..." -ForegroundColor Yellow
if ($Coverage) {
    $testArgs = @(
        "test",
        $TestProject,
        "--no-build",
        "--collect:XPlat Code Coverage",
        "--results-directory", $ResultsDir,
        "--logger", "trx",
        "--settings", "$ProjectRoot\DongNoti.runsettings"
    )
    
    if ($Verbose) {
        $testArgs += "--verbosity", "normal"
    } else {
        $testArgs += "--verbosity", "minimal"
    }
    
    & dotnet @testArgs
} else {
    $testArgs = @(
        "test",
        $TestProject,
        "--no-build"
    )
    
    if ($Verbose) {
        $testArgs += "--verbosity", "normal"
    } else {
        $testArgs += "--verbosity", "minimal"
    }
    
    & dotnet @testArgs
}

$testExitCode = $LASTEXITCODE

Write-Host ""
if ($testExitCode -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host "Some tests failed." -ForegroundColor Red
}

# Generate HTML coverage report
if ($Coverage -and $Html) {
    Write-Host ""
    Write-Host "Generating HTML coverage report..." -ForegroundColor Yellow
    
    # Find coverage file
    $coverageFile = Get-ChildItem -Path $ResultsDir -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
    
    if ($coverageFile) {
        # Check if reportgenerator is installed
        $reportGen = Get-Command reportgenerator -ErrorAction SilentlyContinue
        
        if ($reportGen) {
            if (Test-Path $CoverageReportDir) {
                Remove-Item -Path $CoverageReportDir -Recurse -Force
            }
            
            reportgenerator `
                -reports:$($coverageFile.FullName) `
                -targetdir:$CoverageReportDir `
                -reporttypes:Html
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Coverage report generated: $CoverageReportDir\index.html" -ForegroundColor Green
                
                # Open in browser
                $indexPath = "$CoverageReportDir\index.html"
                if (Test-Path $indexPath) {
                    Write-Host "Opening report in browser..." -ForegroundColor Cyan
                    Start-Process $indexPath
                }
            }
        } else {
            Write-Host "ReportGenerator not found. Install with:" -ForegroundColor Yellow
            Write-Host "  dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor White
        }
    } else {
        Write-Host "Coverage file not found." -ForegroundColor Yellow
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($Coverage) {
    $coverageFile = Get-ChildItem -Path $ResultsDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($coverageFile) {
        Write-Host "Coverage file: $($coverageFile.FullName)" -ForegroundColor White
        
        # Parse coverage summary from XML
        try {
            [xml]$coverageXml = Get-Content $coverageFile.FullName
            $lineRate = [math]::Round([double]$coverageXml.coverage.'line-rate' * 100, 2)
            $branchRate = [math]::Round([double]$coverageXml.coverage.'branch-rate' * 100, 2)
            
            Write-Host ""
            Write-Host "Line Coverage:   $lineRate%" -ForegroundColor $(if ($lineRate -ge 80) { "Green" } elseif ($lineRate -ge 60) { "Yellow" } else { "Red" })
            Write-Host "Branch Coverage: $branchRate%" -ForegroundColor $(if ($branchRate -ge 80) { "Green" } elseif ($branchRate -ge 60) { "Yellow" } else { "Red" })
        } catch {
            Write-Host "Could not parse coverage summary." -ForegroundColor Yellow
        }
    }
}

Write-Host ""
exit $testExitCode
