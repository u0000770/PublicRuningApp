// =============================================================================
// MyDetails.PredictionCap.cs — 7% prediction cap display (added 19 Nov 2026)
// =============================================================================
// A second partial-class file for the My Details page, so the existing
// MyDetails.razor.cs is not changed apart from one call
// (PredictFromRecentRaces -> PredictFromRecentRacesCapped).
//
// WHAT IT DOES:
//   For each race in the "Last 3 races" table it works out the time the
//   calculator used (PredictionCapRule). When any of those races was more than
//   7% slower than its target, the page shows an extra "Calculator Time"
//   column, so the runner can see both their real time and the time used for
//   the prediction (rule R9). If no race was capped the table is unchanged.
//
// Nothing here is saved, and the runner's recorded time is never altered.
// =============================================================================

using RRCServices.Calculator;
using RRCServices.Runner;

namespace PublicApp.Components.Pages
{
    public partial class MyDetails
    {
        /// <summary>
        /// True when at least one of the last three races was capped,
        /// so the "Calculator Time" column and the note are shown.
        /// </summary>
        private bool AnyRaceCapped
        {
            get
            {
                // 7% CAP on/off per season: nothing to show if this season has it off.
                if (_season is null || !_season.ApplyPredictionCap)
                    return false;

                foreach (var r in LastThreeNonMileRaces)
                {
                    if (IsCappedForPrediction(r))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// True when this race was more than 7% slower than its target.
        /// </summary>
        private static bool IsCappedForPrediction(EventRaceTimesDto race)
        {
            return PredictionCapRule.IsCapped(race.ActualSeconds ?? 0, race.TargetTime);
        }

        /// <summary>
        /// The time the calculator used for this race, formatted for display.
        /// </summary>
        private static string CalculatorTimeText(EventRaceTimesDto race)
        {
            int seconds = PredictionCapRule.TimeForPrediction(race.ActualSeconds ?? 0, race.TargetTime);
            return FormatHms(seconds);
        }
    }
}
