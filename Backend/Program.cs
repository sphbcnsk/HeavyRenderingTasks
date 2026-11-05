using Backend.Data; 
using Microsoft.EntityFrameworkCore;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

builder.Services.AddScoped<CurveRenderer>();

builder.Services.AddScoped<CurveRenderer>();
builder.Services.AddSingleton<RenderTaskQueue>();
builder.Services.AddHostedService<RenderBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("AllowAll");

app.UseCors("AllowFrontend");
app.UseStaticFiles();

app.UseAuthorization();


app.MapControllers();

app.MapGet("/", () => "Бекенд рендерера параметричних кривих працює!");

app.Run();