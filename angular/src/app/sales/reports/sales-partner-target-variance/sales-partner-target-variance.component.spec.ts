import { describe, it, expect } from 'vitest';
import {
  clampAchievementProgress,
  filterSalesPartnerTargetVarianceRows,
} from './sales-partner-target-variance.component';
import type { SalesPartnerTargetVarianceRowDto } from '../../../proxy/sales/models';

describe('SalesPartnerTargetVarianceComponent', () => {
  const sampleRows: SalesPartnerTargetVarianceRowDto[] = [
    {
      salesPartnerId: 'sp-1',
      salesPartnerName: 'Channel Partner KL',
      itemGroupId: 'ig-1',
      itemGroupName: 'Hardware',
      target: 100000,
      achieved: 120000,
      variance: 20000,
      achievementPercentage: 120,
    },
    {
      salesPartnerId: 'sp-2',
      salesPartnerName: 'Penang Reseller',
      itemGroupId: null,
      itemGroupName: 'All Item Groups',
      target: 50000,
      achieved: 25000,
      variance: -25000,
      achievementPercentage: 50,
    },
  ];

  it('filters sales partner rows case-insensitively by partner or item group', () => {
    expect(filterSalesPartnerTargetVarianceRows(undefined, '')).toEqual([]);
    expect(filterSalesPartnerTargetVarianceRows(sampleRows, '')).toHaveLength(2);
    expect(filterSalesPartnerTargetVarianceRows(sampleRows, 'penang')).toHaveLength(1);
    expect(filterSalesPartnerTargetVarianceRows(sampleRows, 'hardware')[0].salesPartnerId).toBe('sp-1');
  });

  it('clamps progress percentages to 0..100', () => {
    expect(clampAchievementProgress(undefined)).toBe(0);
    expect(clampAchievementProgress(-10)).toBe(0);
    expect(clampAchievementProgress(72.5)).toBe(72.5);
    expect(clampAchievementProgress(145)).toBe(100);
  });
});
