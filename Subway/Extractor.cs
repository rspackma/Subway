using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Subway;

class Extractor
{
    public static DateTime StartTime { get; } = DateTime.Parse("2025-08-11 04:00 AM");
    public static DateTime EndTime { get; } = DateTime.Parse("2025-08-12 04:00 AM");
    public static DateTime SundayTime { get; } = StartTime - StartTime.TimeOfDay - TimeSpan.FromDays((int)StartTime.DayOfWeek);

    public List<Dictionary<string, string>> StopTimes { get; }
    public List<Dictionary<string, string>> Stops { get; }
    public List<Dictionary<string, string>> Trips { get; }
    public List<Dictionary<string, string>> Routes { get; }
    public List<Dictionary<string, string>> Calendar { get; }
    public List<Dictionary<string, string>> Transfers { get; }

    public Dictionary<string, string> redundantStations = new Dictionary<string, string>
    {
        {"140", "142"},
        {"D13", "A12"},
        {"D20", "A32"},
        {"H19", "H04"},
        {"N12", "D43"},
        {"R09", "718"}
    };

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
        Transfers = ReadCSV(@"..\..\..\..\Schedules\GTFS\transfers.txt");
    }

    private HashSet<string> GetNoTransferStops(Dictionary<string, string> stopMap)
    {
        var tripRouteMap = Trips.ToDictionary(x => x["trip_id"], x => x["route_id"]);
        var stopRoutes = StopTimes
            .Select(x => new { stop_id = stopMap[x["stop_id"]], route_id = tripRouteMap[x["trip_id"]] })
            .GroupBy(x => x.stop_id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.route_id).Distinct().ToList());
        return StopTimes.Select(x => x["stop_id"]).Distinct().Where(x => stopRoutes[stopMap[x]].Count <= 1).ToHashSet();
    }

    public void Process(string outputFile)
    {
        var stopMap = Stops.ToDictionary(x => x["stop_id"], x => x["parent_station"] == "" ? x["stop_id"] : x["parent_station"]);

        var noTransferStops = GetNoTransferStops(stopMap);

        var transitions = new List<Transition>();

        var trips = StopTimes.GroupBy(x => x["trip_id"]).Select(g => g.OrderBy(x => int.Parse(x["stop_sequence"])).ToList());
        foreach (var trip in trips)
        {
            var tripTransitions = GetTripTransitions(trip, stopMap);
            //RemoveNoTransferTransitions(tripTransitions, noTransferStops);
            RemoveInvalidTimeTransitions(tripTransitions);
            RemoveStatenIslandTransitions(tripTransitions);
            var transferTransitions = GetTransferTransitions(tripTransitions, stopMap);
            //AddStopNames(tripTransitions);
            //AddStopNames(transferTransitions);
            transitions.AddRange(tripTransitions);
            transitions.AddRange(transferTransitions);
        }

        foreach(var transition in transitions)
        {
            if(redundantStations.Keys.Contains(transition.StartStation))
            {
                transition.StartStation = redundantStations[transition.StartStation];
            }
            if (redundantStations.Keys.Contains(transition.EndStation))
            {
                transition.EndStation = redundantStations[transition.EndStation];
            }
        }

        File.WriteAllText(outputFile, JsonSerializer.Serialize(transitions, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void RemoveNoTransferTransitions(List<Transition> transitions, HashSet<string> noTransferStops)
    {
        var index = 0;
        while (index + 1 < transitions.Count)
        {
            var transition = transitions[index];
            if (noTransferStops.Contains(transition.EndStation))
            {
                //transition.PassedStations.Add(transition.EndStation);
                transition.EndStation = transitions[index + 1].EndStation;
                transition.EndTime = transitions[index + 1].EndTime;
                transitions.RemoveAt(index + 1);
            }
            else
                ++index;
        }
    }

    private void RemoveInvalidTimeTransitions(List<Transition> transitions)
    {
        var index = 0;
        while (index < transitions.Count)
        {
            var transition = transitions[index];
            if ((transition.StartTime >= StartTime) && (transition.EndTime <= EndTime))
                ++index;
            else
                transitions.RemoveAt(index);
        }
    }

    private void RemoveStatenIslandTransitions(List<Transition> transitions)
    {
        var index = 0;
        while (index < transitions.Count)
        {
            var transition = transitions[index];
            if (transition.Route != "SIR - Staten Island Railway")
                ++index;
            else
                transitions.RemoveAt(index);
        }
    }

    private void AddStopNames(List<Transition> transitions)
    {
        foreach (var transition in transitions)
        {
            transition.StartStation = Stops.Where(x => x["stop_id"] == transition.StartStation).Single()["stop_name"];
            transition.EndStation = Stops.Where(x => x["stop_id"] == transition.EndStation).Single()["stop_name"];
            //int i = 0;
            //while(i < transition.PassedStations.Count)
            //{
            //    transition.PassedStations[i] = Stops.Where(x => x["stop_id"] == transition.PassedStations[i]).Single()["stop_name"];
            //    i++;
            //}
        }
    }

    private List<Transition> GetTripTransitions(List<Dictionary<string, string>> stops, Dictionary<string, string> stopMap)
    {
        var transitions = new List<Transition>();

        var trip = Trips.Where(x => x["trip_id"] == stops[0]["trip_id"]).Single();
        var route = Routes.Where(x => x["route_id"] == trip["route_id"]).Single();
        var routeName = $"{route["route_short_name"]} - {route["route_long_name"]}";

        var calendar = Calendar.Where(x => x["service_id"] == trip["service_id"]).Single();
        var days = Enum.GetValues<DayOfWeek>().Select(x => new { dayName = x.ToString().ToLowerInvariant(), dayOfWeek = (int)x }).Where(o => calendar[o.dayName] == "1").Select(o => o.dayOfWeek).ToList();

        var prevStation = stopMap[stops[0]["stop_id"]];
        var prevTime = stops[0]["departure_time"];
        foreach (var stop in stops)
            ProcessStop(stop, stopMap, transitions, routeName, days, ref prevStation, ref prevTime);

        return transitions;
    }

    private List<Transition> GetTransferTransitions(List<Transition> tripTransitions, Dictionary<string, string> stopMap)
    {
        var transitions = new List<Transition>();

        foreach(var transition in tripTransitions)
        {
            foreach(var transfer in Transfers)
            {
                if (transfer["from_stop_id"] == transition.EndStation && transfer["from_stop_id"] != transfer["to_stop_id"])
                {
                    var transferTransition = new Transition
                    {
                        StartStation = transition.EndStation,
                        StartTime = transition.EndTime +(new TimeSpan(0, 0, 0, 10)),
                        EndStation = transfer["to_stop_id"],
                        EndTime = transition.EndTime +(new TimeSpan(0, 0, 0, 30)),
                        Route = "Transfer",
                    };
                    transitions.Add(transferTransition);
                }

            }

        }

        return transitions;
    }

    static DateTime ParseDateTime(int day, string value)
    {
        var fields = value.Split(":");
        return SundayTime + TimeSpan.FromDays(day) + TimeSpan.FromHours(int.Parse(fields[0])) + TimeSpan.FromMinutes(int.Parse(fields[1]));
    }

    private void ProcessStop(Dictionary<string, string> stop, Dictionary<string, string> stopMap, List<Transition> transitions, string routeName, List<int> days, ref string prevStation, ref string prevTime)
    {
        if (prevStation != stopMap[stop["stop_id"]])
        {
            foreach (var day in days)
            {
                var transition = new Transition
                {
                    StartStation = prevStation,
                    StartTime = ParseDateTime(day, prevTime),
                    EndStation = stopMap[stop["stop_id"]],
                    EndTime = ParseDateTime(day, stop["arrival_time"]),
                    Route = routeName,
                };
                transitions.Add(transition);
            }
        }

        prevStation = stopMap[stop["stop_id"]];
        prevTime = stop["departure_time"];
    }
}
