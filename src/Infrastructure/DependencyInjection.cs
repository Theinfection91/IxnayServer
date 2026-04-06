using IxnayServer.Application.Common.Interfaces;
using IxnayServer.Infrastructure.Data;
using IxnayServer.Infrastructure.Data.Interceptors;
using IxnayServer.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            // Use MySQL when connection string looks like MySQL, otherwise fallback to SQLite.
            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Uid=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
            else
            {
                options.UseSqlite(connectionString);
            }

            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            })
            .AddBearerToken(IdentityConstants.BearerScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
            });

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();
    }
}
