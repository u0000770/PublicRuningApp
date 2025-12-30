using RRCServices.League.DTO;

namespace PublicApp.Components.Pages
{
    public partial class League
    {
        // private DateTime seasonStart = new(DateTime.Today.Year, 1, 1);
        // private DateTime seasonEnd = DateTime.Today;

        private DateTime seasonStart = new DateTime(2025, 01, 01);
        private DateTime seasonEnd = new DateTime(2025, 11, 30);

        private TrophyLeaguePageDto? page;
        private bool isLoading;
        private string? error;

        private async Task LoadAsync()
        {
            error = null;
            page = null;

            if (seasonEnd < seasonStart)
            {
                error = "Season end must be on or after season start.";
                return;
            }

            isLoading = true;
            try
            {
                page = await LeagueTableService.GetLeagueTablesAsync(seasonStart, seasonEnd);
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
            seasonStart = new DateTime(2025, 01, 01);
            seasonEnd = new DateTime(2025, 11, 30);
            page = null;
            error = null;
        }

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