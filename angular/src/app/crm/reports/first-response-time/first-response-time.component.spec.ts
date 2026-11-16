import { describe, it, expect } from 'vitest';
import { computeOverallAverageResponseSeconds } from './first-response-time.component';
import type { FirstResponseTimeRowDto } from '../../../proxy/crm/models';

describe('FirstResponseTimeComponent', () => {
  it('computes average response seconds safely', () => {
    expect(computeOverallAverageResponseSeconds(undefined)).toBe(0);
    expect(computeOverallAverageResponseSeconds([])).toBe(0);

    const rows: FirstResponseTimeRowDto[] = [
      { creationDate: '2026-11-01', avgResponseTimeSeconds: 120, formattedResponseTime: '2m' },
      { creationDate: '2026-11-02', avgResponseTimeSeconds: 240, formattedResponseTime: '4m' },
    ];
    expect(computeOverallAverageResponseSeconds(rows)).toBe(180);
  });
});
