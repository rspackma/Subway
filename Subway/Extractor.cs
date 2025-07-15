using System.Text.Json;

namespace Subway;

class Extractor
{
    public static List<string> DayNames { get; } = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    public List<Dictionary<string, string>> StopTimes { get; }
    public List<Dictionary<string, string>> Stops { get; }
    public List<Dictionary<string, string>> Trips { get; }
    public List<Dictionary<string, string>> Routes { get; }
    public List<Dictionary<string, string>> Calendar { get; }

    List<Dictionary<string, string>> ReadCSV(string fileName)
    {
        var data = File.ReadAllLines(fileName).Select(line => line.Split(",")).ToList();
        return data.Skip(1).Select(row => row.Select((col, idx) => new { col, idx }).ToDictionary(o => data[0][o.idx], o => o.col)).ToList();
    }

    public Extractor()
    {
        StopTimes = ReadCSV(@"..\..\..\..\Schedules\GTFS\stop_times.txt");
        Stops = ReadCSV(@"..\..\..\..\Schedules\GTFS\stops.txt");
        Trips = ReadCSV(@"..\..\..\..\Schedules\GTFS\trips.txt");
        Routes = ReadCSV(@"..\..\..\..\Schedules\GTFS\routes.txt");
        Calendar = ReadCSV(@"..\..\..\..\Schedules\GTFS\calendar.txt");
    }

    public void Process(string outputFile)
    {
        var transitions = new List<Transition>();

        var trips = StopTimes.GroupBy(x => x["trip_id"]).Select(g => g.OrderBy(x => int.Parse(x["stop_sequence"])).ToList());
        foreach (var trip in trips)
        {
            var tripTransitions = GetTripTransitions(trip);
            transitions.AddRange(tripTransitions);
        }

        File.WriteAllText(outputFile, JsonSerializer.Serialize(transitions, new JsonSerializerOptions { WriteIndented = true }));
    }

    private List<Transition> GetTripTransitions(List<Dictionary<string, string>> stops)
    {
        var transitions = new List<Transition>();

        var trip = Trips.Where(x => x["trip_id"] == stops[0]["trip_id"]).Single();
        var route = Routes.Where(x => x["route_id"] == trip["route_id"]).Single();
        var routeName = $"{route["route_short_name"]} - {route["route_long_name"]}";

        var calendar = Calendar.Where(x => x["service_id"] == trip["service_id"]).Single();
        var days = DayNames.Select((dayName, dayOfWeek) => new { dayName, dayOfWeek }).Where(o => calendar[o.dayName] == "1").Select(o => o.dayOfWeek).ToList();

        var prevStation = "";
        var prevTime = "";
        foreach (var stop in stops)
            ProcessStop(stop, transitions, routeName, days, ref prevStation, ref prevTime);

        return transitions;
    }

    static TimeSpan ParseTimeSpan(int day, string value)
    {
        var fields = value.Split(":");
        return TimeSpan.FromDays(day) + TimeSpan.FromHours(int.Parse(fields[0])) + TimeSpan.FromMinutes(int.Parse(fields[1]));
    }

    private void ProcessStop(Dictionary<string, string> stop, List<Transition> transitions, string routeName, List<int> days, ref string prevStation, ref string prevTime)
    {
        var stop_name = Stops.Where(x => x["stop_id"] == stop["stop_id"]).Single()["stop_name"];

        if (prevStation != "")
        {
            foreach (var day in days)
            {
                var transition = new Transition
                {
                    StartStation = prevStation,
                    StartTime = ParseTimeSpan(day, prevTime),
                    EndStation = stop_name,
                    EndTime = ParseTimeSpan(day, stop["arrival_time"]),
                    Route = routeName,
                };
                transitions.Add(transition);
            }
        }

        prevStation = stop_name;
        prevTime = stop["departure_time"];
    }
}
