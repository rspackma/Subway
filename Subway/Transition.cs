namespace Subway;

class Transition
{
    public required string StartStation { get; set; }
    public required DateTime StartTime { get; set; }
    public required string EndStation { get; set; }
    public required DateTime EndTime { get; set; }
    public required string Route { get; set; }

    public override string ToString() => $"{StartTime}: {StartStation} - {EndTime}: {EndStation}";
}
