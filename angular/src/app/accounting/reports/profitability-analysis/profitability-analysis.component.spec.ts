import { describe, it, expect } from 'vitest';
import { calculateGrossMarginPercent } from './profitability-analysis.component';

describe('ProfitabilityAnalysisComponent', () => {
  it('computes gross margin percent safely', () => {
    expect(calculateGrossMarginPercent(0, 100)).toBe(0);
    expect(calculateGrossMarginPercent(2000, 500)).toBe(25);
    expect(calculateGrossMarginPercent(1000, -150)).toBe(-15);
  });
});
