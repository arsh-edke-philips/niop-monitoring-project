using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NIOP_Monitoring.Services;
using NIOP_Monitoring.Agent;
using System.Data;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.Configure<ChatAgentOptions>(builder.Configuration.GetSection("ChatAgent"));
builder.Services.AddHttpClient(ChatController.HttpClientName, (sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<ChatAgentOptions>>().Value;
    http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.Name = ".NIOP.Chat";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddScoped<IDbConnection>(sp => new SqlConnection(connectionString));
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddHostedService<CatalogLoader>();

builder.Services.AddScoped<OrderSummaryService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DistributionCenterDeviceService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceLineageService>();
builder.Services.AddScoped<OrderLineageService>();
builder.Services.AddScoped<EnrollmentLineageService>();
builder.Services.AddScoped<ReportLineageService>();
builder.Services.AddScoped<PreBuiltKitService>();
builder.Services.AddScoped<TransferLineageService>();
builder.Services.AddScoped<AgentQueryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
