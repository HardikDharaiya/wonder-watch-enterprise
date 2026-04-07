#!/bin/bash
# ════════════════════════════════════════════════════════════════════
# WONDER WATCH — SOLUTION & N-TIER ARCHITECTURE SCAFFOLDING (F1)
# ════════════════════════════════════════════════════════════════════

echo "Initializing Wonder Watch Enterprise Solution..."

# 1. Create Solution
dotnet new sln -n WonderWatch

# 2. Create Projects
dotnet new classlib -n WonderWatch.Domain
dotnet new classlib -n WonderWatch.Infrastructure
dotnet new classlib -n WonderWatch.Application
dotnet new mvc -n WonderWatch.Web
dotnet new xunit -n WonderWatch.Tests

# 3. Add Projects to Solution
dotnet sln WonderWatch.sln add WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet sln WonderWatch.sln add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj
dotnet sln WonderWatch.sln add WonderWatch.Application/WonderWatch.Application.csproj
dotnet sln WonderWatch.sln add WonderWatch.Web/WonderWatch.Web.csproj
dotnet sln WonderWatch.sln add WonderWatch.Tests/WonderWatch.Tests.csproj

# 4. Establish Clean Architecture Dependencies (Strict N-Tier)
# Application depends on Domain
dotnet add WonderWatch.Application/WonderWatch.Application.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj

# Infrastructure depends on Application and Domain
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj reference WonderWatch.Application/WonderWatch.Application.csproj

# Web depends on Application and Infrastructure
dotnet add WonderWatch.Web/WonderWatch.Web.csproj reference WonderWatch.Application/WonderWatch.Application.csproj
dotnet add WonderWatch.Web/WonderWatch.Web.csproj reference WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj

# Tests depend on all layers
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Application/WonderWatch.Application.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Web/WonderWatch.Web.csproj

# 5. Clean up default boilerplate classes
rm WonderWatch.Domain/Class1.cs
rm WonderWatch.Infrastructure/Class1.cs
rm WonderWatch.Application/Class1.cs

# 6. Add Core NuGet Packages (Versions locked to .NET 8.0)
# Domain Layer (Zero external dependencies - pure POCOs)

# Application Layer
dotnet add WonderWatch.Application/WonderWatch.Application.csproj package FluentValidation -v 11.9.0
dotnet add WonderWatch.Application/WonderWatch.Application.csproj package Microsoft.AspNetCore.Http.Features -v 5.0.17

# Infrastructure Layer
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer -v 8.0.0
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools -v 8.0.0
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore -v 8.0.0

# Web Layer
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package Microsoft.EntityFrameworkCore.Design -v 8.0.0
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package Razorpay -v 3.1.1
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package Serilog.AspNetCore -v 8.0.0
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package Serilog.Sinks.ApplicationInsights -v 4.0.0
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package SixLabors.ImageSharp -v 3.1.2
dotnet add WonderWatch.Web/WonderWatch.Web.csproj package LigerShark.WebOptimizer.Core -v 3.0.405

echo "Scaffolding Complete. Clean Architecture boundaries enforced."