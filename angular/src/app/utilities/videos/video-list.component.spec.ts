import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { VideoListComponent } from './video-list.component';
import { VideoService } from '../../proxy/utilities/video.service';

describe('VideoListComponent Update Stats', () => {
  it('updates video metrics via VideoService.updateStats and updates item list', () => {
    const videoMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'vid-1', title: 'Intro', viewCount: 10, likeCount: 2 }] })),
      updateStats: vi.fn().mockReturnValue(of({ id: 'vid-1', title: 'Intro', viewCount: 250, likeCount: 30 })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: VideoService, useValue: videoMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new VideoListComponent());
    comp.ngOnInit();
    comp.openStatsEditor(comp.items[0]);
    comp.statsForm.viewCount = 250;
    comp.statsForm.likeCount = 30;
    comp.saveStats();

    expect(videoMock.updateStats).toHaveBeenCalledWith('vid-1', { viewCount: 250, likeCount: 30, dislikeCount: 0, commentCount: 0 });
    expect(comp.items[0].viewCount).toBe(250);
    expect(comp.editingStatsVideo).toBeNull();
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
  });
});
