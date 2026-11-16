import { describe, it, expect } from 'vitest';
import {
  filterLostOpportunityRows,
  sumLostOpportunityAmount,
} from './lost-opportunity.component';
import type { LostOpportunityRowDto } from '../../../proxy/crm/models';

describe('LostOpportunityComponent', () => {
  const sampleRows: LostOpportunityRowDto[] = [
    {
      id: 'opp-1',
      opportunityNumber: 'OPP-001',
      title: 'Enterprise Rollout',
      customerName: 'Omega Corp',
      lostReasons: 'Price too high',
      territory: 'Malaysia',
      salesStage: 'Proposal',
      opportunityAmount: 45000,
    },
    {
      id: 'opp-2',
      opportunityNumber: 'OPP-002',
      title: 'Cloud Migration',
      customerName: 'Beta Sdn Bhd',
      lostReasons: 'Competitor selected',
      territory: 'Singapore',
      salesStage: 'Negotiation',
      opportunityAmount: 25000,
    },
  ];

  it('filters lost opportunities across number, customer, reason, and territory', () => {
    expect(filterLostOpportunityRows(undefined, '')).toEqual([]);
    expect(filterLostOpportunityRows(sampleRows, '')).toHaveLength(2);
    expect(filterLostOpportunityRows(sampleRows, 'price')).toHaveLength(1);
    expect(filterLostOpportunityRows(sampleRows, 'singapore')[0].opportunityNumber).toBe('OPP-002');
  });

  it('sums lost opportunity amounts accurately', () => {
    expect(sumLostOpportunityAmount([])).toBe(0);
    expect(sumLostOpportunityAmount(sampleRows)).toBe(70000);
  });
});
