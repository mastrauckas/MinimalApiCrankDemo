# Database setup SQL

`001-recreate-database.sql` removes and recreates the disposable `CrankDemo`
database. It creates no schema and inserts no data.

The Database project owns schema-only EF Core migrations. After migrations,
the PowerShell seed commands run the idempotent SQLCMD files under:

- `integration/001-seed-data.sql`
- `benchmark/001-seed-data.sql`

These SQL files are test/benchmark tooling and are not compiled into any
application project.
