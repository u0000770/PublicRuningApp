#region old

//using Calculator;
//using Microsoft.AspNetCore.Components;
//using RRCServices;
//using RRCServices.Calculator;
//using RRCServices.Runner;
//using RRCServices.Season;

//namespace PublicApp.Components.Pages
//{
//    public partial class MyDetails
//    {
//        #region Parameters (Query String)

//        /// <summary>
//        /// Runner id supplied from the query string.
//        /// Example: /mydetails?RunnerId=160
//        /// </summary>
//        [Parameter]
//        [SupplyParameterFromQuery]
//        public int RunnerId { get; set; }

//        #endregion

//        #region State (Page / UI)

//        private bool _loading = true;

//        private DateTime _seasonStart;       // DateTime for comparisons (season start boundary)
//        private bool _hasSeasonRaces;        // runner has >= 1 eligible race this season

//        private RunnerDetailsDto? _runner;
//        private List<RaceEventListItemDTO> _races = new();

//        // Selected race id is the “single source of truth” for selection
//        private int? _selectedRaceEventId;

//        // Prediction & messages
//        private int? _predictedSeconds;
//        private string? _calcMessage;
//        private string? _saveMessage;

//        #endregion

//        #region Computed / Derived Properties (Selection + Display)

//        /// <summary>
//        /// Resolves the selected event from the selected id and the loaded list.
//        /// </summary>
//        private RaceEventListItemDTO? SelectedRaceEvent =>
//            _selectedRaceEventId is null
//                ? null
//                : _races.FirstOrDefault(r => r.RaceEventId == _selectedRaceEventId.Value);

//        /// <summary>
//        /// Returns the last three completed, in-season, non-mile races with usable timing + distance.
//        /// </summary>
//        private List<EventRaceTimesDto> LastThreeNonMileRaces =>
//            _runner?.EventTimes
//                .Where(t => t.RaceDate.HasValue)
//                .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date) // completed only
//                .Where(t => t.RaceDate!.Value >= _seasonStart)       // in-season only
//                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
//                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
//                .Where(t => !IsOneMile(t.DistanceMeters!.Value))     // exclude 1 mile
//                .OrderByDescending(t => t.RaceDate)
//                .Take(3)
//                .ToList()
//            ?? new List<EventRaceTimesDto>();

//        /// <summary>
//        /// Formatted predicted time display ("00h:00m:00s").
//        /// </summary>
//        private string? TargetTimeText =>
//            _predictedSeconds is null ? null : FormatHms(_predictedSeconds.Value);

//        /// <summary>
//        /// A simple narrative summary of the selected event and time-to-race.
//        /// </summary>
//        private string? SummaryText
//        {
//            get
//            {
//                if (_predictedSeconds is null || SelectedRaceEvent is null)
//                    return null;

//                var title = SelectedRaceEvent.EventTitle ?? "(unknown race)";
//                var distanceLabel = FormatRaceDistanceLabel(SelectedRaceEvent.DistanceMeters);
//                var raceDate = SelectedRaceEvent.Date;

//                var gapDays = (raceDate.Date - Clock.Now.Date).Days;
//                var weeks = gapDays / 7;

//                return $"{title} is a {distanceLabel} race on {raceDate:d MMMM} and you have {weeks} weeks to prepare";
//            }
//        }

//        /// <summary>
//        /// Predicted pace per mile display ("X min YY sec").
//        /// </summary>
//        private string? PacePerMileText
//        {
//            get
//            {
//                if (_predictedSeconds is null || SelectedRaceEvent is null)
//                    return null;

//                var miles = SelectedRaceEvent.DistanceMeters * 0.00062137119;
//                if (miles <= 0) return null;

//                var paceSecondsPerMile = _predictedSeconds.Value / miles;
//                return FormatPace(paceSecondsPerMile);
//            }
//        }

//        #endregion

//        #region Trophy Races (Projection)

