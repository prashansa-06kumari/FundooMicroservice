using System.Text;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

namespace Fundoo.ApiGateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

            builder.Services
                .AddAuthentication()
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            builder.Services.AddOcelot();

            var app = builder.Build();

            // /health Ocelot se pehle handle hoga
            app.Map("/health", branch =>
            {
                branch.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync("Healthy");
                });
            });

            await app.UseOcelot();

            app.Run();
        }
    }
}