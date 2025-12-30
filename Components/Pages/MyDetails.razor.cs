using Calculator;
using Microsoft.AspNetCore.Components;
using RRCServices;
using RRCServices.Calculator;
using RRCServices.Runner;

namespace PublicApp.Components.Pages
{
    public partial class MyDetails
    {
        // Query string: /mydetails?RunnerId=160
        [Parameter]
        [SupplyParameterFromQuery]
        public int RunnerId { get; set; }

        private DateTime _seasonStart; // DateTime for comparisons
        private bool _hasSeasonRaces;  // runner has >=1 eligible race this season

        private bool _loading = true;

        private RunnerDetailsDto? _runner;
        private List<RaceEventListItemDTO> _races = new();

        private int? _selectedRaceEventId;

        // ✅ Single source of truth for the selected event
        private RaceEventListItemDTO? SelectedRaceEvent =>
            _selectedRaceEventId is null
                ? null
                : _races.FirstOrDefault(r => r.RaceEventId == _selectedRaceEventId.Value);

        // Prediction / messages
        private int? _predictedSeconds;
        private string? _calcMessage;
        private string? _saveMessage;

        private List<EventRaceTimesDto> LastThreeNonMileRaces =>
         _runner?.EventTimes
             .Where(t => t.RaceDate.HasValue)
             .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date) // completed only
             .Where(t => t.RaceDate!.Value >= _seasonStart)       // ✅ in-season only
             .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
             .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
             .Where(t => !IsOneMile(t.DistanceMeters!.Value))     // ✅ exclude 1 mile
             .OrderByDescending(t => t.RaceDate)
             .Take(3)
             .ToList()
         ?? new List<EventRaceTimesDto>();


        private static bool IsOneMile(double meters)
        {
            const double oneMileMeters = 1609.34;

            // Allow tolerance because stored distances can vary slightly
            return Math.Abs(meters - oneMileMeters) <= 5; // +/- 5 meters
        }


        protected override async Task OnParametersSetAsync()
        {
            _loading = true;

            if (RunnerId <= 0)
            {
                _runner = null;
                _loading = false;
                return;
            }

            //  var season = await SeasonSettingsService.GetAsync();
            // _seasonStart = season.SeasonStartDate.ToDateTime(TimeOnly.MinValue);

            _seasonStart = new DateTime(2025, 12, 1);

            _runner = await RunnerService.GetRunnerDetailsAsync(RunnerId, includeInactiveTimes: false);

            _hasSeasonRaces = _runner.EventTimes.Any(t =>
        t.RaceDate.HasValue &&
        t.RaceDate.Value >= _seasonStart &&
        t.ActualSeconds.HasValue &&
        t.ActualSeconds.Value > 0 &&
        t.DistanceMeters.HasValue &&
        t.DistanceMeters.Value > 0 &&
        !IsOneMile(t.DistanceMeters.Value));

            if (_runner is null)
            {
                _loading = false;
                return;
            }

            var from = Clock.Now.Date;               // ✅ use clock, not DateTime.Today
            var to = Clock.Now.Date.AddMonths(3);

            var list = await RaceEventService.GetRaceEventListAsync(
                activeOnly: true,
                distanceCode: null,
                from: from,
                to: to);

            _races = list
                .Where(r => r.Active)
                .OrderBy(r => r.Date)
                .ToList();

            _selectedRaceEventId = _races.FirstOrDefault()?.RaceEventId;

            // ✅ Calculate once after initial load so details appear immediately
            Calculate();

            _loading = false;
        }

        private void OnSelectedRaceChanged()
        {
            // Selection changed, recalc prediction & derived display strings
            Calculate();
        }

        // ---------------- Display strings ----------------

        private string? TargetTimeText =>
            _predictedSeconds is null ? null : FormatHms(_predictedSeconds.Value);

        private string? SummaryText
        {
            get
            {
                if (_predictedSeconds is null || SelectedRaceEvent is null)
                    return null;

                var title = SelectedRaceEvent.EventTitle ?? "(unknown race)";
                var distanceLabel = FormatRaceDistanceLabel(SelectedRaceEvent.DistanceMeters);
                var raceDate = SelectedRaceEvent.Date;

                var gapDays = (raceDate.Date - Clock.Now.Date).Days;
                var weeks = gapDays / 7;

                return $"{title} is a {distanceLabel} race on {raceDate: d MMMM} and you have {weeks} weeks to prepare";
            }
        }



        private static bool IsClose(double actual, double expected, double tolerance = 0.05)
        {
            return Math.Abs(actual - expected) <= tolerance;
        }


        private string? PacePerMileText
        {
            get
            {
                if (_predictedSeconds is null || SelectedRaceEvent is null)
                    return null;

                var miles = SelectedRaceEvent.DistanceMeters * 0.00062137119;
                if (miles <= 0) return null;

                var paceSecondsPerMile = _predictedSeconds.Value / miles;
                return FormatPace(paceSecondsPerMile);
            }
        }

        private sealed class TrophyRaceRow
        {
            public string RaceTitle { get; init; } = "";
            public string DistanceLabel { get; init; } = "";
            public DateTime RaceDate { get; init; }
            public string RecordedTime { get; init; } = "";
            public string AgeGrade { get; init; } = "";
        }