//        /// <summary>
//        /// Row model for displaying race history with age-grade.
//        /// </summary>
//        private sealed class TrophyRaceRow
//        {
//            public string RaceTitle { get; init; } = "";
//            public string DistanceLabel { get; init; } = "";
//            public DateTime RaceDate { get; init; }
//            public string RecordedTime { get; init; } = "";
//            public string AgeGrade { get; init; } = "";
//        }

//        /// <summary>
//        /// Projects completed races into a table-friendly structure including age grade score.
//        /// </summary>
//        private List<TrophyRaceRow> TrophyRaces =>
//            _runner?.EventTimes
//                .Where(t => t.RaceDate.HasValue)
//                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
//                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
//                .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date)
//                .OrderByDescending(t => t.RaceDate)
//                .Select(t =>
//                {
//                    DateTime? dobDt = _runner.Dob?.ToDateTime(TimeOnly.MinValue);

//                    var ageGrade =
//                        (dobDt.HasValue && t.RaceDate.HasValue && !string.IsNullOrWhiteSpace(t.RaceDistance))
//                            ? AgeGrade.GetWavScore(
//                                time: t.ActualSeconds!.Value,
//                                gender: _runner.IsMale,
//                                dob: dobDt,
//                                RaceCode: t.RaceDistance,     // distance code
//                                RaceDate: t.RaceDate)
//                            : 0;

//                    return new TrophyRaceRow
//                    {
//                        RaceTitle = t.RaceTitle ?? "(unknown)",
//                        DistanceLabel = FormatRaceDistanceLabel(t.DistanceMeters!.Value),
//                        RaceDate = t.RaceDate!.Value,
//                        RecordedTime = FormatHms(t.ActualSeconds!.Value),
//                        AgeGrade = ageGrade.ToString()
//                    };
//                })
//                .ToList()
//            ?? new List<TrophyRaceRow>();

//        #endregion

//        #region Lifecycle (Load / Initialise)

//        /// <summary>
//        /// Loads runner details and upcoming race events based on RunnerId.
//        /// Also selects the first upcoming event and performs an initial prediction.
//        /// </summary>
//        protected override async Task OnParametersSetAsync()
//        {
//            _loading = true;
//            _seasonStart = new DateTime(2025, 12, 1);
//            // Guard: invalid RunnerId
//            if (RunnerId <= 0)
//            {
//                _runner = null;
//                _loading = false;
//                return;
//            }

//            // TODO: restore season settings service usage when ready
//           //  var season = await SeasonSettingsService.GetAsync();
//           //  _seasonStart = season.SeasonStartDate.ToDateTime(TimeOnly.MinValue);

//            // Load runner details
//            _runner = await RunnerService.GetRunnerDetailsAsync(RunnerId, includeInactiveTimes: false);

//            // Determine if runner has any eligible in-season races (completed, non-mile, with distance/time)
//            _hasSeasonRaces = _runner.EventTimes.Any(t =>
//                t.RaceDate.HasValue &&
//                t.RaceDate.Value >= _seasonStart &&
//                t.ActualSeconds.HasValue &&
//                t.ActualSeconds.Value > 0 &&
//                t.DistanceMeters.HasValue &&
//                t.DistanceMeters.Value > 0 &&
//                !IsOneMile(t.DistanceMeters.Value));

//            if (_runner is null)
//            {
//                _loading = false;
//                return;
//            }

//            // Load upcoming races (next 3 months)
//            var from = Clock.Now.Date;               // use clock, not DateTime.Today
//            var to = Clock.Now.Date.AddMonths(3);

//            var list = await RaceEventService.GetRaceEventListAsync(
//                activeOnly: true,
//                distanceCode: null,
//                from: from,
//                to: to);

//            _races = list
//                .Where(r => r.Active)
//                .OrderBy(r => r.Date)
//                .ToList();

//            // Default selection = first upcoming race
//            _selectedRaceEventId = _races.FirstOrDefault()?.RaceEventId;

//            // Initial calculation so UI shows results immediately
//            Calculate();

//            _loading = false;
//        }

//        #endregion

//        #region UI Event Handlers

//        /// <summary>
//        /// Called when the user changes the selected race event.
//        /// Triggers recalculation of prediction and derived display strings.
//        /// </summary>
//        private void OnSelectedRaceChanged()
//        {
//            Calculate();
//        }

