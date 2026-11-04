using Microsoft.AspNetCore.Authentication.Cookies;
using Oracle.ManagedDataAccess.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

// 1. Session Services Configuration
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var connectionString = builder.Configuration.GetConnectionString("OracleDbContext");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("❌ Connection string 'OracleDbContext' not found in appsettings.json!");
}

builder.Services.AddSingleton(connectionString);
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Database Connection Test on Startup
try
{
    using (var testConnection = new OracleConnection(connectionString))
    {
        testConnection.Open();
        Console.WriteLine("✔ Oracle Database Connected Successfully!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database Connection Failed: {ex.Message}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 🔑 2. অত্যন্ত গুরুত্বপূর্ণ অর্ডার: UseSession() অবশ্যই UseAuthentication() এর আগে থাকতে হবে!
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();