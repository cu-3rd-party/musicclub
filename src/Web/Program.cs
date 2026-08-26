using CuMusicClub.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

await app.InitialiseDatabaseAsync();

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseCors(static builder => builder
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowAnyOrigin());

app.UseFileServer();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");

app.UseExceptionHandler(options => { });

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
