using MoltbookPilot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register Lm Studio
builder.Services.AddHttpClient<LmStudioClient>(http =>
{
    http.BaseAddress = new Uri("http://localhost:1234");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/api/health", () => Results.Ok("OK"));

app.MapPost("/api/agent/think", async (ThinkRequest req, LmStudioClient lm) =>
{
    var text = await lm.ChatAsync(
        model: "gpt-4o",
        system: "You are a helpful assistant that helps users with their tasks.",
        user: req.prompt
        );

    return Results.Ok(text);
});

app.Run();
