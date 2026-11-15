using RRCServices.League.DTO;
using RRCServices.Season;

namespace PublicApp.Components.Pages
{
    public partial class League
    {
        // SEASON WORK (batch 2, 15 Nov 2026):
        //   The hard-coded window (2026-01-01 .. 2026-11-30, and Reset -> 2025)
        //   is replaced by the CURRENT season from ISeasonService:
        //     - during a season: that season (01 Jan .. 31 Oct)
        //     - 01 Nov .. 31 Dec: the season that has just ended
        //   The league therefore stops at 31 Oct, so the special November race
        //   is never scored.
        //
        // private DateTime seasonStart = new DateTime(2026, 01, 01);
        // private DateTime seasonEnd = new DateTime(2026, 11, 30);

        private SeasonInfo? season;

        // Kept as DateTime so the (commented-out) date pickers in League.razor
        // can still bind to them if they are switched back on.
        private DateTime seasonStart;
        private DateTime seasonEnd;   // last day (inclusive), at midnight

        private TrophyLeaguePageDto? page;
        private bool isLoading;
        private string? error;

        protected override async Task OnInitializedAsync()
        {
            await LoadCurrentSeasonAsync();
        }

        private async Task LoadCurrentSeasonAsync()
        {
            season = await SeasonService.GetCurrentAsync();

            if (season is null)
            {
                error = "No season has been set up yet.";
                return;
            }

            seasonStart = season.Window.FromInclusive;
            seasonEnd = season.EndDate.ToDateTime(TimeOnly.MinValue);
        }

        private async Task LoadAsync()
        {
            error = null;
            page = null;

            if (season is null)
            {
                await LoadCurrentSeasonAsync();
                if (season is null) return;
            }

            if (seasonEnd < seasonStart)
            {
                error = "Season end must be on or after season start.";
                return;
            }

            isLoading = true;
            try
            {
                // The service filters with "Date <= seasonEnd". Pass the END of
                // the last day so races held on it at any time are included.
                // (23:59:59, not TimeOnly.MaxValue: SQL 'datetime' would round
                // .9999999 up to midnight of the NEXT day.)
                page = await LeagueTableService.GetLeagueTablesAsync(seasonStart, EndOfDay(seasonEnd));
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                isLoading = false;
            }
        }

        private void Reset()
        {
            // SEASON WORK (batch 2): back to the current season (was 2025).
            if (season is not null)
            {
                seasonStart = season.Window.FromInclusive;
                seasonEnd = season.EndDate.ToDateTime(TimeOnly.MinValue);
            }
            page = null;
            error = null;
        }

        private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddSeconds(-1);

        private static string FormatTimeDiff(int seconds)
        {
            // You store timeDiff as seconds of improvement (capped per race).
            // Display as mm:ss for readability.
            if (seconds <= 0) return "0:00";

            var ts = TimeSpan.FromSeconds(seconds);
            // total minutes can exceed 59, so use (int)TotalMinutes
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }
    }
}
