using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Island.Controls;

/// <summary>Small markdown renderer for note previews: headings, bold, italic, code, bullets.</summary>
public sealed class MarkdownText : TextBlock
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownText),
        new PropertyMetadata("", (d, _) => ((MarkdownText)d).Render()));

    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(
        nameof(MaxLines), typeof(int), typeof(MarkdownText),
        new PropertyMetadata(6, (d, _) => ((MarkdownText)d).Render()));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    private void Render()
    {
        Inlines.Clear();

        var text = Markdown ?? "";
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Where(line => line.Trim().Length > 0)
            .Take(Math.Max(1, MaxLines))
            .ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var heading = line.StartsWith("# ", StringComparison.Ordinal);
            if (heading) line = line[2..];

            var check = Regex.Match(line, @"^\s*[-*]\s*\[( |x|X)\]\s*(.*)$");
            if (check.Success)
            {
                if (i > 0) Inlines.Add(new LineBreak());
                var done = check.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
                AddRun(done ? "☑ " : "☐ ", heading, false, false, false);
                AddInline(check.Groups[2].Value, heading);
                continue;
            }

            if (Regex.IsMatch(line, @"^\s*[-*]\s+"))
                line = Regex.Replace(line, @"^\s*[-*]\s+", "• ");

            if (i > 0) Inlines.Add(new LineBreak());
            AddInline(line, heading);
        }
    }

    private void AddInline(string text, bool heading)
    {
        var pattern = new Regex(@"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`(.+?)`)");
        var index = 0;

        foreach (Match match in pattern.Matches(text))
        {
            if (match.Index > index) AddRun(text[index..match.Index], heading, false, false, false);

            if (match.Groups[1].Success) AddRun(match.Groups[2].Value, heading, true, false, false);
            else if (match.Groups[3].Success) AddRun(match.Groups[4].Value, heading, false, true, false);
            else if (match.Groups[5].Success) AddRun(match.Groups[6].Value, heading, false, false, true);

            index = match.Index + match.Length;
        }

        if (index < text.Length) AddRun(text[index..], heading, false, false, false);
    }

    private void AddRun(string text, bool heading, bool bold, bool italic, bool code)
    {
        var run = new Run(text)
        {
            FontWeight = bold || heading ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            FontFamily = code ? new FontFamily("Consolas") : FontFamily,
            FontSize = heading ? FontSize + 1.5 : FontSize
        };

        Inlines.Add(run);
    }
}
