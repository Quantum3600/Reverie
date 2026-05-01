using System;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Reverie.Services;

public class MediaService
{
    public async Task<(string Title, string Artist, Windows.Storage.Streams.IRandomAccessStreamReference? Thumbnail)> GetCurrentTrackAsync()
    {
        var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var currentSession = sessionManager.GetCurrentSession();

        if (currentSession == null) return ("No Track", "No Artist", null);

        var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
        return (mediaProperties.Title, mediaProperties.Artist, mediaProperties.Thumbnail);
    }

    public async Task<TimeSpan?> GetPlaybackPositionAsync()
    {
        var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var currentSession = sessionManager.GetCurrentSession();

        if (currentSession == null) return null;

        var timeline = currentSession.GetTimelineProperties();
        if (timeline == null) return null;

        // Calculate actual position by adding the time elapsed since the last update (only if playing)
        var playbackInfo = currentSession.GetPlaybackInfo();
        if (playbackInfo != null && playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            return timeline.Position + (DateTime.UtcNow - timeline.LastUpdatedTime.UtcDateTime);
        }
        
        return timeline.Position;
    }
}