using System;

namespace Reverie.Models;

public class LyricLine
{
    public TimeSpan StartTime { get; set; }
    public string Text { get; set; } = "";
}

public class LyricsResult
{
    public System.Collections.Generic.List<LyricLine> Lines { get; set; } = new();
    public bool IsSuccessful { get; set; }
}
