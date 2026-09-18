#requires -Version 7
param (
  [parameter(mandatory = $true)]
  [string]$TestPath,
  [parameter(mandatory = $true)]
  [string]$ReportDirectoryPath
)
Set-StrictMode -Version Latest
$InformationPreference = 'Continue'

<# Global #>
# Check path of system under test.
if (-Not (Test-Path -Path $TestPath)) {
  throw 'Cannot find test path.'
}
[string]$TestDirectoryPath = Split-Path -Path $TestPath -Parent
[string]$SourceDirectoryPath = Join-Path $TestDirectoryPath 'src'

# Check path of test coverage settings.
[string]$CoverletRunSettingsPath = Join-Path $TestDirectoryPath '.runsettings'
if (-Not (Test-Path -Path $CoverletRunSettingsPath -PathType leaf)) {
  throw 'Cannot find coverlet run settings file.'
}

<# Main #>
# Set up report results path.
if (-Not (Test-Path -Path $ReportDirectoryPath)) {
  $null = New-Item -ItemType 'Directory' -Path $ReportDirectoryPath
}
[string]$ResultsDirectoryPath = Join-Path $ReportDirectoryPath 'Results'
if (-Not (Test-Path -Path $ResultsDirectoryPath)) {
  $null = New-Item -ItemType 'Directory' -Path $ResultsDirectoryPath
}

# Execute unit tests.
$TestArgs = @(
  'test', $TestPath,
  '/property:GenerateFullPaths=true',
  '/consoleloggerparameters:NoSummary',
  "--results-directory:$ResultsDirectoryPath",
  "--settings:$CoverletRunSettingsPath"
)
Write-Information "Test args: $($TestArgs | ConvertTo-Json)"
& dotnet $TestArgs
if ($LastExitCode -ne 0) {
  exit 1
}

# Update test coverage report.
$ReportGeneratorArgs = @(
  'reportgenerator',
  "-title:ReverseProxy",
  "-reports:$ResultsDirectoryPath/**/coverage.cobertura.xml",
  "-targetdir:$ReportDirectoryPath",
  "-sourcedirs:$SourceDirectoryPath",
  '-reporttypes:HtmlInline_AzurePipelines'
)
Write-Information "Report generator args: $($ReportGeneratorArgs | ConvertTo-Json)"
& dotnet $ReportGeneratorArgs
