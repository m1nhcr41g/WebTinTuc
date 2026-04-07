using Microsoft.AspNetCore.Authentication.Cookies;
using WebTinTuc.Services.Articles;
using WebTinTuc.Services.Preferences;
using WebTinTuc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthAccountStore>();
builder.Services.AddScoped<JournalistArticleStore>();
builder.Services.AddScoped<ArticleInteractionStore>();
builder.Services.AddScoped<UserPreferenceStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var accountStore = scope.ServiceProvider.GetRequiredService<AuthAccountStore>();
    var articleStore = scope.ServiceProvider.GetRequiredService<JournalistArticleStore>();
    var interactionStore = scope.ServiceProvider.GetRequiredService<ArticleInteractionStore>();
    var preferenceStore = scope.ServiceProvider.GetRequiredService<UserPreferenceStore>();
    await accountStore.EnsureSchemaAsync();
    await articleStore.EnsureSchemaAsync();
    await interactionStore.EnsureSchemaAsync();
    await preferenceStore.EnsureSchemaAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
