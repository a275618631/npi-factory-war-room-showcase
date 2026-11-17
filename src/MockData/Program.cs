using System;
using System.IO;
using MockData;

var options = new ScenarioOptions();

for (var i = 0; i < args.Length; i++)
{
    if ((args[i] == "--out-dir" || args[i] == "-o") && i + 1 < args.Length)
    {
        options.OutputDirectory = args[++i];
    }
    else if ((args[i] == "--seed" || args[i] == "-s") && i + 1 < args.Length && int.TryParse(args[i + 1], out var seed))
    {
        options.Seed = seed;
        i++;
    }
}

Console.WriteLine($"[MockData] Generating DVT scenario data with Seed={options.Seed}...");
var dataSet = DvtScenarioGenerator.Generate(options);

var targetDir = Path.GetFullPath(options.OutputDirectory);
DvtScenarioGenerator.WriteCsvFiles(dataSet, targetDir);

Console.WriteLine($"[MockData] Successfully wrote CSV files to: {targetDir}");
Console.WriteLine($"  - OrgUnits:     {dataSet.OrgUnits.Count}");
Console.WriteLine($"  - Persons:      {dataSet.Persons.Count}");
Console.WriteLine($"  - Projects:     {dataSet.Projects.Count}");
Console.WriteLine($"  - StagePlans:   {dataSet.StagePlans.Count}");
Console.WriteLine($"  - Deliverables: {dataSet.Deliverables.Count}");
Console.WriteLine($"  - Blockers:     {dataSet.Blockers.Count}");


