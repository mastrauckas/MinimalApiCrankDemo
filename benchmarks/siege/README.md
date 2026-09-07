# Siege benchmark

This benchmark exercises the same authenticated `GET /api/products` endpoint,
API, and disposable benchmark database as Crank. It does not contain another
application.

Siege does not run natively on Windows. `Dockerfile` installs it in a small
Debian Linux image, built automatically by the shared C# tool.

The default run has 32 concurrent Siege users, a 5-second warmup, and a
15-second measured duration. The runner writes measured Siege output to
`artifacts/benchmarks/siege/`.
