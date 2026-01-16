using Calculator;
using Microsoft.AspNetCore.Components;
using RRCServices;
using RRCServices.Calculator;
using RRCServices.Runner;
using RRCServices.Season;

namespace PublicApp.Components.Pages
{
    public partial class MyDetails
    {
        #region Parameters (Query String)

        /// <summary>
        /// Runner id supplied from the query string.
        /// Example: /mydetails?RunnerId=160
        /// </summary>
        [Parameter]
        [SupplyParameterFromQuery]
        public int RunnerId { get; set; }

        #endregion

        #region State (Page / UI)

        private bool _loading = true;

        private DateTime _seasonStart;       // DateTime for comparisons (season start boundary)
        private bool _hasSeasonRaces;        // runner has >= 1 eligible race this season

        private RunnerDetailsDto? _runner;
        private List<RaceEventListItemDTO> _races = new();

        // Selected race id is the “single source of truth” for selection
        private int? _selectedRaceEventId;

        // Prediction & messages
        private int? _predictedSeconds;
        private string? _calcMessage;
        private string? _saveMessage;

        #endregion

        #region Computed / Derived Properties (Selection + Display)

        /// <summary>
        /// Resolves the selected event from the selected id and the loaded list.
        /// </summary>
        private RaceEventListItemDTO? SelectedRaceEvent =>
            _selectedRaceEventId is null
                ? null
                : _races.FirstOrDefault(r => r.RaceEventId == _selectedRaceEventId.Value);

        /// <summary>
        /// Returns the last three completed, in-season, non-mile races with usable timing + distance.
        /// </summary>
        private List<EventRaceTimesDto> LastThreeNonMileRaces =>
            _runner?.EventTimes
                .Where(t => t.RaceDate.HasValue)
                .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date) // completed only
                .Where(t => t.RaceDate!.Value >= _seasonStart)       // in-season only
                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
                .Where(t => !IsOneMile(t.DistanceMeters!.Value))     // exclude 1 mile
                .OrderByDescending(t => t.RaceDate)
                .Take(3)
                .ToList()
            ?? new List<EventRaceTimesDto>();

        /// <summary>
        /// Formatted predicted time display ("00h:00m:00s").
        /// </summary>
        private string? TargetTimeText =>
            _predictedSeconds is null ? null : FormatHms(_predictedSeconds.Value);

        /// <summary>
        /// A simple narrative summary of the selected event and time-to-race.
        /// </summary>
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

