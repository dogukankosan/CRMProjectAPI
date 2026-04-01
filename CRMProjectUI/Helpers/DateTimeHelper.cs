namespace CRMProjectUI.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo TurkeyZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows()
                    ? "Turkey Standard Time"
                    : "Europe/Istanbul");
        public static DateTime NowTurkey =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TurkeyZone);
    }
}