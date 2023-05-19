using Bank.Api.Core;
using Bank.Data.Context;
using Bank.Data.Interface;
using Bank.Data.Model;
using Bank.Data.Repositories;
using IdentityServer4.AccessTokenValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;

using NLog;


var builder = WebApplication.CreateBuilder(args);
var context = new HttpContextAccessor();
var logger = LogManager.Setup().SetupExtensions(se =>
{
    se.RegisterLayoutRenderer("aspnet-request-authorization", (logevent) => context?.HttpContext?.Request?.Headers?["Authorization"].ToString());
    se.RegisterLayoutRenderer("aspnet-request-useragent", (logevent) => context?.HttpContext?.Request?.Headers?["User-Agent"].ToString());
});

// Add services to the container.
var services = builder.Services;
var configuration = builder.Configuration;
string idpHost = configuration.GetSection("AppSettings")["IDPHost"];


services.AddMvc(opt => opt.EnableEndpointRouting = false).AddMvcOptions(o =>
{
    o.Filters.Add(new ExceptionHandlingFilter());
});
services.AddControllers().AddNewtonsoftJson(opt =>
{
    opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    opt.SerializerSettings.DateFormatString = "yyyy-MM-dd'T'HH:mm:ss";
});

services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
services.AddScoped<ICustomerAccountRepository, CustomerAccountRepository>();


// Add accessing policy
//services.AddMvcCore(opt =>
//{
//    var policy = ScopePolicy.Create("yourpolicyscope");
//    opt.Filters.Add(new AuthorizeFilter(policy));
//}).AddXmlSerializerFormatters()
//.AddXmlDataContractSerializerFormatters()
//.AddAuthorization(opt =>
//{
//    opt.AddPolicy("myPolicy", pol =>
//    {
//        pol.RequireScope("yourpolicyscope");
//    });
//});

// Add IDP
//services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
//.AddIdentityServerAuthentication(options =>
//{
//    options.Authority = idpHost;
//    options.RequireHttpsMetadata = false;
//    options.LegacyAudienceValidation = true;
//    options.SupportedTokens = SupportedTokens.Jwt;
//});

// Connect to db
//services.AddDbContext<BankDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("WebApiDatabase"), opt =>
//{
//    opt.EnableRetryOnFailure(2, System.TimeSpan.FromSeconds(2), null);
//}));


// Add Logging
services.AddLogging(log =>
{
    log.AddConfiguration(configuration.GetSection("Logging"));
});

services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Bank Api" });
    // Add Swagger Definition
    //c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    //{
    //    Name = "Authorization",
    //    Type = SecuritySchemeType.ApiKey,
    //    Scheme = "Bearer",
    //    BearerFormat = "JWT",
    //    In = ParameterLocation.Header
    //});
    // Add Swagger security Scheme
    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference
    //            {
    //                Type = ReferenceType.SecurityScheme,
    //                Id = "Bearer"
    //            },
    //        },
    //        new List<string>()
    //    }
    //});
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    LogManager.LoadConfiguration($"nlog.config");

}
else
{
    // Normal Situation will have environment
    // example nlog.live.config which will use like as below
    //LogManager.LoadConfiguration($"nlog.{app.Environment.EnvironmentName}.config");
    LogManager.LoadConfiguration($"nlog.config");
}


app.UseHttpsRedirection();

// For Idp Login authentication
//app.UseAuthentication();
//app.UseAuthorization();


app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bank Api");
});

app.Run();
