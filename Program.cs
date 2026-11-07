using System.Security.Cryptography;
using KimiSync.Services;

if (args.Contains("--new-secret"))
{
    var path = Environment.GetEnvironmentVariable("SECRET_PATH") ?? "secret.key";
    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    using var writer = new StreamWriter(file);
    writer.Write(Convert.ToBase64String(RandomNumberGenerator.GetBytes(3072)));
    Console.WriteLine($"Secret saved to {path}");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<KumiTokenService>();
builder.Services.AddSingleton<SyncDataStore>();
var website = builder.Configuration["WEBSITE_URL"] ?? "https://example.com";
var cors = builder.Configuration["HTTP_CORS"] == "yes";
if (cors)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(website).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (args.Contains("--init-db"))
{
    await app.Services.GetRequiredService<SyncDataStore>().Initialize(CancellationToken.None);
    Console.WriteLine("SQL Server schema initialized.");
    return;
}
// Fail at startup if the signing key is absent or unsafe.
_ = app.Services.GetRequiredService<KumiTokenService>();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
}));
app.Use(async (context, next) =>
{
    if (builder.Configuration["IS_PUBLIC"] != "yes" && context.Request.Headers.Origin.ToString() != website)
    {
        context.Response.StatusCode = 403;
        return;
    }
    if (builder.Configuration["HTTPS_REDIRECT"] == "yes" && !context.Request.IsHttps)
    {
        context.Response.Redirect($"https://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}", permanent: true);
        return;
    }
    await next();
});
if (cors) app.UseCors();
app.MapGet("/", () => Results.Redirect(website, permanent: true));
app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /", "text/plain"));
app.MapGet("/auth/client", (KumiTokenService tokens) => Results.Ok(new { secret = tokens.Issue() }));
app.MapControllers();
app.Run();
