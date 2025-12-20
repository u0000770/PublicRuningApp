using Microsoft.EntityFrameworkCore;
using PublicApp.Components;
using RRCDataModel.Data;
using RRCServices;
using RRCServices.Calculator;
using RRCServices.Calculator.RRCServices;
using RRCServices.Clock;
using RRCServices.Runner;

namespace PublicApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContextFactory<RRCContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("RRC")));


        builder.Services.AddScoped<IRaceEventService, RaceEventService>();
        builder.Services.AddScoped<IRunnerService, RunnerService>();
        builder.Services.AddScoped<IClock, SystemClock>();
        builder.Services.AddScoped<CalculatorService>();


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
