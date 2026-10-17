using Microsoft.AspNetCore.Authentication.Cookies;
using Oracle.ManagedDataAccess.Client;

var builder = WebApplication.CreateBuilder(args);

// ১. কুকি অথেন্টিকেশন কনফিগারেশন
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

// ২. appsettings.json থেকে Oracle Connection String সংগ্রহ করা
var connectionString = builder.Configuration.GetConnectionString("OracleDbContext");

// কানেকশন স্ট্রিং খালি বা নাল থাকলে সাথে সাথে যেন ধরা পড়ে
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("❌ Connection string 'OracleDbContext' not found in appsettings.json!");
}

// ৩. কানেকশন স্ট্রিং রেজিস্টার করা
builder.Services.AddSingleton(connectionString);

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ৪. অ্যাপ স্টার্ট হওয়ার সময় কানেকশন টেস্ট করা
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

// মিডলওয়্যارهای সঠিক ক্রম
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();