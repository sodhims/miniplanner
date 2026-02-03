using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using dfd2wasm;
using dfd2wasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Core services (injected by DFDEditor)
builder.Services.AddScoped<GeometryService>();
builder.Services.AddScoped<PathService>();
builder.Services.AddScoped<UndoService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<ShapeLibraryService>();
builder.Services.AddScoped<TemplateConfigService>();
builder.Services.AddScoped<RecordingService>();
builder.Services.AddScoped<LayoutOptimizationService>();
builder.Services.AddScoped<LayoutOptimizerService>();

await builder.Build().RunAsync();
