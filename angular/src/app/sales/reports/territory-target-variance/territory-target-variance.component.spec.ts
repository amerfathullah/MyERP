import { describe, it, expect } from 'vitest';
import {
  clampTerritoryAchievementProgress,
  filterTerritoryTargetVarianceRows,
} from './territory-target-variance.component';
import type { TerritoryTargetVarianceRowDto } from '../../../proxy/sales/models';

describe('TerritoryTargetVarianceComponent', () => {
  const sampleRows: TerritoryTargetVarianceRowDto[] = [
    {
      territoryId: 't-1',
      territoryName: 'Malaysia',
      itemGroupId: 'ig-1',
      itemGroupName: 'Software',
      target: 200000,
      achieved: 180000,
      variance: -20000,
      achievementPercentage: 90,
    },
    {
      territoryId: 't-2',
      territoryName: 'Singapore',
      itemGroupId: null,
      itemGroupName: 'All Item Groups',
      target: 80000,
      achieved: 96000,
      variance: 16000,
      achievementPercentage: 120,
    },
  ];

  it('filters territory rows case-insensitively by territory or item group', () => {
    expect(filterTerritoryTargetVarianceRows(undefined, '')).toEqual([]);
    expect(filterTerritoryTargetVarianceRows(sampleRows, '')).toHaveLength(2);
    expect(filterTerritoryTargetVarianceRows(sampleRows, 'singapore')).toHaveLength(1);
    expect(filterTerritoryTargetVarianceRows(sampleRows, 'software')[0].territoryId).toBe('t-1');
  });

  it('clamps progress percentages to 0..100', () => {
    expect(clampTerritoryAchievementProgress(undefined)).toBe(0);
    expect(clampTerritoryAchievementProgress(-5)).toBe(0);
    expect(clampTerritoryAchievementProgress(90)).toBe(90);
    expect(clampTerritoryAchievementProgress(120)).toBe(100);
  });
});
