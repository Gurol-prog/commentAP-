using Comment.API.Config;
using Comment.Application.Interfaces;
using Comment.Infrastructure.Services;
using Comment.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// MongoDB ayarlarını çek
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

// MongoDB service
builder.Services.AddSingleton<IMongoDBService, MongoDBService>();

// Servisleri bağla
builder.Services.AddScoped<IContentCommentService, ContentCommentService>();
builder.Services.AddScoped<ICommentVoteService, CommentVoteService>();
builder.Services.AddScoped<ICommentReportService, CommentReportService>();

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
app.UseAuthorization();
app.MapControllers();
app.Run();