        private List<TrophyRaceRow> TrophyRaces =>
        _runner?.EventTimes
            .Where(t => t.RaceDate.HasValue)
            .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
            .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
            .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date)
            .OrderByDescending(t => t.RaceDate)
            .Select(t =>
            {
                DateTime? dobDt = _runner.Dob?.ToDateTime(TimeOnly.MinValue);

                var ageGrade =
                    (dobDt.HasValue && t.RaceDate.HasValue && !string.IsNullOrWhiteSpace(t.RaceDistance))
                    ? AgeGrade.GetWavScore(
                        time: t.ActualSeconds!.Value,
                        gender: _runner.IsMale,
                        dob: dobDt,
                        RaceCode: t.RaceDistance,      // ✅ distance code
                        RaceDate: t.RaceDate)
                    : 0;

                return new TrophyRaceRow
                {
                    RaceTitle = t.RaceTitle ?? "(unknown)",
                    DistanceLabel = FormatRaceDistanceLabel(t.DistanceMeters!.Value),
                    RaceDate = t.RaceDate!.Value,
                    RecordedTime = FormatHms(t.ActualSeconds!.Value),
                    AgeGrade = ageGrade.ToString()
                };
            })
            .ToList()
        ?? new List<TrophyRaceRow>();


        //  public static int GetWavScore(int time, bool gender, DateTime? dob, string RaceCode, DateTime? RaceDate)

        // ---------------- Core calculate ----------------

        private static double GetRaceEventDistanceMeters(RaceEventListItemDTO re) => re.DistanceMeters;

        private void Calculate()
        {
            _predictedSeconds = null;
            _calcMessage = null;
            _saveMessage = null;

            if (_runner is null || SelectedRaceEvent is null)
            {
                _calcMessage = "Please select a runner and a race event.";
                return;
            }

            if (!_hasSeasonRaces)
            {
                _calcMessage = $"We can’t provide a predicted time yet — please run at least one race this season (from {_seasonStart:dd MMM yyyy}).";
                return;
            }

            var newDistanceMeters = GetRaceEventDistanceMeters(SelectedRaceEvent);
            if (newDistanceMeters <= 0)
            {
                _calcMessage = "Could not determine the selected race distance (meters).";
                return;
            }

            var today = Clock.Now.Date;

            var recent = _runner.EventTimes
                .Where(t => t.RaceDate.HasValue && t.RaceDate.Value.Date < today)
                .Where(t => t.RaceDate!.Value >= _seasonStart)            // ✅ in-season only
                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
                .Where(t => !IsOneMile(t.DistanceMeters!.Value))          // ✅ exclude mile
                .OrderByDescending(t => t.RaceDate)
                .Take(3)
                .Select(t => new RecentRaceDto
                {
                    Distance = t.DistanceMeters!.Value,
                    Actual = t.ActualSeconds!.Value
                })
                .ToList();

            if (recent.Count == 0)
            {
                _calcMessage = $"We can’t provide a predicted time yet — please run at least one eligible race this season (from {_seasonStart:dd MMM yyyy}).";
                return;
            }

            var predicted = CalculatorService.PredictFromRecentRaces(recent, newDistanceMeters);
            if (predicted is null)
            {
                _calcMessage = "Prediction failed (no usable recent race data).";
                return;
            }

            _predictedSeconds = (int)Math.Round(predicted.Value);
            _calcMessage = $"Prediction calculated using {recent.Count} race(s) from this season.";
        }


        // ---------------- Formatting helpers ----------------

        private static string FormatHms(int totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)ts.TotalHours:00}h:{ts.Minutes:00}m:{ts.Seconds:00}s";
        }

        private static string FormatPace(double paceSecondsPerMile)
        {
            if (paceSecondsPerMile <= 0) return "n/a";
            var ts = TimeSpan.FromSeconds(paceSecondsPerMile);
            var mins = (int)ts.TotalMinutes;
            return $"{mins} min {ts.Seconds:00} sec";
        }

        private static string FormatRaceDistanceLabel(double meters)
        {
            if (meters <= 0) return "unknown distance";

            const double metersPerMile = 1609.344;

            var miles = meters / metersPerMile;
            var km = meters / 1000.0;

            // ---- Mile-based distances ----
            var roundedMiles = Math.Round(miles, 2);

            if (IsClose(roundedMiles, 1)) return "1 Mile";
            if (IsClose(roundedMiles, 3)) return "3 Miles";
            if (IsClose(roundedMiles, 5)) return "5 Miles";
            if (IsClose(roundedMiles, 10)) return "10 Miles";

            // ---- Metric distances ----
            var roundedKm = Math.Round(km, 1);

            if (IsClose(roundedKm, 5)) return "5k";
            if (IsClose(roundedKm, 10)) return "10k";
            if (IsClose(roundedKm, 21.1)) return "Half Marathon";
            if (IsClose(roundedKm, 42.2)) return "Marathon";

            // ---- Fallback (rare / custom events) ----
            return $"{roundedKm:0.#}km";
        }
    }
}