class Transition
{
    public string StartStation { get; set; }
    public TimeSpan StartTime { get; set; }
    public string EndStation { get; set; }
    public TimeSpan EndTime { get; set; }

    public override string ToString() => $"{StartTime}: {StartStation} - {EndTime}: {EndStation}";
}
