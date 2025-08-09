﻿using System.Diagnostics;
using System.Text.Json;

namespace Subway;

class Program
{
    Dictionary<string, List<Transition>> Transitions { get; set; }
    Dictionary<string, int> Visits { get; set; } = new();
    List<Result> Results { get; set; } = new();
    int TotalVisits { get; set; }

    void TryRoute(Transition transition, Result result)
    {
        if (!Visits.ContainsKey(transition.EndStation))
            Visits[transition.EndStation] = 0;
        if (Visits[transition.EndStation] == 0) ;
            ++TotalVisits;
        ++Visits[transition.EndStation];

        result.Transitions.Add(transition);

        Console.WriteLine($"{transition.Route} to {transition.EndStation}, arriving {transition.EndTime}");

        if (TotalVisits == Transitions.Count)
        {
            result.StartTime = transition.EndTime;
            Results.Add(result.Copy());
            return;
        }

        foreach (var testTransition in Transitions[transition.EndStation])
        {
            if (transition.EndTime > testTransition.StartTime)
                continue;
            TryRoute(testTransition, result);
        }

        result.Transitions.RemoveAt(result.Transitions.Count - 1);
        --Visits[transition.EndStation];
        if (Visits[transition.EndStation] == 0)
            --TotalVisits;
    }

    public static void Main()
    {
        //var sw = Stopwatch.StartNew();
        //new Extractor().Process(@"C:\dev\Subway\Subway\Routes.jsn");
        //var end = sw.ElapsedMilliseconds;
        //end = end;

        var data = File.ReadAllBytes(@"C:\dev\Subway\Subway\Routes.jsn");
        var transitions = JsonSerializer.Deserialize<List<Transition>>(data);
        var program = new Program { Transitions = transitions.GroupBy(x => x.StartStation).ToDictionary(g => g.Key, g => g.ToList()) };

        var result = new Result { StartStation = "H15" , StartTime = DateTime.Parse("2025-08-11 04:01 AM") };
        program.TryRoute(new Transition { StartStation = "H15", StartTime = DateTime.Parse("2025-08-11 04:01 AM"), EndStation = "H14", EndTime = DateTime.Parse("2025-08-11 04:03 AM"), Route = "S - Rockaway Park Shuttle"}, result);
        Console.WriteLine($"{program.Results.Count} results");
        //Console.WriteLine(string.Join("\n", program.Results.Select(result => $"{result.Duration}: {string.Join(" => ", result.Transitions.Select(t => $"{t.EndStation}: {t.EndTime}"))}")));
        Console.WriteLine("Done");
        Console.ReadKey();
    }
}
