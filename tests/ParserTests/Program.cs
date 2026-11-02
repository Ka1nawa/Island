using System;
using System.Globalization;
using System.Linq;
using Island;
using Island.Services;
using Island.ViewModels;

var testData = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata-" + Guid.NewGuid().ToString("N"));
System.IO.Directory.CreateDirectory(testData);
Environment.SetEnvironmentVariable("ISLAND_DATA_DIR", testData);

var now = new DateTime(2026, 11, 2, 10, 0, 0); // Monday
var failures = 0;

void Check(string name, bool ok, string detail)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " -> " + detail : ""));
    if (!ok) failures++;
}

var p1 = NaturalTimeParser.Parse("через 20 минут", now);
Check("relative minutes", p1.Ok && p1.Repeat == "Once" && p1.Time == "10:20" && p1.Date == "2026-11-02",
    p1.Repeat + " " + p1.Time + " " + p1.Date);

var p2 = NaturalTimeParser.Parse("через 2 часа", now);
Check("relative hours", p2.Ok && p2.Time == "12:00", p2.Time);

var p3 = NaturalTimeParser.Parse("каждый час", now);
Check("hourly", p3.Ok && p3.Repeat == "Hourly" && p3.Time == "10:00", p3.Repeat + " " + p3.Time);

var p4 = NaturalTimeParser.Parse("каждый день в 9:30", now);
Check("daily", p4.Ok && p4.Repeat == "Daily" && p4.Time == "09:30", p4.Repeat + " " + p4.Time);

var p5 = NaturalTimeParser.Parse("в среду в 15:00", now);
Check("weekly wednesday", p5.Ok && p5.Repeat == "Weekly" && p5.Weekdays == 4 && p5.Time == "15:00",
    p5.Repeat + " " + p5.Weekdays + " " + p5.Time);

var p6 = NaturalTimeParser.Parse("завтра в 10", now);
Check("tomorrow", p6.Ok && p6.Date == "2026-11-03" && p6.Time == "10:00", p6.Date + " " + p6.Time);

var p7 = NaturalTimeParser.Parse("15:45", now);
Check("bare time", p7.Ok && p7.Time == "15:45" && p7.Date == "2026-11-02", p7.Date + " " + p7.Time);

var p8 = NaturalTimeParser.Parse("in 20 minutes", now);
Check("english relative", p8.Ok && p8.Time == "10:20", p8.Time);

var p9 = NaturalTimeParser.Parse("every monday at 9", now);
Check("english weekly", p9.Ok && p9.Repeat == "Weekly" && p9.Weekdays == 1 && p9.Time == "09:00",
    p9.Repeat + " " + p9.Weekdays + " " + p9.Time);

var p10 = NaturalTimeParser.Parse("по будням", now);
Check("weekdays mask", p10.Ok && p10.Repeat == "Weekly" && p10.Weekdays == 31, p10.Weekdays.ToString());

var p11 = NaturalTimeParser.Parse("просто текст", now);
Check("garbage rejected", !p11.Ok, p11.Ok.ToString());

var p12 = NaturalTimeParser.Parse("каждый вторник", now);
Check("tuesday", p12.Ok && p12.Weekdays == 2 && p12.Time == "10:00", p12.Weekdays + " " + p12.Time);

var p13 = NaturalTimeParser.Parse("on wednesday at 15:00", now);
Check("english weekly time", p13.Ok && p13.Weekdays == 4 && p13.Time == "15:00", p13.Weekdays + " " + p13.Time);

var p14 = NaturalTimeParser.Parse("через полчаса", now);
Check("half an hour", p14.Ok && p14.Time == "10:30", p14.Time);

Check("strip schedule", NaturalTimeParser.StripSchedule("выпить воды через 20 минут") == "выпить воды",
    NaturalTimeParser.StripSchedule("выпить воды через 20 минут"));

Check("strip daily", NaturalTimeParser.StripSchedule("зарядка каждый день в 9:30") == "зарядка",
    NaturalTimeParser.StripSchedule("зарядка каждый день в 9:30"));

