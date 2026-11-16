import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CampaignListComponent } from './campaign-list.component';
import { CampaignService } from '../../proxy/crm/campaign.service';

describe('CampaignListComponent UTM Slug', () => {
  it('loads UTM campaign identifier via CampaignService.getUtmCampaign', () => {
    const campaignMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cmp-1', campaignName: 'Q4 Launch' }] })),
      getUtmCampaign: vi.fn().mockReturnValue(of('q4_launch')),
    };

    const injector = Injector.create({
      providers: [
        { provide: CampaignService, useValue: campaignMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CampaignListComponent());
    comp.ngOnInit();
    comp.loadUtmSlug('cmp-1');

    expect(campaignMock.getUtmCampaign).toHaveBeenCalledWith('cmp-1');
    expect(comp.utmSlugs()['cmp-1']).toBe('q4_launch');
  });
});
