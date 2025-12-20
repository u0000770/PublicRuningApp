using RRCServices.Clock;

namespace PublicApp
{
    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
