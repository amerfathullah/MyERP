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

    [Theory]
    [InlineData("https://vimeo.com/76979871")]
    [InlineData("https://drive.google.com/file/d/abc123/view")]
    public void NonYoutube_ValidWebUrl_Succeeds(string url)
    {
        var video = new Video(Guid.NewGuid(), "External Video", VideoProvider.Vimeo, url);
        video.Url.ShouldBe(url);
    }

    [Theory]
    [InlineData("V01X\" style=\"animation:spinner-border 1s\" onanimationstart=\"alert(1)")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://x\" onmouseover=\"alert(1)")]
    [InlineData("ftp://example.com/video.mp4")]
    public void NonYoutube_UnsafeOrNonWebUrl_Throws(string badUrl)
    {
        var ex = Should.Throw<BusinessException>(() =>
            new Video(Guid.NewGuid(), "Bad Video", VideoProvider.Vimeo, badUrl));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Youtube_InvalidUrlWithoutVideoId_Throws()
    {
        var ex = Should.Throw<BusinessException>(() =>
            new Video(Guid.NewGuid(), "Missing ID", VideoProvider.YouTube, "https://www.youtube.com/"));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Youtube_AlwaysOverwritesTamperedVideoIdFromUrl()
    {
        var video = new Video(
            Guid.NewGuid(),
            "Edited ID",
            VideoProvider.YouTube,
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            youtubeVideoId: "abc\" x=\"1");

        video.YoutubeVideoId.ShouldBe("dQw4w9WgXcQ");

        video.UpdateDetails(
            "Edited ID",
            VideoProvider.YouTube,
            "https://youtu.be/9bZkp7q19f0",
            youtubeVideoId: "tampered_bad");
        video.YoutubeVideoId.ShouldBe("9bZkp7q19f0");
    }
}
