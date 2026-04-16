using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BackupDriver.Core.Schedule;

public class SchedulerStatusMessage : ValueChangedMessage<SchedulerStatus>
{
    public SchedulerStatusMessage(SchedulerStatus status) : base(status) { }
}