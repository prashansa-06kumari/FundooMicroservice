using Fundoo.User.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUserInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();