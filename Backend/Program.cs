using Microsoft.EntityFrameworkCore;
using Backend.Data; 

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

var app = builder.Build();


app.UseCors("AllowFrontend");

app.UseAuthorization();


app.MapControllers();

app.MapGet("/", () => "Бекенд рендерера параметричних кривих працює! 🚀");

app.Run();