namespace MinimalApiCrankDemo.Api.Configuration;

internal static class BuilderConfigurationExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public void ConfigureBuilder()
        {
            builder.RegisterOpenApi();
            builder.RegisterDatabase();
            builder.RegisterAuthentication();
            builder.RegisterCors();
            builder.RegisterRateLimiting();
            builder.RegisterHealthChecks();
            builder.RegisterProblemDetails();
            builder.RegisterForwardedHeaders();
            builder.RegisterLogging();
            builder.RegisterValidation();
        }

        public void RegisterOpenApi()
        {
            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
        }

        public void RegisterAuthentication()
        {
            builder.Services
                .AddIdentityApiEndpoints<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = false;
                })
                .AddRoles<IdentityRole<int>>()
                .AddEntityFrameworkStores<CrankDemoDbContext>();

            builder.Services.AddAuthorization();
        }

        public void RegisterCors()
        {
            var allowedOrigins = builder
                .Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowLocalDevelopment",
                    policy =>
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
            });
        }

        public void RegisterRateLimiting()
        {
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("fixed",
                    limiter =>
                    {
                        limiter.PermitLimit = 100;
                        limiter.Window = TimeSpan.FromSeconds(10);
                        limiter.QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst;
                        limiter.QueueLimit = 10;
                    });
                options.RejectionStatusCode =
                    StatusCodes.Status429TooManyRequests;
            });
        }

        public void RegisterHealthChecks()
        {
            builder.Services
                .AddHealthChecks()
                // Liveness: only checks if the process is running
                // (used by Kubernetes liveness probe)
                .AddCheck("live",
                    () => HealthCheckResult.Healthy(),
                    tags: ["live"]);
            // Readiness: add dependency checks tagged "ready"
            // for Kubernetes readiness probe. Examples:
            //
            // SQL Server (AspNetCore.HealthChecks.SqlServer):
            // .AddSqlServer(builder.Configuration
            //     .GetConnectionString("DefaultConnection")!)
            //
            // PostgreSQL (AspNetCore.HealthChecks.NpgSql):
            // .AddNpgSql(builder.Configuration
            //     .GetConnectionString("DefaultConnection")!)
            //
            // Redis (AspNetCore.HealthChecks.Redis):
            // .AddRedis(builder.Configuration
            //     .GetConnectionString("Redis")!)
            //
            // RabbitMQ (AspNetCore.HealthChecks.RabbitMQ):
            // .AddRabbitMQ(builder.Configuration
            //     .GetConnectionString("RabbitMQ")!)
            //
            // MongoDB (AspNetCore.HealthChecks.MongoDb):
            // .AddMongoDb(builder.Configuration
            //     .GetConnectionString("MongoDB")!)
            //
            // Azure Cosmos DB (AspNetCore.HealthChecks.CosmosDb):
            // .AddCosmosDb(builder.Configuration
            //     .GetConnectionString("CosmosDb")!)
            //
            // Azure Service Bus
            // (AspNetCore.HealthChecks.AzureServiceBus):
            // .AddAzureServiceBusQueue(builder.Configuration
            //     .GetConnectionString("ServiceBus")!,
            //     "queue-name")
            //
            // Azure Blob Storage
            // (AspNetCore.HealthChecks.AzureBlobStorage):
            // .AddAzureBlobStorage(builder.Configuration
            //     .GetConnectionString("BlobStorage")!)
            //
            // Azure Queue Storage
            // (AspNetCore.HealthChecks.AzureStorage):
            // .AddAzureQueueStorage(builder.Configuration
            //     .GetConnectionString("QueueStorage")!)
            //
            // Azure Key Vault
            // (AspNetCore.HealthChecks.AzureKeyVault):
            // .AddAzureKeyVault(
            //     new Uri(builder.Configuration["KeyVault:Uri"]!),
            //     new DefaultAzureCredential(),
            //     options => { })
            //
            // External HTTP dependency reachability:
            // .AddUrlGroup(
            //     new Uri("https://external-service/health"),
            //     "external-service")
            //
            // Memory (fails if process exceeds 512 MB):
            // .AddProcessAllocatedMemoryHealthCheck(512);
        }

        public void RegisterProblemDetails() =>
            builder.Services.AddProblemDetails();

        public void RegisterForwardedHeaders()
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto;

                // Uncomment the lines below when ALL of the
                // following are true:
                //   - Running behind a reverse proxy
                //     (nginx, Kubernetes ingress,
                //     AWS ALB, Azure Front Door)
                //   - The proxy is the sole entry point (pods are
                //     not directly reachable from the internet)
                //   - Traffic arrives from a non-loopback IP
                //     (e.g., Kubernetes ClusterIP,
                //     internal VPC address)
                //
                // Common scenarios:
                //   - Kubernetes: ingress controller reaches pods
                //     via ClusterIP — not loopback, so headers are
                //     ignored by default. Clearing these lets any
                //     in-cluster proxy forward headers.
                //   - Docker Compose: nginx container forwards to
                //     app container via bridge network IP.
                //   - Cloud load balancers: AWS ALB, Azure App
                //     Gateway, Cloudflare — all use non-loopback
                //     IPs.
                //
                // Do NOT uncomment if pods/containers are directly
                // reachable from the internet — an attacker could
                // spoof X-Forwarded-For to fake their IP.
                // options.KnownNetworks.Clear();
                // options.KnownProxies.Clear();
            });
        }

        public void RegisterValidation() => builder.Services.AddValidation();

        // ---- Customize these for your application ----

        public void RegisterLogging()
        {
            builder.Host.UseSerilog((ctx, config) =>
                config.ReadFrom.Configuration(ctx.Configuration));

            // Serilog is configured via the "Serilog" section in
            // appsettings.json.
            // Active sinks: Console + File (rolling daily, 10-day
            // retention).
            // To add more sinks, install the NuGet package and add
            // to "Using" + "WriteTo" in appsettings.json:
            //
            // Seq (local structured log server — great for development):
            //   Package : Serilog.Sinks.Seq
            //   WriteTo : { "Name": "Seq", "Args": {
            //     "serverUrl": "http://localhost:5341" } }
            //
            // Azure Application Insights:
            //   Package : Serilog.Sinks.ApplicationInsights
            //   WriteTo : { "Name": "ApplicationInsights",
            //     "Args": { "connectionString": "<conn-str>",
            //       "telemetryConverter": "<see pkg docs>" } }
            //
            // Azure Blob Storage:
            //   Package : Serilog.Sinks.AzureBlobStorage
            //   WriteTo : { "Name": "AzureBlobStorage",
            //     "Args": { "connectionString": "<conn-str>",
            //       "storageContainerName": "logs",
            //       "storageFileName": "api-.log" } }
            //
            // Azure Cosmos DB:
            //   Package : Serilog.Sinks.AzureCosmosDB
            //   WriteTo : { "Name": "AzureCosmosDB",
            //     "Args": { "endpointUri": "<endpoint>",
            //       "authorizationKey": "<key>" } }
            //
            // Elasticsearch / Kibana (ELK stack):
            //   Package : Serilog.Sinks.Elasticsearch
            //   WriteTo : { "Name": "Elasticsearch",
            //     "Args": { "nodeUris": "http://localhost:9200",
            //       "indexFormat": "api-{0:yyyy.MM.dd}" } }
            //
            // Grafana Loki:
            //   Package : Serilog.Sinks.Grafana.Loki
            //   WriteTo : { "Name": "GrafanaLoki",
            //     "Args": { "uri": "http://localhost:3100" } }
            //
            // Datadog:
            //   Package : Serilog.Sinks.Datadog.Logs
            //   WriteTo : { "Name": "DatadogLogs",
            //     "Args": { "apiKey": "<api-key>" } }
            //
            // SQL Server:
            //   Package : Serilog.Sinks.MSSqlServer
            //   WriteTo : { "Name": "MSSqlServer",
            //     "Args": { "connectionString": "<conn-str>",
            //       "tableName": "Logs",
            //       "autoCreateSqlTable": true } }
            //
            // MongoDB:
            //   Package : Serilog.Sinks.MongoDB
            //   WriteTo : { "Name": "MongoDB",
            //     "Args": { "databaseUrl": "<conn-str>",
            //       "collectionName": "logs" } }
        }

        public void RegisterDatabase()
        {
            var connectionString = builder.Configuration
                .GetConnectionString("CrankDemo") ??
                throw new InvalidOperationException(
                    "ConnectionStrings:CrankDemo is required. " +
                    "Use scripts/Start-Api.ps1.");

            builder.Services.AddDbContext<CrankDemoDbContext>(options =>
                options.UseSqlServer(connectionString));
        }
    }
}
