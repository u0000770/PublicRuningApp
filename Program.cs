using Microsoft.EntityFrameworkCore;
using PublicApp.Components;
using RRCConfig.Data;
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


        ////
        builder.Services.AddDbContextFactory<RRCContext>(options =>
    options.UseSqlServer(cs, sql =>
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));


        ////

        //builder.Services.AddDbContextFactory<RRCContext>(options =>
        //    options.UseSqlServer(cs));

       // builder.Services.AddDbContextFactory<RRCContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("RRCAzure")));


        builder.Services.AddScoped<IRaceEventService, RaceEventService>();
        builder.Services.AddScoped<IRunnerService, RunnerService>();
        // SEASON WORK (batch 5): SystemClock is now RRCServices.Clock.SystemClock
        // (UK time). PublicApp's own copy in clock.cs has been deleted.
        builder.Services.AddScoped<IClock, SystemClock>();
        builder.Services.AddScoped<CalculatorService>();
        builder.Services.AddScoped<IRaceResultService, RaceResultService>();
        builder.Services.AddScoped<ITrophyCalculator, TrophyCalculator>();
        builder.Services.AddSingleton<ITimeFormatter, TimeFormatter>();

        // SEASON WORK (batch 5): the old JSON season settings registrations
        // (ISeasonSettingsStore / JsonSeasonSettingsStore, ISeasonSettingsService)
        // have been removed.

        // SEASON WORK (batch 2): seasons live in config.Season in the same
        // database, via their own small SeasonContext (RRCConfig project).
        // MyDetails and League use it.
        builder.Services.AddDbContextFactory<SeasonContext>(options =>
            options.UseSqlServer(cs, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
        builder.Services.AddScoped<ISeasonService, SeasonService>();

        builder.Services.AddScoped<ILeagueTableService, LeagueTableService>();
       // builder.Services.AddScoped<ILeagueDataService, LeagueDataService>();

     


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
