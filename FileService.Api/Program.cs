using FileService.Api.Dtos;
using FileService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Load YAML rules into a singleton service


var app = builder.Build();

// Middleware (pipeline) ############ i need to understand this part more ##############
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // In prod you’d use a generic error handler
    app.UseExceptionHandler("/error");
}

app.MapFilesEndpoints();

app.Run();
