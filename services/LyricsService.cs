using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Reverie.Models;

namespace Reverie.Services;

public class LyricsService
{
    private static readonly HttpClient client = new HttpClient();

    public async Task<LyricsResult> GetLyricsAsync(string title, string artist)
    {
        var result = new LyricsResult();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            return result;

        // Strip Apple Music's album suffix from artist name
        if (artist.Contains(" — "))
            artist = artist.Substring(0, artist.IndexOf(" — ")).Trim();
        else if (artist.Contains(" - "))
            artist = artist.Substring(0, artist.IndexOf(" - ")).Trim();

        string encodedTitle = System.Uri.EscapeDataString(title);
        string encodedArtist = System.Uri.EscapeDataString(artist);
        string url = $"https://lrclib.net/api/get?track_name={encodedTitle}&artist_name={encodedArtist}";
        
        try 
        {
            var response = await client.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("syncedLyrics", out var lyricsProp) && lyricsProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var lrcText = lyricsProp.GetString();
                if (!string.IsNullOrWhiteSpace(lrcText))
                {
                    result.Lines = ParseLrc(lrcText);
                    result.IsSuccessful = true;
                }
            }
            else if (doc.RootElement.TryGetProperty("plainLyrics", out var plainProp) && plainProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                // Fallback if no synced lyrics
                result.Lines.Add(new LyricLine { 
                    StartTime = TimeSpan.Zero, 
                    Text = plainProp.GetString() ?? "Lyrics not synced." 
                });
                result.IsSuccessful = true;
            }
            else 
            {
                // API returned but no lyrics found in the response body properties we care about
                result.IsSuccessful = true; // Still technically successful API call but no lyrics
            }
        }
        catch
        {
            result.IsSuccessful = false;
        }

        return result;
    }

    private List<LyricLine> ParseLrc(string lrcText)
    {
        var lines = new List<LyricLine>();
        var regex = new Regex(@"\[(\d+):(\d+\.\d+)\](.*)");
        
        var rawLines = lrcText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in rawLines)
        {
            var match = regex.Match(rawLine);
            if (match.Success)
            {
                int minutes = int.Parse(match.Groups[1].Value);
                double seconds = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var time = TimeSpan.FromSeconds(minutes * 60 + seconds);
                string text = match.Groups[3].Value.Trim();
                
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Check for significant gap before this line
                    if (lines.Count > 0)
                    {
                        var lastLine = lines.Last();
                        var gap = time - lastLine.StartTime;
                        
                        // Estimate duration of the previous line based on word count
                        // Assuming average 0.5s per word + 1s base
                        double estimatedDuration = lastLine.Text.Split(' ').Length * 0.5 + 1.0;
                        if (estimatedDuration > 8) estimatedDuration = 8; // Cap it

                        // If there's a gap of more than 8 seconds AND more than the estimated duration
                        if (gap.TotalSeconds > 10 && gap.TotalSeconds > (estimatedDuration + 4))
                        {
                            lines.Add(new LyricLine 
                            { 
                                StartTime = lastLine.StartTime.Add(TimeSpan.FromSeconds(estimatedDuration)), 
                                Text = "♪ ♪ ♪",
                                IsInstrumental = true
                            });
                        }
                    }
                    else if (time.TotalSeconds > 6)
                    {
                        // If the first line starts after 6 seconds, insert an intro marker
                        lines.Add(new LyricLine 
                        { 
                            StartTime = TimeSpan.FromSeconds(2), 
                            Text = "♪ ♪ ♪",
                            IsInstrumental = true
                        });
                    }

                    lines.Add(new LyricLine { 
                        StartTime = time, 
                        Text = text
                    });
                }
            }
        }
        
        return lines;
    }
}