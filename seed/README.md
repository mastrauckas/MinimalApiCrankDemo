# Database initialization

`001-recreate-database.sql` removes and recreates the disposable database.
Integration and benchmark setup then apply the EF Core migrations in
`src/MinimalApiCrankDemo.Api/Data/Migrations`. The test-owned idempotent
Identity and catalog seeder is under
`tests/MinimalApiCrankDemo.Api.IntegrationTests/Seeding` and is not compiled
into the API project.

The SQL script only recreates the database. It does not create application
tables or insert demo rows.
