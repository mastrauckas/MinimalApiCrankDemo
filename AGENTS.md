# Repository Agent Instructions

## Package Management

Before changing dependency versions in any implementation, run:

```powershell
dotnet list .\01-inefficient\MinimalApiPerformanceDemo.slnx package --outdated
dotnet list .\01-inefficient\MinimalApiPerformanceDemo.slnx package --vulnerable --include-transitive
```

Resolve reported vulnerabilities and apply only upgrades compatible with the
solution target framework. Restore, build, and test after upgrades.
