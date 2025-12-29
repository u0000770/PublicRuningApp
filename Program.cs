using Microsoft.EntityFrameworkCore;
using PublicApp.Components;
using RRCDataModel.Data;
using RRCServices;
using RRCServices.Calculator;
using RRCServices.Calculator.RRCServices;
using RRCServices.Clock;
using RRCServices.League;
using RRCServices.Runner;
using RRCServices.Season;

namespace PublicApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //      builder.Services.AddDbContextFactory<RRCContext>(options =>
        //options.UseSqlServer(builder.Configuration.GetConnectionString("RRC")));


        var cs = builder.Configuration.GetConnectionString("RRCAzure");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'RRCAzure' not found.");

        builder.Services.AddDbContextFactory<RRCContext>(options =>
            options.UseSqlServer(cs));

       // builder.Services.AddDbContextFactory<RRCContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("RRCAzure")));


        builder.Services.AddScoped<IRaceEventService, RaceEventService>();
        builder.Services.AddScoped<IRunnerService, RunnerService>();
        builder.Services.AddScoped<IClock, SystemClock>();
        builder.Services.AddScoped<CalculatorService>();

     //   builder.Services.AddScoped<ILeagueTableService, LeagueTableService>();

        // ✅ Register the settings store (file path wherever you want)
        var seasonSettingsPath = Path.Combine(
            builder.Environment.ContentRootPath,
            "App_Data",
            "seasonSettings.json");

        // ✅ Store + service registrations (required)
        builder.Services.AddSingleton<ISeasonSettingsStore>(_ =>
            new JsonSeasonSettingsStore(seasonSettingsPath));

        builder.Services.AddSingleton<ISeasonSettingsService, SeasonSettingsService>();


        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
