namespace PublicApp.Components.Pages
{
    public partial class Home
    {
        // Inputs (UI only for now)
        private string? Ukan { get; set; }
        private string? FirstName { get; set; }
        private string? SecondName { get; set; }

       
        private DateTime? Dob { get; set; }

        private async Task Continue()
        {
            var runnerId = await RunnerService.FindRunnerIdAsync(
                ukan: Ukan,
                firstName: FirstName,
                secondName: SecondName,
                dob: Dob);

            if (runnerId.HasValue && runnerId.Value > 0)
            {
                Nav.NavigateTo($"/mydetails?RunnerId={runnerId.Value}");
                return;
            }

        }

    }
}