import { describe, it, expect } from 'vitest';
import { filterLeadOwnerRows } from './lead-owner-efficiency.component';
import type { CampaignEfficiencyRowDto } from '../../../proxy/crm/models';

describe('LeadOwnerEfficiencyComponent', () => {
  const rows: CampaignEfficiencyRowDto[] = [
    {
      campaign: 'Ahmad Zaki',
      leadCount: 15,
      oppCount: 8,
      quotCount: 5,
      orderCount: 3,
      orderValue: 12000,
    },
    {
      campaign: 'Siti Nurhaliza',
      leadCount: 10,
      oppCount: 6,
      quotCount: 4,
      orderCount: 4,
      orderValue: 22000,
    },
  ];

  it('filters lead owner efficiency rows case-insensitively', () => {
    expect(filterLeadOwnerRows(undefined, '')).toEqual([]);
    expect(filterLeadOwnerRows(rows, '')).toHaveLength(2);
    expect(filterLeadOwnerRows(rows, 'ahmad')).toHaveLength(1);
    expect(filterLeadOwnerRows(rows, 'siti')[0].orderValue).toBe(22000);
  });
});
