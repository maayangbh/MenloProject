using FileService.Api.Dtos;
using FileService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);



var app = builder.Build();

app.MapFilesEndpoints();

app.Run();