//        #endregion

//        #region Core Calculation (Prediction)

//        /// <summary>
//        /// Extracts distance in meters from the selected race event DTO.
//        /// </summary>
//        private static double GetRaceEventDistanceMeters(RaceEventListItemDTO re) => re.DistanceMeters;

//        /// <summary>
//        /// Calculates predicted time (seconds) for the selected event using up to 3 recent eligible races.
//        /// Also sets user-facing messages when prediction cannot be produced.
//        /// </summary>
//        private void Calculate()
//        {
//            _predictedSeconds = null;
//            _calcMessage = null;
//            _saveMessage = null;

//            if (_runner is null || SelectedRaceEvent is null)
//            {
//                _calcMessage = "Please select a runner and a race event.";
//                return;
//            }

//            if (!_hasSeasonRaces)
//            {
//                _calcMessage = $"We can’t provide a predicted time yet — please run at least one race this season (from {_seasonStart:dd MMM yyyy}).";
//                return;
//            }

//            var newDistanceMeters = GetRaceEventDistanceMeters(SelectedRaceEvent);
//            if (newDistanceMeters <= 0)
//            {
//                _calcMessage = "Could not determine the selected race distance (meters).";
//                return;
//            }

//            var today = Clock.Now.Date;

//            // Build “recent” dataset (up to 3 completed, in-season, non-mile races)
//            var recent = _runner.EventTimes
//                .Where(t => t.RaceDate.HasValue && t.RaceDate.Value.Date < today)
//                .Where(t => t.RaceDate!.Value >= _seasonStart)            // in-season only
//                .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
//                .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
//                .Where(t => !IsOneMile(t.DistanceMeters!.Value))          // exclude mile
//                .OrderByDescending(t => t.RaceDate)
//                .Take(3)
//                .Select(t => new RecentRaceDto
//                {
//                    Distance = t.DistanceMeters!.Value,
//                    Actual = t.ActualSeconds!.Value
//                })
//                .ToList();

//            if (recent.Count == 0)
//            {
//                _calcMessage = $"We can’t provide a predicted time yet — please run at least one eligible race this season (from {_seasonStart:dd MMM yyyy}).";
//                return;
//            }

//            var predicted = CalculatorService.PredictFromRecentRaces(recent, newDistanceMeters);
//            if (predicted is null)
//            {
//                _calcMessage = "Prediction failed (no usable recent race data).";
//                return;
//            }

//            _predictedSeconds = (int)Math.Round(predicted.Value);
//            _calcMessage = $"Prediction calculated using {recent.Count} race(s) from this season.";
//        }

//        #endregion

//        #region Utilities (Distance Checks)

//        /// <summary>
//        /// True if the provided meters value is approximately one mile (tolerance included).
//        /// </summary>
//        private static bool IsOneMile(double meters)
//        {
//            const double oneMileMeters = 1609.34;
//            return Math.Abs(meters - oneMileMeters) <= 5; // +/- 5 meters tolerance
//        }

//        /// <summary>
//        /// Compares two doubles for approximate equality within a tolerance.
//        /// Used for mapping distance values to friendly labels.
//        /// </summary>
//        private static bool IsClose(double actual, double expected, double tolerance = 0.05)
//        {
//            return Math.Abs(actual - expected) <= tolerance;
//        }

//        #endregion

//        #region Formatting Helpers (Time / Pace / Distance Labels)

//        /// <summary>
//        /// Formats seconds into a HHh:MMm:SSs string.
//        /// </summary>
//        private static string FormatHms(int totalSeconds)
//        {
//            var ts = TimeSpan.FromSeconds(totalSeconds);
//            return $"{(int)ts.TotalHours:00}h:{ts.Minutes:00}m:{ts.Seconds:00}s";
//        }

//        /// <summary>
//        /// Formats pace (seconds per mile) into "X min YY sec".
//        /// </summary>
//        private static string FormatPace(double paceSecondsPerMile)
//        {
//            if (paceSecondsPerMile <= 0) return "n/a";
//            var ts = TimeSpan.FromSeconds(paceSecondsPerMile);
//            var mins = (int)ts.TotalMinutes;
//            return $"{mins} min {ts.Seconds:00} sec";
//        }

