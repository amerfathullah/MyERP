import { describe, it, expect } from 'vitest';
import {
  calculateFixedAssetTotals,
  filterFixedAssetRows,
} from './fixed-asset-register.component';
import type { FixedAssetRegisterRowDto } from '../../../proxy/assets/models';

describe('FixedAssetRegisterComponent', () => {
  const sampleRows: FixedAssetRegisterRowDto[] = [
    {
      assetNumber: 'AST-0001',
      assetName: 'CNC Lathe',
      assetCategoryName: 'Machinery',
      costCenterName: 'Production',
      location: 'Plant A',
      status: 'Submitted',
      grossPurchaseAmount: 50000,
      openingAccumulatedDepreciation: 5000,
      depreciatedAmount: 2500,
      revaluationAmount: 1000,
      assetValue: 43500,
    },
    {
      assetNumber: 'AST-0002',
      assetName: 'Office Laptop',
      assetCategoryName: 'IT Equipment',
      costCenterName: 'HQ',
      location: 'Floor 3',
      status: 'Submitted',
      grossPurchaseAmount: 6000,
      openingAccumulatedDepreciation: 1000,
      depreciatedAmount: 500,
      revaluationAmount: 0,
      assetValue: 4500,
    },
  ];

  it('filters rows across asset number, category, and location', () => {
    expect(filterFixedAssetRows(undefined, '')).toEqual([]);
    expect(filterFixedAssetRows(sampleRows, '')).toHaveLength(2);
    expect(filterFixedAssetRows(sampleRows, 'plant a')).toHaveLength(1);
    expect(filterFixedAssetRows(sampleRows, 'AST-0002')[0].assetName).toBe('Office Laptop');
  });

  it('calculates column totals accurately', () => {
    const totals = calculateFixedAssetTotals(sampleRows);
    expect(totals.grossPurchaseAmount).toBe(56000);
    expect(totals.openingAccumulatedDepreciation).toBe(6000);
    expect(totals.depreciatedAmount).toBe(3000);
    expect(totals.revaluationAmount).toBe(1000);
    expect(totals.assetValue).toBe(48000);
  });
});
