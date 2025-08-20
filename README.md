# Auth0 + Blazor Web App (.NET 8)

Minimal example of adding **Auth0** login to a **Blazor Web App (Interactive Server)** using two tiny **Razor Pages** for the handshake. No local user tables, no password flows.

## What you get

- Blazor Web App (.NET 8), **Interactive Server**
- Auth routes: **`/auth/login`** & **`/auth/logout`** (Razor Pages, not components)
- Cookie-based sign-in (via Auth0/OIDC)
- `AuthorizeView` support in components
- Optional compatibility for `/Account/Login|Logout`
- Ready for local dev and Fly.io deployment

---

## Prerequisites

- .NET 8 SDK
- Auth0 tenant (free plan is fine)

---

## 1) Create the app

```bash
dotnet new blazor -n Auth0Blazor.NET -int Server --empty
cd Auth0Blazor.NET
```

---

## 2) Auth0 setup (once)

1. Create/Login to Auth0 → create a **Regular Web Application**.
2. Copy **Domain**, **Client ID**, **Client Secret**.
3. Configure URLs (dev):
    - **Allowed Callback URLs:** `https://localhost:5001/callback`
    - **Allowed Logout URLs:** `https://localhost:5001/`
    - **Allowed Web Origins:** `https://localhost:5001`
4. Enable at least one **Connection** (Database or Social).

> The Auth0 ASP.NET Core package uses `/callback` by default.

---

## 3) Add packages

```bash
dotnet add package Auth0.AspNetCore.Authentication
```

---

## 4) Configuration (dev & prod)

**Dev — User Secrets**
```bash
dotnet user-secrets init
dotnet user-secrets set "Auth0:Domain" "your-tenant.eu.auth0.com"
dotnet user-secrets set "Auth0:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Auth0:ClientSecret" "YOUR_CLIENT_SECRET"
```

**Prod — Fly.io secrets (optional)**
```bash
flyctl secrets set   Auth0__Domain="your-tenant.eu.auth0.com"   Auth0__ClientId="YOUR_CLIENT_ID"   Auth0__ClientSecret="YOUR_CLIENT_SECRET"
```

---

## 5) Wire it up (`Program.cs`)

```csharp
using Auth0.AspNetCore.Authentication;
using Auth0Blazor.NET.Components; // replace with your root component namespace
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Blazor (Interactive Server)
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// <AuthorizeView> support in components
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorPages();               // for /auth/*.cshtml
builder.Services.AddHttpContextAccessor();

// Auth0 (OIDC) + cookie session
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"]!;
    options.ClientId = builder.Configuration["Auth0:ClientId"]!;
    options.ClientSecret = builder.Configuration["Auth0:ClientSecret"];
});

// Optional: classic paths for challenges/sign-out
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.LoginPath  = "/auth/login";
        o.LogoutPath = "/auth/logout";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();                            // hosts /auth/login & /auth/logout
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
```

---

## 6) Router with auth state (`Components/Routes.razor`)

```razor
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Routing

<CascadingAuthenticationState>
  <Router AppAssembly="@typeof(Program).Assembly">
    <Found Context="routeData">
      <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
      <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
      <LayoutView Layout="@typeof(MainLayout)">
        <p>Sorry, there’s nothing at this address.</p>
      </LayoutView>
    </NotFound>
  </Router>
</CascadingAuthenticationState>
```

---

## 7) Auth endpoints (single-file Razor Pages)

**Create: `Components/Pages/Auth/Login.cshtml`**
```razor
@page "/auth/login"
@using Microsoft.AspNetCore.Authentication
@using Auth0.AspNetCore.Authentication

@functions {
  public async Task OnGetAsync()
  {
    var redirect = string.IsNullOrWhiteSpace(Request.Query["redirectUri"])
      ? "/"
      : Request.Query["redirectUri"].ToString();

    await HttpContext.ChallengeAsync(
      Auth0Constants.AuthenticationScheme,
      new AuthenticationProperties { RedirectUri = redirect });
  }
}
```

