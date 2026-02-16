using CompanyKnowledgeAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using CompanyKnowledgeAssistant.API.Hubs;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:60616")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<DocumentProcessorService>();
builder.Services.AddScoped<VectorStoreService>();
builder.Services.AddScoped<HtmlSanitizerService>();
builder.Services.AddHttpClient<EmbeddingService>();
builder.Services.AddHttpClient<OllamaLlmService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run();
