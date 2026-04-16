using System;
using BackupDriver.Core.Config;

namespace BackupDriver.Core.Schedule;

public static class ScheduleCalculator
{
    public static DateTime CalculateNextRun(ScheduleConfig config, DateTime from)
    {
        if (config.Mode == "interval")
        {
            return from.AddHours(config.IntervalHours);
        }

        if (TimeSpan.TryParse(config.FixedTime, out var fixedTime))
        {
            var today = from.Date.Add(fixedTime);
            return today > from ? today : today.AddDays(1);
        }

        return from.AddHours(config.IntervalHours);
    }

    public static string FormatTimeSpan(DateTime nextRun, DateTime now)
    {
        var diff = nextRun - now;
        if (diff.TotalMinutes < 1) return "menos de 1 min";
        if (diff.TotalHours < 1) return $"{diff.Minutes}min";
        return $"{(int)diff.TotalHours}h {diff.Minutes}min";
    }
}