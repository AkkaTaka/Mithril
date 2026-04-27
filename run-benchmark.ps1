param(
  [ValidateSet("All", "NativeMemoryPool", "NativeMemoryPoolParallel")]
  [string]$Target
)

$filterMap = @{
  "All"                     = "*"
  "NativeMemoryPool"        = "Mithril.Benchmarks.NativeMemoryPoolBenchmark.*"
  "NativeMemoryPoolParallel"= "Mithril.Benchmarks.NativeMemoryPoolParallelBenchmark.*"

}

if (-not $Target) {
  $options = @("All", "NativeMemoryPool", "NativeMemoryPoolParallel")

  Write-Host ""
  Write-Host "Select benchmark:" -ForegroundColor Cyan
  for ($i = 0; $i -lt $options.Count; $i++) {
    Write-Host "  [$($i + 1)] $($options[$i])"
  }
  Write-Host ""

  do {
    $input = Read-Host "Enter number (1-$($options.Count))"
    $index = $input -as [int]
  } while ($index -lt 1 -or $index -gt $options.Count)

  $Target = $options[$index - 1]
}

$filter  = $filterMap[$Target]
$project = "Benchmarks/Mithril.Benchmarks/Mithril.Benchmarks.csproj"

Write-Host ""
Write-Host "Target : $Target" -ForegroundColor Cyan
Write-Host "Filter : $filter"  -ForegroundColor Cyan
Write-Host ""

dotnet run -c Release -p:Platform=x64 --project $project -- --filter $filter
