using SharpCAT2.WebClient.Components;
using SharpCAT2.WebClient.Services;
using SharpCAT2.WebClient.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SharpCAT2 WebClient settings
builder.Services.Configure<WebClientConfiguration>(
    builder.Configuration.GetSection("SharpCAT2WebClient"));

// Add HTTP client for API communication
var webClientConfig = builder.Configuration.GetSection("SharpCAT2WebClient").Get<WebClientConfiguration>() 
    ?? new WebClientConfiguration();

builder.Services.AddHttpClient<IRadioApiService, RadioApiService>(client =>
{
    client.BaseAddress = new Uri(webClientConfig.WebApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(webClientConfig.TimeoutSeconds);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
