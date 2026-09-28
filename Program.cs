using GestorArchivos_RRHH.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();



builder.Services.AddSingleton<ITipoIncapacidadService, JsonTipoIncapacidadService>();


builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});


var app = builder.Build();

// consola
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
Console.WriteLine($"[INFO] tipos-incapacidad.json → {Path.Combine(env.ContentRootPath, "tipos-incapacidad.json")}");

// pepeline.
if (!app.Environment.IsDevelopment())

    // Configure 
    if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
