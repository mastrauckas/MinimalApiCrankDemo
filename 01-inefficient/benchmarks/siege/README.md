# Siege benchmark

This benchmark exercises the same authenticated `GET /api/products` endpoint,
API, and disposable benchmark database as Crank. It does not contain another
application.

Siege does not run natively on Windows. `Dockerfile` installs it in a small
Alpine Linux image, built automatically by `scripts/Invoke-SiegeOnly.ps1`.

From `01-inefficient`, run either:

```powershell
# Create the benchmark database, migrate, seed, start the API, and run Siege.
.\scripts\Invoke-BenchmarkSetupAndSiege.ps1

# Reuse an already prepared benchmark database.
.\scripts\Invoke-SiegeOnly.ps1
```

The default run has 32 concurrent Siege users, a 5-second warmup, and a
15-second measured duration. The runner writes measured Siege output to
`artifacts/benchmarks/siege/`.
