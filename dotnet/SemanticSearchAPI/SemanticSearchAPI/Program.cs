using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

var qdrantUrl = builder.Configuration.GetValue<string>("Qdrant:Url") ?? "http://localhost:6333";
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // Read from appsettings.json or default to localhost:6334
    var host = config.GetValue<string>("Qdrant:Url") ?? "localhost";
    var apiKey = config.GetValue<string?>("Qdrant:Key");

    return new QdrantClient( new Uri(qdrantUrl), apiKey: apiKey);
});
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
