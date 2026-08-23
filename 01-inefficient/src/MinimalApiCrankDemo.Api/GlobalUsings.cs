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

global using MinimalApiCrankDemo.Api.Configuration;
global using MinimalApiCrankDemo.Api.Endpoints;
global using MinimalApiCrankDemo.Api.Logging;
global using MinimalApiCrankDemo.Api.Middleware;
global using MinimalApiCrankDemo.Application.Dtos;
global using MinimalApiCrankDemo.Application.Repositories;
global using MinimalApiCrankDemo.Application.Services;
global using MinimalApiCrankDemo.Database;
global using MinimalApiCrankDemo.Database.Entities;
global using MinimalApiCrankDemo.Infrastructure.Repositories;

global using Scalar.AspNetCore;

global using Serilog;

global using ILogger = Microsoft.Extensions.Logging.ILogger;
global using SerilogLogger = Serilog.ILogger;
