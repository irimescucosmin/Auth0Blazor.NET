using Auth0.AspNetCore.Authentication;
using Auth0Blazor.NET.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

// Auth0 (OIDC) + cookie sign-in
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"]!;
    options.ClientId = builder.Configuration["Auth0:ClientId"]!;
    options.ClientSecret = builder.Configuration["Auth0:ClientSecret"];
    // If/when you need API tokens: options.Audience = "...";
});

// Override the default cookie authentication options
builder.Services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, o =>
{
    o.LoginPath = "/auth/login";
    o.LogoutPath = "/auth/logout";
});

builder.Services.AddAuthorization();

var app = builder.Build();

// If deploying behind a proxy (Nginx, Fly.io, etc.)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

app.UseHttpsRedirection(); // if you want redirection as well

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();


app.MapGet("/auth/login", async (HttpContext ctx) =>
{
    var redirectUri = string.IsNullOrWhiteSpace(ctx.Request.Query["redirectUrl"]) 
        ? "/" : ctx.Request.Query["redirectUrl"].ToString();
    
    if (ctx.User.Identity?.IsAuthenticated == true)
    {
        ctx.Response.Redirect(redirectUri);
        return;
    }

    await ctx.ChallengeAsync(Auth0Constants.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = redirectUri });
}).AllowAnonymous();

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    var redirectUri = string.IsNullOrWhiteSpace(ctx.Request.Query["redirectUrl"]) 
        ? "/" : ctx.Request.Query["redirectUrl"].ToString();
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(Auth0Constants.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = redirectUri });
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();