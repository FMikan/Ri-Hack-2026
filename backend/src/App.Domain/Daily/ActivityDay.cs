namespace App.Domain.Daily;

// A local day (Clock.Today) on which the player did something that counts for the streak (idea §13).
public class ActivityDay
{
    public required Guid UserId { get; init; }
    public required DateOnly Day { get; init; }
}
