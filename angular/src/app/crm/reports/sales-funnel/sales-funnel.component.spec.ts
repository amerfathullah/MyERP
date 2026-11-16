import { describe, it, expect } from 'vitest';
import { buildFunnelMetrics } from './sales-funnel.component';
import type { SalesFunnelStageItemDto } from '../../../proxy/crm/models';

describe('SalesFunnelComponent', () => {
  it('calculates bar widths and step-over-step conversion percentages', () => {
    const stages: SalesFunnelStageItemDto[] = [
      { title: 'Active Leads', value: 100, color: '#B03B46' },
      { title: 'Opportunities', value: 40, color: '#F09C00' },
      { title: 'Quotations', value: 20, color: '#0d6efd' },
      { title: 'Converted', value: 5, color: '#198754' },
    ];

    const metrics = buildFunnelMetrics(stages);
    expect(metrics).toHaveLength(4);
    expect(metrics[0].widthPercent).toBe(100);
    expect(metrics[0].stepConversionPercent).toBeNull();
    expect(metrics[1].widthPercent).toBe(40);
    expect(metrics[1].stepConversionPercent).toBe(40);
    expect(metrics[2].stepConversionPercent).toBe(50);
    expect(metrics[3].stepConversionPercent).toBe(25);
  });
});
