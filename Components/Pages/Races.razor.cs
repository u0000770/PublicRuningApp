namespace PublicApp.Components.Pages
{
    public partial class Races
    {
        private bool _loading = true;
        private string? _error;

        // fixed window you requested
        private readonly DateTime _from = new DateTime(2026, 1, 1);
        private readonly DateTime _to = new DateTime(2026, 12, 1);

        private List<RaceEventListItemVM> _races = new();

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
                    .Where(x => x.Active) // defensive (activeOnly should already do it)
                    .OrderBy(x => x.Date)
                    .Select(x => new RaceEventListItemVM
                    {
                        EventTitle = x.EventTitle,
                        Date = x.Date,
                        DistanceCode = x.DistanceCode,
                        Location = x.Location
                    })
                    .ToList();
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

        public sealed class RaceEventListItemVM
        {
            public string? EventTitle { get; set; }
            public DateTime Date { get; set; }
            public string? DistanceCode { get; set; }
            public string? Location { get; set; }
        }
    }
}