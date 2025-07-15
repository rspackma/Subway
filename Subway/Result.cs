namespace Subway;

class Result
{
    public required TimeSpan Duration { get; set; }
    public required string StartStation { get; set; }
    public List<Transition> Transitions { get; set; } = new();

    public Result Copy()
    {
        return new Result
        {
            Duration = Duration,
            StartStation = StartStation,
            Transitions = Transitions.ToList(),
        };
    }
}
