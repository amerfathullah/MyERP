import { describe, it, expect } from 'vitest';
import { filterCampaignRows } from './campaign-efficiency.component';
import type { CampaignEfficiencyRowDto } from '../../../proxy/crm/models';

describe('CampaignEfficiencyComponent', () => {
  const sampleRows: CampaignEfficiencyRowDto[] = [
    {
      campaign: 'Raya Promo 2026',
      leadCount: 25,
      oppCount: 10,
      quotCount: 6,
      orderCount: 4,
      orderValue: 18500,
      oppLeadRate: 40,
      quotLeadRate: 24,
      orderQuotRate: 66.67,
    },
    {
      campaign: 'LinkedIn Ads',
      leadCount: 12,
      oppCount: 3,
      quotCount: 2,
      orderCount: 1,
      orderValue: 4200,
      oppLeadRate: 25,
      quotLeadRate: 16.67,
      orderQuotRate: 50,
    },
  ];

  it('returns empty array when rows are undefined or empty', () => {
    expect(filterCampaignRows(undefined, '')).toEqual([]);
    expect(filterCampaignRows([], 'raya')).toEqual([]);
  });

  it('returns all rows when searchTerm is blank', () => {
    expect(filterCampaignRows(sampleRows, '   ')).toHaveLength(2);
  });

  it('filters campaign rows case-insensitively by campaign name', () => {
    const filtered = filterCampaignRows(sampleRows, 'raya');
    expect(filtered).toHaveLength(1);
    expect(filtered[0].campaign).toBe('Raya Promo 2026');
    expect(filtered[0].orderValue).toBe(18500);
  });
});