Check("quiet hours inside", NotificationPolicy.IsQuietHours(true, "23:00", "08:00", new DateTime(2026, 11, 2, 23, 30, 0)), "23:30");
Check("quiet hours early morning", NotificationPolicy.IsQuietHours(true, "23:00", "08:00", new DateTime(2026, 11, 2, 7, 59, 0)), "07:59");
Check("quiet hours boundary", !NotificationPolicy.IsQuietHours(true, "23:00", "08:00", new DateTime(2026, 11, 2, 8, 0, 0)), "08:00");
Check("quiet hours outside", !NotificationPolicy.IsQuietHours(true, "23:00", "08:00", new DateTime(2026, 11, 2, 22, 59, 0)), "22:59");
Check("quiet hours disabled", !NotificationPolicy.IsQuietHours(false, "00:00", "23:59", new DateTime(2026, 11, 2, 12, 0, 0)), "disabled");

Check("warning storm", WeatherViewModel.WarningFor(new WeatherNow { Code = 95, Wind = 5 }) == "WarnStorm",
    WeatherViewModel.WarningFor(new WeatherNow { Code = 95, Wind = 5 }));
Check("warning wind", WeatherViewModel.WarningFor(new WeatherNow { Code = 0, Wind = 55 }) == "WarnWind",
    WeatherViewModel.WarningFor(new WeatherNow { Code = 0, Wind = 55 }));
Check("warning heavy rain", WeatherViewModel.WarningFor(new WeatherNow { Code = 65, Wind = 5 }) == "WarnHeavyRain",
    WeatherViewModel.WarningFor(new WeatherNow { Code = 65, Wind = 5 }));
Check("warning none", WeatherViewModel.WarningFor(new WeatherNow { Code = 0, Wind = 5 }) == "",
    "none");


var ics = @"BEGIN:VCALENDAR
BEGIN:VEVENT
UID:standup
SUMMARY:Standup
DTSTART;TZID=Europe/Moscow:20261103T100000
DTEND;TZID=Europe/Moscow:20261103T101500
RRULE:FREQ=WEEKLY;BYDAY=TU
END:VEVENT
BEGIN:VEVENT
UID:cancelled
SUMMARY:Cancelled
DTSTART:20261102T120000Z
STATUS:CANCELLED
END:VEVENT
BEGIN:VEVENT
UID:daily
SUMMARY:Daily
DTSTART;VALUE=DATE:20261102
DTEND;VALUE=DATE:20261103
RRULE:FREQ=DAILY;COUNT=2
EXDATE;VALUE=DATE:20261103
END:VEVENT
BEGIN:VEVENT
UID:zulu
SUMMARY:Zulu
DTSTART:20261104T070000Z
DTEND:20261104T080000Z
END:VEVENT
END:VCALENDAR";

var icsEvents = IcsCalendar.Parse(ics, new DateTime(2026, 11, 2), new DateTime(2026, 11, 10));
var standup = icsEvents.Where(e => e.Summary == "Standup").ToList();
Check("ics weekly", standup.Count == 1 && DateTime.Parse(standup[0].Start).Hour == 10 && DateTime.Parse(standup[0].Start).Day == 3,
    standup.Count + " " + (standup.Count > 0 ? standup[0].Start : ""));
Check("ics cancelled skipped", icsEvents.All(e => e.Summary != "Cancelled"), "");
var daily = icsEvents.Where(e => e.Summary == "Daily").ToList();
Check("ics exdate", daily.Count == 1 && DateTime.Parse(daily[0].Start).Day == 2, daily.Count.ToString());
Check("ics all-day", daily.Count == 1 && daily[0].AllDay, "");
var zulu = icsEvents.FirstOrDefault(e => e.Summary == "Zulu");
Check("ics zulu to local", zulu is not null && DateTime.Parse(zulu.Start).Hour == 10,
    zulu is null ? "missing" : DateTime.Parse(zulu.Start).ToString("HH:mm"));


var notesFile = System.IO.Path.Combine(testData, "notes-export.json");
var syncSettings = new AppSettings();
syncSettings.Notes.Add(new NoteItem { Id = "a", Title = "A", Body = "- [ ] вода", Updated = "2026-11-01T10:00:00.0000000+03:00" });
NotesSync.ExportFile(notesFile, syncSettings.Notes);
var importedNotes = NotesSync.ImportFile(notesFile);
Check("notes export/import", importedNotes.Count == 1 && importedNotes[0].Title == "A", importedNotes.Count.ToString());

var merged1 = NotesSync.MergeInto(syncSettings, new[]
{
    new NoteItem { Id = "a", Title = "A2", Updated = "2026-11-02T10:00:00.0000000+03:00" }
});
Check("notes merge newer", merged1 == 1 && syncSettings.Notes[0].Title == "A2", merged1 + " " + syncSettings.Notes[0].Title);

