using System.Security.Claims;
using System.Text;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Communication.Email;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.Data.Attachments;
using Atlas.Infrastructure.Data.History;
using Atlas.Infrastructure.Data.History.Comparison;
using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Generation;
using Atlas.Infrastructure.Data.History.Inspection;
using Atlas.Infrastructure.Data.History.Management;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Planning;
using Atlas.Infrastructure.Data.History.Reading;
using Atlas.Infrastructure.Data.Initializers.Platform;
using Atlas.Infrastructure.Data.Interceptors;
using Atlas.Infrastructure.Identity;
using Atlas.Infrastructure.Observability;
using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        // =========================================================================
        // 1. Configuração do Log Estruturado Centralizado (Serilog)
        // =========================================================================
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "atlas-log-.txt");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Services.AddSerilog();

        // =========================================================================
        // 2. Serviços de Observabilidade e Sanitização de Logs
        // =========================================================================
        builder.Services.AddSingleton<ILogSanitizer, LogSanitizer>();

        // 3. Busca a Connection String
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);

        Guard.Against.Null(
            connectionString,
            message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddSingleton<IDapperContext>(new DapperContext(connectionString));

        // 4. Registra os Interceptors (Gatilhos do Entity Framework)
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        // 4.1. Serviços de Gerenciamento e Resolução de Histórico de Entidades
        builder.Services.AddScoped<IEntityHistoryModelResolver, EntityHistoryModelResolver>();
        builder.Services.AddSingleton<IEntityHistorySqlGenerator, PostgreSqlEntityHistorySqlGenerator>();
        builder.Services.AddScoped<IEntityHistorySchemaInspector, PostgreSqlEntityHistorySchemaInspector>();
        builder.Services.AddSingleton<IEntityHistorySchemaComparer, EntityHistorySchemaComparer>();
        builder.Services.AddScoped<IEntityHistoryProvisioningExecutor, PostgreSqlEntityHistoryProvisioningExecutor>();
        builder.Services.AddScoped<IEntityHistoryManager, EntityHistoryManager>();
        builder.Services.AddScoped<IEntityHistoryInitializer, EntityHistoryInitializer>();
        builder.Services.AddScoped<IEntityHistorySnapshotReader, PostgreSqlEntityHistorySnapshotReader>();
        builder.Services.AddSingleton<IEntityHistorySnapshotComparer, EntityHistorySnapshotComparer>();
        builder.Services.AddScoped<IEntityHistoryQueryService, EntityHistoryQueryService>();

        // 4.2. Serviços Transversais de Arquivos e Anexos
        builder.Services.AddScoped<IEntityAttachmentService, EntityAttachmentService>();

        // 5. Configura o DbContext (PostgreSQL)
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        // 6. Mapeia a Interface para a implementação concreta
        builder.Services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // 7. Registra o Initialiser
        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        // 7.1. Inicializador de Metadados Automático
        builder.Services.AddScoped<IEntityMetadataInitializer, EntityMetadataInitializer>();

        // 7.2. Inicializador de Ícones do Sistema
        builder.Services.AddScoped<IIconInitializer, IconInitializer>();

        // =========================================================================
        // Registramos os serviços utilitários ANTES da segurança
        // para que os eventos de validação do token/cookie encontrem os Services em runtime.
        // =========================================================================
        // 8. Serviços Utilitários, Comunicação e Core de Segurança
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
        builder.Services.AddScoped<IEmailMessageService, EmailMessageService>();
        builder.Services.AddHostedService<EmailProcessingWorker>();

        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ISystemParameterProvider, SystemParameterProvider>();

        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserSessionService, UserSessionService>();
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<
            IUserClaimsPrincipalFactory<ApplicationUser>,
            ApplicationUserClaimsPrincipalFactory>();
        builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // 9. Política de Senhas, Bloqueio e Usuários (IdentityOptions)
        builder.Services.Configure<IdentityOptions>(options =>
        {
            // Regras de Senha
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // Regras de Bloqueio (Lockout) de Conta
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5; // Bloqueia após 5 tentativas falhas
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // Bloqueia por 15 minutos

            // Regras Gerais de Usuário
            options.User.RequireUniqueEmail = false;
        });

        // =========================================================================
        // PASSO CRÍTICO: Definimos a Autenticação Híbrida PRIMEIRO
        // =========================================================================
        // 10. Configuração da Autenticação Híbrida (JWT Bearer e Roteador de Esquemas)
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var key = builder.Configuration["Jwt:Key"];

        Guard.Against.NullOrWhiteSpace(issuer, message: "Jwt:Issuer not found.");
        Guard.Against.NullOrWhiteSpace(audience, message: "Jwt:Audience not found.");
        Guard.Against.NullOrWhiteSpace(key, message: "Jwt:Key not found.");

        builder.Services
            .AddAuthentication(options =>
            {
                // Sobrescreve globalmente para que o nosso seletor intercepte tudo antes do Identity
                options.DefaultAuthenticateScheme = "MixedAuthSchema";
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("MixedAuthSchema", "MixedAuthSchema", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    string authorization = context.Request.Headers.Authorization.ToString();

                    if (!string.IsNullOrEmpty(authorization) &&
                        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    return IdentityConstants.ApplicationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var sessionId = context.Principal?.FindFirstValue(CustomClaimTypes.SessionId);

                        if (string.IsNullOrWhiteSpace(sessionId))
                        {
                            context.Fail("Sessão inválida.");
                            return;
                        }

                        var sessionService = context.HttpContext.RequestServices
                            .GetRequiredService<IUserSessionService>();

                        var isActive = await sessionService.IsSessionActiveAsync(sessionId);

                        if (!isActive)
                        {
                            context.Fail("Sessão expirada ou revogada.");
                        }
                    }
                };
            });

        // =========================================================================
        // PASSO SEGUINTE: Acoplamos o Identity por cima da nossa Autenticação base
        // =========================================================================
        // 11. Configuração de Autenticação e Identity Api Endpoints
        builder.Services
            .AddIdentityApiEndpoints<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // 11.1. Serviço de gerenciamento de papéis do Identity
        builder.Services.AddScoped<IRoleService, RoleService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();

        // 12. Customização do Comportamento do Cookie gerado pelo Identity acima
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Atlas.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;

            options.Events.OnValidatePrincipal = async context =>
            {
                var sessionId = context.Principal?.FindFirstValue(CustomClaimTypes.SessionId);

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }

                var sessionService = context.HttpContext.RequestServices
                    .GetRequiredService<IUserSessionService>();

                var isActive = await sessionService.IsSessionActiveAsync(sessionId);

                if (!isActive)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });

        // 13. Políticas de Autorização baseadas em Claims (Permissions)
        var authorizationBuilder = builder.Services.AddAuthorizationBuilder();

        foreach (var permission in PermissionKeys.All())
        {
            authorizationBuilder.AddPolicy(permission, policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireAssertion(async context =>
                {
                    // Tenta acessar o HttpContext para injetar dependências
                    var httpContext = context.Resource as HttpContext;

                    // Se não houver HttpContext, segue a regra padrão de claim
                    if (httpContext == null)
                    {
                        return context.User.HasClaim(c => c.Type == CustomClaimTypes.Permission && c.Value == permission);
                    }

                    // Resolve o serviço em tempo real
                    var identityService = httpContext.RequestServices.GetRequiredService<IIdentityService>();

                    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    // Se for Superusuário (bypass), libera o acesso
                    if (long.TryParse(userIdString, out var userId))
                    {
                        if (await identityService.IsSuperUserAsync(userId))
                        {
                            return true;
                        }
                    }

                    // Se não for superusuário, verifica a permissão específica
                    return context.User.HasClaim(c => c.Type == CustomClaimTypes.Permission && c.Value == permission);
                });
            });
        }
    }
}
