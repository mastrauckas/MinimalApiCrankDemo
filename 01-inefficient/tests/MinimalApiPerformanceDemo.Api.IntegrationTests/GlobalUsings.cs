global using System.Diagnostics;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Security.Cryptography;

global using Microsoft.AspNetCore.Authentication.BearerToken;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Identity.Data;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

global using MinimalApiPerformanceDemo.Application.Dtos;
global using MinimalApiPerformanceDemo.Database;

global using Snapshooter.Xunit;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
