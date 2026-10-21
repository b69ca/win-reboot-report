using System.Text.Json.Nodes;
namespace WinDiagnostics;
public static class Tool
{
    public const string Name="win-reboot-report";
    public static readonly string[] ValueOptions=["--days","--max-events"];
    public static readonly string[] Flags=[];
    public const string Help="win-reboot-report [--days 7] [--max-events 300] [--json]\nReads a chronological System event timeline and last boot time. Event clues are not automatic root-cause diagnoses.";
    public static void ValidateOptions(Options o){o.Number("--days",7,1,30);o.Number("--max-events",300,10,1000);}
    public static Task<Collection> Collect(Options o,CancellationToken ct)=>PowerShell.Collect(new(){["WD_DAYS"]=o.Number("--days",7,1,30).ToString(),["WD_MAX"]=o.Number("--max-events",300,10,1000).ToString()},ct);
    public static void ValidateEvidence(JsonObject e){J.Require(e,"events","lastBootUtc");J.RequireRows(e,"events");}
    public static List<Finding> Analyze(JsonObject e)
    {
        var r=new List<Finding>();var events=J.Rows(e,"events").ToArray();
        if(events.Any(x=>J.Number(x,"id")==41&&J.Text(x,"provider")=="Microsoft-Windows-Kernel-Power"||J.Number(x,"id")==6008&&J.Text(x,"provider")=="EventLog"))r.Add(new("unclean-shutdown","warning","Windows recorded an unclean shutdown","Kernel-Power 41 / EventLog 6008 indicate an unexpected interruption. They do not distinguish power loss, a crash, a forced reset, or a hang.","Correlate bugcheck records, hardware logs, power events, and the surrounding timeline."));
        if(events.Any(x=>J.Number(x,"id")==1001&&J.Text(x,"provider")=="Microsoft-Windows-WER-SystemErrorReporting"))r.Add(new("bugcheck-record","warning","A system error-reporting record is present","The timeline contains a system error-reporting event 1001; inspect its recorded bugcheck/dump details.","Use the referenced dump and debugging tools to investigate the recorded stop code."));
        if(events.Any(x=>J.Number(x,"id")==1074&&J.Text(x,"provider")=="User32"))r.Add(new("requested-restart","info","A process-requested shutdown or restart was recorded","User32 1074 messages identify the recorded initiating process/account and reason. This does not explain every restart in the window.","Match the specific event timestamp with the restart you are investigating."));
        if(J.Bool(e,"limitReached"))r.Add(new("timeline-capped","warning","The event limit was reached","Earlier matching records may have been omitted.","Reduce --days or increase --max-events up to 1000."));return r;
    }
    public static IEnumerable<string> Describe(JsonObject e){yield return $"Last boot (UTC): {J.Text(e,"lastBootUtc","unavailable")}";foreach(var row in J.Rows(e,"events").OrderBy(x=>J.Text(x,"timeUtc")))yield return $"{J.Text(row,"timeUtc")} | {J.Text(row,"provider")} {J.Text(row,"id")} | {J.Text(row,"message")}";}
    public static string[] SmokeArgs=>["--days","1","--max-events","10"];
}