                return $"{title} is a {distanceLabel} race on {raceDate:d MMMM} and you have {weeks} weeks to prepare";
            }
        }

        /// <summary>
        /// Predicted pace per mile display ("X min YY sec").
        /// </summary>
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

        #endregion

        #region Trophy Races (Projection)

        /// <summary>
        /// Row model for displaying race history with age-grade.
        /// </summary>
        private sealed class TrophyRaceRow
        {
            public string RaceTitle { get; init; } = "";
            public string DistanceLabel { get; init; } = "";
            public DateTime RaceDate { get; init; }
            public string RecordedTime { get; init; } = "";
            public string AgeGrade { get; init; } = "";
        }

        /// <summary>
        /// Projects completed races into a table-friendly structure including age grade score.
        /// </summary>
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
                                RaceCode: t.RaceDistance,     // distance code
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

        #endregion

        #region Lifecycle (Load / Initialise)

        /// <summary>
        /// Loads runner details and upcoming race events based on RunnerId.
        /// Also selects the first upcoming event and performs an initial prediction.
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            _loading = true;
            _seasonStart = new DateTime(2025, 12, 1);
            // Guard: invalid RunnerId
            if (RunnerId <= 0)
            {
                _runner = null;
                _loading = false;
                return;
            }

            // TODO: restore season settings service usage when ready
           //  var season = await SeasonSettingsService.GetAsync();
           //  _seasonStart = season.SeasonStartDate.ToDateTime(TimeOnly.MinValue);

            // Load runner details
            _runner = await RunnerService.GetRunnerDetailsAsync(RunnerId, includeInactiveTimes: false);

            // Determine if runner has any eligible in-season races (completed, non-mile, with distance/time)
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

            // Load upcoming races (next 3 months)
            var from = Clock.Now.Date;               // use clock, not DateTime.Today
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

            // Default selection = first upcoming race
            _selectedRaceEventId = _races.FirstOrDefault()?.RaceEventId;

            // Initial calculation so UI shows results immediately
            Calculate();

            _loading = false;
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Called when the user changes the selected race event.
        /// Triggers recalculation of prediction and derived display strings.
        /// </summary>
        private void OnSelectedRaceChanged()
        {
            Calculate();
        }

        #endregion

        #region Core Calculation (Prediction)

        /// <summary>
        /// Extracts distance in meters from the selected race event DTO.
        /// </summary>
        private static double GetRaceEventDistanceMeters(RaceEventListItemDTO re) => re.DistanceMeters;

        /// <summary>
        /// Calculates predicted time (seconds) for the selected event using up to 3 recent eligible races.
        /// Also sets user-facing messages when prediction cannot be produced.
        /// </summary>
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

            // Build “recent” dataset (up to 3 completed, in-season, non-mile races)
            var recent = _runner.EventTimes
                .Where(t => t.RaceDate.HasValue && t.RaceDate.Value.Date < today)
                .Where(t => t.RaceDate!.Value >= _seasonStart)            // in-season only
                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
                .Where(t => !IsOneMile(t.DistanceMeters!.Value))          // exclude mile
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

        #endregion

        #region Utilities (Distance Checks)

        /// <summary>
        /// True if the provided meters value is approximately one mile (tolerance included).
        /// </summary>
        private static bool IsOneMile(double meters)
        {
            const double oneMileMeters = 1609.34;
            return Math.Abs(meters - oneMileMeters) <= 5; // +/- 5 meters tolerance
        }

        /// <summary>
        /// Compares two doubles for approximate equality within a tolerance.
        /// Used for mapping distance values to friendly labels.
        /// </summary>
        private static bool IsClose(double actual, double expected, double tolerance = 0.05)
        {
            return Math.Abs(actual - expected) <= tolerance;
        }

        #endregion

        #region Formatting Helpers (Time / Pace / Distance Labels)

        /// <summary>
        /// Formats seconds into a HHh:MMm:SSs string.
        /// </summary>
        private static string FormatHms(int totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)ts.TotalHours:00}h:{ts.Minutes:00}m:{ts.Seconds:00}s";
        }

        /// <summary>
        /// Formats pace (seconds per mile) into "X min YY sec".
        /// </summary>
        private static string FormatPace(double paceSecondsPerMile)
        {
            if (paceSecondsPerMile <= 0) return "n/a";
            var ts = TimeSpan.FromSeconds(paceSecondsPerMile);
            var mins = (int)ts.TotalMinutes;
            return $"{mins} min {ts.Seconds:00} sec";
        }

        /// <summary>
        /// Produces a friendly distance label (e.g., "5k", "Half Marathon", "3 Miles", etc.) from meters.
        /// Falls back to rounded km text for uncommon distances.
        /// </summary>
        private static string FormatRaceDistanceLabel(double meters)
        {
            if (meters <= 0) return "unknown distance";

            const double metersPerMile = 1609.344;

            var miles = meters / metersPerMile;
            var km = meters / 1000.0;

            // Mile-based distances
            var roundedMiles = Math.Round(miles, 2);
            if (IsClose(roundedMiles, 1)) return "1 Mile";
            if (IsClose(roundedMiles, 3)) return "3 Miles";
            if (IsClose(roundedMiles, 5)) return "5 Miles";
            if (IsClose(roundedMiles, 10)) return "10 Miles";

            // Metric distances
            var roundedKm = Math.Round(km, 1);
            if (IsClose(roundedKm, 5)) return "5k";
            if (IsClose(roundedKm, 10)) return "10k";
            if (IsClose(roundedKm, 21.1)) return "Half Marathon";
            if (IsClose(roundedKm, 42.2)) return "Marathon";

            // Fallback (custom distances)
            return $"{roundedKm:0.#}km";
        }

        #endregion
    }
}
