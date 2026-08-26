using System;
using MyERP.Utilities.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Utilities;

public class VideoDomainTests
{
    [Fact]
    public void Should_Create_Valid_Video_And_Update_Stats()
    {
        var id = Guid.NewGuid();
        var video = new Video(
            id,
            "Introduction to MyERP Inventory",
            VideoProvider.YouTube,
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "dQw4w9WgXcQ",
            DateTime.UtcNow,
            360,
            "A comprehensive overview of stock operations",
            "https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg",
            true);

        video.Id.ShouldBe(id);
        video.Title.ShouldBe("Introduction to MyERP Inventory");
        video.Provider.ShouldBe(VideoProvider.YouTube);
        video.YoutubeVideoId.ShouldBe("dQw4w9WgXcQ");
        video.DurationSeconds.ShouldBe(360);
        video.IsActive.ShouldBeTrue();

        video.UpdateStats(1500, 120, 2, 35);
        video.ViewCount.ShouldBe(1500);
        video.LikeCount.ShouldBe(120);
        video.DislikeCount.ShouldBe(2);
        video.CommentCount.ShouldBe(35);
    }

    [Fact]
    public void Should_Create_Valid_VideoSettings()
    {
        var id = Guid.NewGuid();
        var settings = new VideoSettings(
            id,
            true,
            "AIzaSyDemoKey12345",
            30);

        settings.Id.ShouldBe(id);
        settings.EnableYoutubeTracking.ShouldBeTrue();
        settings.ApiKey.ShouldBe("AIzaSyDemoKey12345");
        settings.FrequencyMinutes.ShouldBe(30);
    }
}
