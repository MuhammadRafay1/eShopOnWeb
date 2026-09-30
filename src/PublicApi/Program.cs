using System;
using System.Collections.Generic;
using System.Text;
using BlazorShared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Constants;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.Logging;
using Microsoft.eShopWeb.PublicApi;
using Microsoft.eShopWeb.PublicApi.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MinimalApi.Endpoint.Configurations.Extensions;
using MinimalApi.Endpoint.Extensions;
using Microsoft.eShopWeb.Infrastructure.Services.PayPal;
using PayPalServerSdk;
using PayPalServerSdk.Servers;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpoints();

// Use to force loading of appsettings.json of test project
builder.Configuration.AddConfigurationFile("appsettings.test.json");
builder.Logging.AddConsole();

Microsoft.eShopWeb.Infrastructure.Dependencies.ConfigureServices(builder.Configuration, builder.Services);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<AppIdentityDbContext>()
        .AddDefaultTokenProviders();

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
builder.Services.Configure<CatalogSettings>(builder.Configuration);
var catalogSettings = builder.Configuration.Get<CatalogSettings>() ?? new CatalogSettings();
builder.Services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));
builder.Services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
builder.Services.AddScoped<ITokenClaimsService, IdentityTokenClaimService>();

var configSection = builder.Configuration.GetRequiredSection(BaseUrlConfiguration.CONFIG_NAME);
builder.Services.Configure<BaseUrlConfiguration>(configSection);
var baseUrlConfig = configSection.Get<BaseUrlConfiguration>();

builder.Services.AddMemoryCache();

var key = Encoding.ASCII.GetBytes(AuthorizationConstants.JWT_SECRET_KEY);
builder.Services.AddAuthentication(config =>
{
    config.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(config =>
{
    config.RequireHttpsMetadata = false;
    config.SaveToken = true;
    config.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

const string CORS_POLICY = "CorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CORS_POLICY,
        corsPolicyBuilder =>
        {
            corsPolicyBuilder.WithOrigins(baseUrlConfig!.WebBase.Replace("host.docker.internal", "localhost").TrimEnd('/'));
            corsPolicyBuilder.AllowAnyMethod();
            corsPolicyBuilder.AllowAnyHeader();
        });
});

builder.Services.AddControllers();
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Configuration.AddEnvironmentVariables();

// --- PayPal configuration & DI ---------------------------------------------------------------------
// ASP.NET Core's default env-var provider maps PAYPAL_CLIENT_ID -> config key "PAYPAL_CLIENT_ID", not
// "PayPal:ClientId" (its section delimiter is "__"). Bridge the task's env vars onto the PayPal: keys so
// the same PayPalOptions binding works whether values arrive via user-secrets, appsettings, or env vars.
// Only the variable NAMES appear here; the values live outside the repo.
static void BridgeEnvVar(ConfigurationManager config, string envVarName, string configKey)
{
    var value = System.Environment.GetEnvironmentVariable(envVarName);
    if (!string.IsNullOrWhiteSpace(value)) config[configKey] = value;
}
BridgeEnvVar(builder.Configuration, "PAYPAL_CLIENT_ID", "PayPal:ClientId");
BridgeEnvVar(builder.Configuration, "PAYPAL_CLIENT_SECRET", "PayPal:ClientSecret");
BridgeEnvVar(builder.Configuration, "PAYPAL_ENVIRONMENT", "PayPal:Environment");
BridgeEnvVar(builder.Configuration, "PAYPAL_CURRENCY", "PayPal:Currency");
BridgeEnvVar(builder.Configuration, "PAYPAL_BASE_URL", "PayPal:BaseUrl"); // optional override

var payPalSection = builder.Configuration.GetSection(PayPalOptions.CONFIG_NAME);
builder.Services.Configure<PayPalOptions>(payPalSection);
var payPalOptions = payPalSection.Get<PayPalOptions>() ?? new PayPalOptions();

// Diagnostics handler on the SDK's (default, unnamed) HttpClient: records the real response status so the
// gateway can disambiguate a JsonException on the error path, and bounds a single attempt.
builder.Services.AddTransient<PayPalDiagnosticsHandler>();
builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddHttpMessageHandler<PayPalDiagnosticsHandler>();

builder.Services.AddPayPalServerSdkClient(options =>
{
    // Sandbox is the only environment this SDK ships; PayPal:Environment is validated/logged, not switched on.
    options.Environment = ServerEnvironment.Sandbox;
    options.Oauth2 = new OAuth2ClientCredentials
    {
        ClientId = payPalOptions.ClientId,
        ClientSecret = payPalOptions.ClientSecret
    };
    // Per-attempt timeout; a hung provider ends on the first attempt (timeout rejections are not retried).
    options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(30) };
    // When PayPal:BaseUrl is set it is used verbatim for every call, including the OAuth token request.
    if (!string.IsNullOrWhiteSpace(payPalOptions.BaseUrl))
    {
        options.Server.Default.Sandbox.BaseUrl = payPalOptions.BaseUrl;
    }
});

builder.Services.AddScoped<IPayPalPaymentGateway, PayPalPaymentGateway>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderPaymentService, OrderPaymentService>();
builder.Services.AddScoped<ISavedPaymentMethodService, SavedPaymentMethodService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.EnableAnnotations();
    c.SchemaFilter<CustomSchemaFilters>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. \r\n\r\n 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      \r\n\r\nExample: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
            {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,

                        },
                        new List<string>()
                    }
            });
});

var app = builder.Build();

app.Logger.LogInformation("PublicApi App created...");

// Log the PayPal wiring at startup — environment name, currency, and whether a BaseUrl override is in effect.
// Never log ClientSecret. A missing credential is surfaced as a warning rather than failing startup.
app.Logger.LogInformation("PayPal configured: Environment={Environment}, Currency={Currency}, BaseUrlOverride={HasBaseUrl}",
    payPalOptions.Environment, payPalOptions.Currency, !string.IsNullOrWhiteSpace(payPalOptions.BaseUrl));
if (string.IsNullOrWhiteSpace(payPalOptions.ClientId) || string.IsNullOrWhiteSpace(payPalOptions.ClientSecret))
{
    app.Logger.LogWarning("PayPal credentials are not configured; payment operations will fail until PayPal:ClientId/ClientSecret are set.");
}
if (!string.Equals(payPalOptions.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning("PayPal:Environment is '{Environment}'; this SDK only targets Sandbox. Use PayPal:BaseUrl to point at another host.", payPalOptions.Environment);
}

app.Logger.LogInformation("Seeding Database...");

using (var scope = app.Services.CreateScope())
{
    var scopedProvider = scope.ServiceProvider;
    try
    {
        var catalogContext = scopedProvider.GetRequiredService<CatalogContext>();
        await CatalogContextSeed.SeedAsync(catalogContext, app.Logger);

        var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var identityContext = scopedProvider.GetRequiredService<AppIdentityDbContext>();
        await AppIdentityDbContextSeed.SeedAsync(identityContext, userManager, roleManager);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(CORS_POLICY);

app.UseAuthorization();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
// specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

app.MapControllers();
app.MapEndpoints();

app.Logger.LogInformation("LAUNCHING PublicApi");
app.Run();

public partial class Program { }
