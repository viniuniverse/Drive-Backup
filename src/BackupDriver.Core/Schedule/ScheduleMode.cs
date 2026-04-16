namespace BackupDriver.Core.Schedule;

public enum ScheduleMode
{
    Interval,
    FixedTime
}

public class ScheduleSettings
{
    public ScheduleMode Mode { get; set; } = ScheduleMode.Interval;
    public int IntervalHours { get; set; } = 4;
    public string FixedTime { get; set; } = "02:00";
    public bool Active { get; set; }
}

public interface ISchedulerService
{
    Task<bool> ScheduleAsync(ScheduleSettings settings, string appPath);
    Task<bool> RemoveScheduleAsync();
    Task<DateTime?> GetNextRunTimeAsync();
    bool IsScheduled();
}