var merged2 = NotesSync.MergeInto(syncSettings, new[]
{
    new NoteItem { Id = "a", Title = "A1", Updated = "2026-11-01T09:00:00.0000000+03:00" },
    new NoteItem { Id = "b", Title = "B", Updated = "2026-11-02T11:00:00.0000000+03:00" }
});
Check("notes merge older ignored + new added", merged2 == 1 && syncSettings.Notes.Count == 2 && syncSettings.Notes[0].Title == "A2",
    merged2 + " count=" + syncSettings.Notes.Count);

System.IO.File.Delete(notesFile);

var lrc = "[00:12.50] first line" + (char)10 + "[01:05.00] second line" + (char)10 + "[01:05.00][02:10.00] repeated";
var parsedLrc = LyricsService.ParseLrc(lrc);
Check("lrc parse", parsedLrc.Count == 4 && Math.Abs(parsedLrc[0].Time - 12.5) < 0.01 && parsedLrc[0].Text == "first line",
    parsedLrc.Count + " " + (parsedLrc.Count > 0 ? parsedLrc[0].Time.ToString() : ""));
Check("lrc multi tag", parsedLrc.Count(l => l.Text == "repeated") == 2, "");

var wb = new System.Windows.Media.Imaging.WriteableBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
var pixels = new byte[8 * 8 * 4];
for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 20; pixels[i + 1] = 30; pixels[i + 2] = 220; pixels[i + 3] = 255; }
wb.WritePixels(new System.Windows.Int32Rect(0, 0, 8, 8), pixels, 8 * 4, 0);
wb.Freeze();
var accentColor = MediaViewModel.ComputeAccentColor(wb);
Check("accent from red artwork", accentColor.R > 180 && accentColor.G < 80 && accentColor.B < 80,
    accentColor.ToString());

Check("sha256 parse", UpdateService.ParseSha256("notes\nsha256: BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD") ==
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "");

Check("sha256 missing", UpdateService.ParseSha256("no hash here") == "", "");

var hashFile = System.IO.Path.Combine(testData, "hash.txt");
System.IO.File.WriteAllText(hashFile, "abc");
Check("sha256 compute", UpdateService.ComputeSha256(hashFile) ==
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", UpdateService.ComputeSha256(hashFile));

var picked = UpdateService.PickAsset(new UpdateInfo
{
    Assets = new[]
    {
        new UpdateAsset { Name = "island.zip", Url = "https://example/1" },
        new UpdateAsset { Name = "island-setup.exe", Url = "https://example/2" }
    }
});
Check("pick exe asset", picked?.Name == "island-setup.exe", picked?.Name ?? "null");

Check("theme resolve light", Theme.ResolveMode(true) == "light", Theme.ResolveMode(true));
Check("theme resolve dark", Theme.ResolveMode(false) == "dark", Theme.ResolveMode(false));

var systemLight = 0;
try
{
    using var personalize = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
    systemLight = personalize?.GetValue("AppsUseLightTheme") is int value ? value : 0;
}
catch
{
}
Check("theme system matches registry", Theme.ResolveSystem() == Theme.ResolveMode(systemLight != 0),
    Theme.ResolveSystem() + " reg=" + systemLight);

// updater hardening
var safePath = UpdateService.ResolveDownloadPath(@"C:\temp\island", "..\\evil.exe");
Check("update path traversal resolved", safePath == @"C:\temp\evil.exe", safePath);

var nestedPath = UpdateService.ResolveDownloadPath(@"C:\temp\island", "..\\..\\Windows\\evil.exe");
Check("update nested traversal resolved", nestedPath == @"C:\temp\Windows\\evil.exe" || nestedPath == @"C:\temp\evil.exe",
    nestedPath);

Check("update empty name rejected", UpdateService.ResolveDownloadPath(@"C:\temp\island", "..") == "", "");

var traversalAssets = new UpdateInfo
{
    Assets = new[]
    {
        new UpdateAsset { Name = "..\\evil.exe", Url = "https://example/1" },
        new UpdateAsset { Name = "C:\\Windows\\evil.exe", Url = "https://example/2" }
    }
};
Check("update suspicious assets rejected", UpdateService.PickAsset(traversalAssets) is null, "");

var zipAsset = UpdateService.PickAsset(new UpdateInfo
{
    Assets = new[] { new UpdateAsset { Name = "island-0.1.0.zip", Url = "https://example/3" } }
});
Check("update zip accepted", zipAsset?.Name == "island-0.1.0.zip", zipAsset?.Name ?? "null");

Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILURES");
return failures == 0 ? 0 : 1;
