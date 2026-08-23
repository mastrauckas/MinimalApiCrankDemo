# Database initialization

`001-recreate-database.sql` removes and recreates the disposable database.
The integration setup then applies the EF Core migrations in
`src/MinimalApiCrankDemo.Api/Data/Migrations` and runs the idempotent Identity
and catalog seeder in `DatabaseInitializer.cs`.
