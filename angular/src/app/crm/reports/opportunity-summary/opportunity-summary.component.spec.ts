import { describe, it, expect } from 'vitest';
import {
  filterOpportunitySummaryRows,
  sumSummaryStageValues,
} from './opportunity-summary.component';
import type { OpportunitySummaryRowDto } from '../../../proxy/crm/models';

describe('OpportunitySummaryComponent', () => {
  const sampleRows: OpportunitySummaryRowDto[] = [
    {
      key: 'Sales',
      stageValues: { Qualification: 4, Proposal: 2, Closed: 1 },
    },
    {
      key: 'Support',
      stageValues: { Qualification: 1, Proposal: 0, Closed: 2 },
    },
  ];

  it('filters summary rows by grouping key', () => {
    expect(filterOpportunitySummaryRows(undefined, '')).toEqual([]);
    expect(filterOpportunitySummaryRows(sampleRows, '')).toHaveLength(2);
    expect(filterOpportunitySummaryRows(sampleRows, 'sales')).toHaveLength(1);
  });

  it('sums values across stage keys accurately', () => {
    expect(sumSummaryStageValues(sampleRows[0], ['Qualification', 'Proposal', 'Closed'])).toBe(7);
    expect(sumSummaryStageValues(sampleRows[1], ['Qualification', 'MissingStage'])).toBe(1);
  });
});
