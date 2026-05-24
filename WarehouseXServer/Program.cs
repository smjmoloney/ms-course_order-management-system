using WarehouseXServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowClient", policy =>
        policy.WithOrigins("http://localhost:5131", "https://localhost:7176")
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

app.UseCors("AllowClient");

// Return JSON for all unhandled exceptions so the Blazor client can display them.
app.UseExceptionHandler(exApp => exApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex      = feature?.Error;
    await ctx.Response.WriteAsJsonAsync(new
    {
        error   = ex?.GetType().Name ?? "Exception",
        message = ex?.Message       ?? "An unexpected error occurred."
    });
}));

app.UseHttpsRedirection();

app.MapControllers();

DatabaseInitializer.Initialize(
    builder.Configuration.GetConnectionString("WarehouseX")!);

app.Run();
