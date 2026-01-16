using System.Globalization;

namespace PublicApp.Components.Pages
{
    public partial class Races
    {
        private bool _loading = true;
        private string? _error;

        private readonly DateTime _from = new DateTime(2026, 1, 1);
        private readonly DateTime _to = new DateTime(2026, 12, 1);

        private List<RaceEventListItemVM> _races = new();

        // filter state
        private string? _selectedDistance = string.Empty;
        private int? _selectedMonth;
        private string? _selectedLocation = string.Empty;

        // filter option lists
        private List<string> _distanceOptions = new();
        private List<int> _monthOptions = new();
        private List<string> _locationOptions = new();

        private IEnumerable<RaceEventListItemVM> FilteredRaces =>
            _races
                .Where(r => string.IsNullOrEmpty(_selectedDistance) || r.DistanceCode == _selectedDistance)
                .Where(r => !_selectedMonth.HasValue || r.Date.Month == _selectedMonth.Value)
                .Where(r => string.IsNullOrEmpty(_selectedLocation) || r.Location == _selectedLocation);

        protected override async Task OnInitializedAsync()
        {
            _loading = true;
            _error = null;
            _races.Clear();

            try
            {
                var dto = await RaceEventService.GetRaceEventListAsync(
                    activeOnly: true,
                    distanceCode: null,
                    from: _from,
                    to: _to);

                _races = dto
                    .Where(x => x.Active)
                    .OrderBy(x => x.Date)
                    .Select(x => new RaceEventListItemVM
                    {
                        EventTitle = x.EventTitle,
                        Date = x.Date,
                        DistanceCode = x.DistanceCode,
                        Location = x.Location
                    })
                    .ToList();

                BuildFilterOptions();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }

        private void BuildFilterOptions()
        {
            _distanceOptions = _races
                .Select(r => r.DistanceCode)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            _monthOptions = _races
                .Select(r => r.Date.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            _locationOptions = _races
                .Select(r => r.Location)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .OrderBy(l => l)
                .ToList();
        }

        private void ResetFilters()
        {
            _selectedDistance = string.Empty;
            _selectedMonth = null;
            _selectedLocation = string.Empty;
        }

        private static string GetMonthName(int month) =>
            CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

        public sealed class RaceEventListItemVM
        {
            public string? EventTitle { get; set; }
            public DateTime Date { get; set; }
            public string? DistanceCode { get; set; }
            public string? Location { get; set; }
        }
    }
}
