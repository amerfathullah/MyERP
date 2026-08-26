using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Utilities;

public abstract class VideoAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IVideoAppService _videoAppService;
    private readonly IVideoSettingsAppService _settingsAppService;

    protected VideoAppServiceTests()
    {
        _videoAppService = GetRequiredService<IVideoAppService>();
        _settingsAppService = GetRequiredService<IVideoSettingsAppService>();
    }

    [Fact]
    public async Task Video_Services_Should_Perform_CRUD_And_Stats_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Create Video
            var video = await _videoAppService.CreateAsync(new CreateUpdateVideoDto
            {
                Title = "Warehouse Management Tutorial",
                Provider = VideoProvider.YouTube,
                Url = "https://www.youtube.com/watch?v=sample123",
                YoutubeVideoId = "sample123",
                DurationSeconds = 420,
                IsActive = true
            });
            video.Id.ShouldNotBe(Guid.Empty);
            video.Title.ShouldBe("Warehouse Management Tutorial");

            // Update Stats
            var updatedVideo = await _videoAppService.UpdateStatsAsync(video.Id, new UpdateVideoStatsDto
            {
                ViewCount = 2500,
                LikeCount = 180,
                DislikeCount = 1,
                CommentCount = 45
            });
            updatedVideo.ViewCount.ShouldBe(2500);
            updatedVideo.LikeCount.ShouldBe(180);

            // Update VideoSettings
            var settings = await _settingsAppService.UpdateAsync(new UpdateVideoSettingsDto
            {
                EnableYoutubeTracking = true,
                ApiKey = "SecretApiKeyVal",
                FrequencyMinutes = 60
            });
            settings.EnableYoutubeTracking.ShouldBeTrue();
            settings.ApiKey.ShouldBe("SecretApiKeyVal");
        });
    }
}