//        /// <summary>
//        /// Produces a friendly distance label (e.g., "5k", "Half Marathon", "3 Miles", etc.) from meters.
//        /// Falls back to rounded km text for uncommon distances.
//        /// </summary>
//        private static string FormatRaceDistanceLabel(double meters)
//        {
//            if (meters <= 0) return "unknown distance";

//            const double metersPerMile = 1609.344;

//            var miles = meters / metersPerMile;
//            var km = meters / 1000.0;

//            // Mile-based distances
//            var roundedMiles = Math.Round(miles, 2);
//            if (IsClose(roundedMiles, 1)) return "1 Mile";
//            if (IsClose(roundedMiles, 3)) return "3 Miles";
//            if (IsClose(roundedMiles, 5)) return "5 Miles";
//            if (IsClose(roundedMiles, 10)) return "10 Miles";

//            // Metric distances
//            var roundedKm = Math.Round(km, 1);
//            if (IsClose(roundedKm, 5)) return "5k";
//            if (IsClose(roundedKm, 10)) return "10k";
//            if (IsClose(roundedKm, 21.1)) return "Half Marathon";
//            if (IsClose(roundedKm, 42.2)) return "Marathon";

//            // Fallback (custom distances)
//            return $"{roundedKm:0.#}km";
//        }

//        #endregion
//    }
//}

#endregion

// =============================================================================
// new_MyDetails.razor.cs
// =============================================================================
// WHAT CHANGED FROM MyDetails.razor.cs:
//
//   1. Calculate() — the inline LINQ block that built the 'recent' race list
//      has been replaced with a single call to the new centralised method:
//          CalculatorService.SelectRacesForPredictionInput(...)
//      This ensures the public app uses identical input-selection rules to the
//      admin app, and that the mile exclusion rule lives in one place only.
//
//   2. Season dates — _seasonStart is now read from ISeasonSettingsService
//      rather than being hardcoded as new DateTime(2025, 12, 1).
//      The admin SeasonDates page can now update dates and this page will
//      reflect them without a code change or redeployment.
//
//   3. LastThreeNonMileRaces — the display property that shows the runner's
//      recent races in the UI now also delegates to SelectRacesForPredictionInput
//      and projects the result back to EventRaceTimesDto for display. This
//      ensures the UI table and the prediction input use the same races.
//
//   4. _hasSeasonRaces check — updated to use SelectRacesForPredictionInput
//      so the "no eligible races" guard uses the same criteria as the
//      prediction itself.
//
//   5. IsOneMile() — private helper removed. It is no longer needed here
//      because the mile exclusion logic now lives in CalculatorService.
//      IsClose() and the formatting helpers are unchanged.
//
// WHAT DID NOT CHANGE:
//   All state fields, computed properties, lifecycle methods, UI event
//   handlers, formatting helpers, TrophyRaces, SummaryText, PacePerMileText,
//   ConfirmUpdate, navigation methods — all unchanged.
// =============================================================================

