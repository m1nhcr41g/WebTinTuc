using Microsoft.AspNetCore.Authentication.Cookies;
using WebTinTuc.Services.Articles;
using WebTinTuc.Services.Preferences;
using WebTinTuc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ?? AUTH USER + ADMIN (2 cookie riêng)
builder.Services.AddAuthentication()
    .AddCookie("UserScheme", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
    })
    .AddCookie("AdminScheme", options =>
    {
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
    });

builder.Services.AddAuthorization();

// ?? SERVICES
builder.Services.AddScoped<AuthAccountStore>();
builder.Services.AddScoped<AdminAuthService>(); // ?? THÊM
builder.Services.AddScoped<JournalistArticleStore>();
builder.Services.AddScoped<ArticleInteractionStore>();
builder.Services.AddScoped<UserPreferenceStore>();

var app = builder.Build();

// ?? INIT DATABASE
using (var scope = app.Services.CreateScope())
{
    var accountStore = scope.ServiceProvider.GetRequiredService<AuthAccountStore>();
    var adminStore = scope.ServiceProvider.GetRequiredService<AdminAuthService>(); // ?? THÊM
    var articleStore = scope.ServiceProvider.GetRequiredService<JournalistArticleStore>();
    var interactionStore = scope.ServiceProvider.GetRequiredService<ArticleInteractionStore>();
    var preferenceStore = scope.ServiceProvider.GetRequiredService<UserPreferenceStore>();

    await accountStore.EnsureSchemaAsync();
    await adminStore.InitAsync(); // ?? THÊM
    await articleStore.EnsureSchemaAsync();
    await interactionStore.EnsureSchemaAsync();
    await preferenceStore.EnsureSchemaAsync();
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // ?? GI?
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();