**Create: `Components/Pages/Auth/Logout.cshtml`**
```razor
@page "/auth/logout"
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Authentication.Cookies
@using Auth0.AspNetCore.Authentication
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

@functions {
  public async Task OnGetAsync()
  {
    var redirect = string.IsNullOrWhiteSpace(Request.Query["redirectUri"])
      ? "/"
      : Request.Query["redirectUri"].ToString();

    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    await HttpContext.SignOutAsync(
      Auth0Constants.AuthenticationScheme,
      new AuthenticationProperties { RedirectUri = redirect });
  }
}
```

> Keep these under `/Pages/Auth/`. No `.cshtml.cs` files required.

---

## 8) Minimal Home to test auth (`Components/Pages/Home.razor`)

```razor
@page "/"
@using Microsoft.AspNetCore.Components.Authorization
@inject NavigationManager Nav

<PageTitle>Home</PageTitle>

<AuthorizeView Context="auth">
  <Authorized>
    <h1>Hello, @GetDisplayName(auth.User) 👋</h1>
    <button @onclick="Logout">Logout</button>
    <details style="margin-top:1rem">
      <summary>Show my claims</summary>
      <ul>
        @foreach (var c in auth.User.Claims) { <li><b>@c.Type</b>: @c.Value</li> }
      </ul>
    </details>
  </Authorized>
  <NotAuthorized>
    <h1>Welcome</h1>
    <p>You’re not signed in yet.</p>
    <button @onclick="Login">Login</button>
  </NotAuthorized>
</AuthorizeView>

@code {
  void Login()
  {
    var redirect = Uri.EscapeDataString(Nav.ToBaseRelativePath(Nav.Uri));
    Nav.NavigateTo($"/auth/login?redirectUri={redirect}", forceLoad: true);
  }

  void Logout()
  {
    var redirect = Uri.EscapeDataString(Nav.ToBaseRelativePath(Nav.Uri));
    Nav.NavigateTo($"/auth/logout?redirectUri={redirect}", forceLoad: true);
  }

  static string GetDisplayName(System.Security.Claims.ClaimsPrincipal user) =>
    user.Identity?.Name
    ?? user.FindFirst("name")?.Value
    ?? user.FindFirst("email")?.Value
    ?? "User";
}
```

---

## 9) Run

```bash
dotnet run
```
Open the HTTPS URL shown (e.g., `https://localhost:5001`), click **Login**, authenticate with Auth0, and you’ll land back on `/` with your name and a **Logout** button.

---

## 10) Optional: Override cookie paths
If you don't want to use classic paths like `/Account/Login` and `/Account/Logout`, you can override the cookie paths in `Program.cs`:

```csharp
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.LoginPath  = "/Auth/Login";
        o.LogoutPath = "/Auth/Logout";
    });
```

---

## Why Auth0?

- Free tier good enough to start.
- Fast to integrate in ASP.NET Core/Blazor.
- Hosted Universal Login with social providers.
- Easy to swap/upgrade later if you outgrow limits.

---

## Troubleshooting

- **404 on `/auth/login`**  
  Make sure your .cshtml pages live under /Pages/Auth/ and that Razor Pages are registered in Program.cs with `builder.Services.AddRazorPages()` and `app.MapRazorPages()`. If you prefer to keep them under `/Components/...`, set the Razor Pages root instead:
  `builder.Services.AddRazorPages(o => o.RootDirectory = "/Components")` (and still call `app.MapRazorPages()`).

- **Redirect loops / 401 after login**  
  Callback URL must match exactly (scheme/host/path). Keep HTTPS even locally.

- **Behind proxy (Fly.io/Nginx)**  
  Keep `UseForwardedHeaders` so OIDC sees the correct scheme/host.

---

## Deploying to Fly.io (quick notes)

- Set Auth0 secrets with `flyctl secrets set ...` as shown above.
- Add your Fly hostname(s) to Auth0:
    - Callback: `https://<app>.fly.dev/callback`
    - Logout: `https://<app>.fly.dev/`
    - Web Origin: `https://<app>.fly.dev`

---

## License

MIT (or your preferred license).

