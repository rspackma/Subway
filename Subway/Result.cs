class Result
{
    public TimeSpan Duration { get; set; }
    public string StartStation { get; set; }
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
