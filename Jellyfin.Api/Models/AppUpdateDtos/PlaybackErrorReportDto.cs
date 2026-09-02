using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// Playback error report DTO.
/// </summary>
public class PlaybackErrorReportDto
{
    /// <summary>
    /// Gets or sets the app version.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the app version code.
    /// </summary>
    public int AppVersionCode { get; set; }

    /// <summary>
    /// Gets or sets the device manufacturer.
    /// </summary>
    public string DeviceManufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device model.
    /// </summary>
    public string DeviceModel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Android version.
    /// </summary>
    public string AndroidVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error type.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stack trace.
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the play method.
    /// </summary>
    public string PlayMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the container.
    /// </summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the video codec.
    /// </summary>
    public string VideoCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the video resolution.
    /// </summary>
    public string VideoResolution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audio codec.
    /// </summary>
    public string AudioCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audio channels.
    /// </summary>
    public int AudioChannels { get; set; }

    /// <summary>
    /// Gets or sets the media URL.
    /// </summary>
    public string MediaUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the current position in milliseconds.
    /// </summary>
    public long CurrentPositionMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
