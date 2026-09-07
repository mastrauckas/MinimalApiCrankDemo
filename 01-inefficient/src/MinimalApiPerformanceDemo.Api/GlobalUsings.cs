global using System.ComponentModel.DataAnnotations;
global using System.Security.Claims;
global using System.Threading.RateLimiting;

global using Microsoft.AspNetCore.Diagnostics.HealthChecks;
global using Microsoft.AspNetCore.Http.HttpResults;
global using Microsoft.AspNetCore.HttpOverrides;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.RateLimiting;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;

global using MinimalApiPerformanceDemo.Api.Configuration;
global using MinimalApiPerformanceDemo.Api.Endpoints;
global using MinimalApiPerformanceDemo.Api.Logging;
global using MinimalApiPerformanceDemo.Api.Middleware;
global using MinimalApiPerformanceDemo.Application.Dtos;
global using MinimalApiPerformanceDemo.Application.Repositories;
global using MinimalApiPerformanceDemo.Application.Services;
global using MinimalApiPerformanceDemo.Database;
global using MinimalApiPerformanceDemo.Database.Entities;
global using MinimalApiPerformanceDemo.Infrastructure.Repositories;

global using Scalar.AspNetCore;

global using Serilog;

global using ILogger = Microsoft.Extensions.Logging.ILogger;
global using SerilogLogger = Serilog.ILogger;
