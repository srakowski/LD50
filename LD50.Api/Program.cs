using LD50.LdjamApi;
using Orleans;
using Orleans.Hosting;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddRefitClient<ILdjamApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.ldjam.com"));

builder.Host.UseOrleans(sb =>
{
    sb.UseLocalhostClustering();
    sb.AddMemoryGrainStorage("default");
});

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
