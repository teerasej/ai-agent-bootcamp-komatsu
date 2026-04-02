using PVAgentWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// [TODO] Register PVAgentService as a scoped service
// Hint: use builder.Services.AddScoped<PVAgentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<PVAgentWeb.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
