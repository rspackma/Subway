namespace Subway;

class Result
{
    public required DateTime StartTime { get; set; }
    public required string StartStation { get; set; }
    public List<Transition> Transitions { get; set; } = new();

    public Result Copy()
    {
        return new Result
        {
            StartTime = StartTime,
            StartStation = StartStation,
            Transitions = Transitions.ToList(),
        };
    }
}