using XCalculator;
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

        // Selected race id is the "single source of truth" for selection
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
        ///
        /// CHANGE: now delegates to CalculatorService.SelectRacesForPredictionInput so the
        /// races shown in the UI table are exactly the same races used in the prediction.
        /// Previously this had its own inline LINQ with its own IsOneMile check — using
        /// the shared method ensures the two cannot drift apart.
        /// </summary>
        private List<EventRaceTimesDto> LastThreeNonMileRaces
        {
            get
            {
                if (_runner is null) return new List<EventRaceTimesDto>();

                // SelectRacesForPredictionInput returns RecentRaceDto (distance + actual only).
                // We need full EventRaceTimesDto rows for display, so we apply the same
                // filters directly to EventTimes here but using the same rules as the service.
                // This keeps the display in sync without losing the display fields.
                return _runner.EventTimes
                    .Where(t => t.RaceDate.HasValue)
                    .Where(t => t.RaceDate!.Value.Date < Clock.Now.Date)
                    .Where(t => t.RaceDate!.Value >= _seasonStart)
                    .Where(t => t.ActualSeconds.HasValue && t.ActualSeconds.Value > 0)
                    .Where(t => t.DistanceMeters.HasValue && t.DistanceMeters.Value > 0)
                    .Where(t => !CalculatorService.SelectRacesForPredictionInput(
                                    new[] { t }, _seasonStart, Clock.Now.Date).Count.Equals(0))
                    .OrderByDescending(t => t.RaceDate)
                    .Take(3)
                    .ToList();
            }
        }

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
        /// Trophy races include ALL distances including mile — the mile exclusion
        /// applies only to prediction inputs, not to results display or scoring.
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
                                RaceCode: t.RaceDistance,
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
        ///
        /// CHANGE: _seasonStart is now read from ISeasonSettingsService instead of
        /// being hardcoded. The admin SeasonDates page controls this value.
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            _loading = true;

            // Guard: invalid RunnerId
            if (RunnerId <= 0)
            {
                _runner = null;
                _loading = false;
                return;
            }

            // CHANGE: read season start from the settings service rather than
            // hardcoding new DateTime(2025, 12, 1). The admin SeasonDates page
            // manages this value — changes there are now reflected here immediately.
            var season = await SeasonSettingsService.GetAsync();
            _seasonStart = season.SeasonStartDate.ToDateTime(TimeOnly.MinValue);

            // Load runner details
            _runner = await RunnerService.GetRunnerDetailsAsync(RunnerId, includeInactiveTimes: false);

            if (_runner is null)
            {
                _loading = false;
                return;
            }

            // CHANGE: _hasSeasonRaces now delegates to SelectRacesForPredictionInput
            // so the guard uses exactly the same criteria as the prediction itself.
            // Previously this had its own inline check with its own IsOneMile call.
            _hasSeasonRaces = CalculatorService
                .SelectRacesForPredictionInput(_runner.EventTimes, _seasonStart, Clock.Now.Date)
                .Count > 0;

            // Load upcoming races (next 3 months)
            var from = Clock.Now.Date;
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
        ///
        /// CHANGE: the inline LINQ block that built the 'recent' list has been replaced
        /// with a single call to CalculatorService.SelectRacesForPredictionInput.
        /// This method applies all the correct filtering rules including the mile
        /// exclusion, season boundary, and completed-race checks in one place.
        /// The rest of this method is unchanged.
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
                _calcMessage = $"We can't provide a predicted time yet — please run at least one race this season (from {_seasonStart:dd MMM yyyy}).";
                return;
            }

            var newDistanceMeters = GetRaceEventDistanceMeters(SelectedRaceEvent);
            if (newDistanceMeters <= 0)
            {
                _calcMessage = "Could not determine the selected race distance (meters).";
                return;
            }

            // CHANGE: replaced the inline 8-clause LINQ chain with a single call to
            // the centralised method. All filtering rules (completed, in-season,
            // positive time, known distance, mile exclusion) are enforced inside
            // SelectRacesForPredictionInput in CalculatorService.
            var recent = CalculatorService.SelectRacesForPredictionInput(
                eventTimes: _runner.EventTimes,
                seasonStart: _seasonStart,
                today: Clock.Now.Date,
                maxRaces: 3);

            if (recent.Count == 0)
            {
                _calcMessage = $"We can't provide a predicted time yet — please run at least one eligible race this season (from {_seasonStart:dd MMM yyyy}).";
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

        #region Formatting Helpers (Time / Pace / Distance Labels)

        // NOTE: IsOneMile() has been removed from this file.
        // The mile exclusion is now handled exclusively inside
        // CalculatorService.SelectRacesForPredictionInput — there is no
        // longer any reason to duplicate the check here.

        /// <summary>
        /// Compares two doubles for approximate equality within a tolerance.
        /// Used for mapping distance values to friendly labels.
        /// </summary>
        private static bool IsClose(double actual, double expected, double tolerance = 0.05)
        {
            return Math.Abs(actual - expected) <= tolerance;
        }

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

