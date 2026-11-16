import { describe, it, expect } from 'vitest';
import {
  calculateDepreciationBalanceTotals,
  filterDepreciationBalanceRows,
} from './asset-depreciations-and-balances.component';
import type { AssetDepreciationsAndBalancesRowDto } from '../../../proxy/assets/models';

describe('AssetDepreciationsAndBalancesComponent', () => {
  const sampleRows: AssetDepreciationsAndBalancesRowDto[] = [
    {
      assetCategoryName: 'Machinery',
      assetNumber: 'AST-001',
      assetName: 'Milling Machine',
      valueAsOnFromDate: 100000,
      valueOfNewPurchase: 20000,
      valueOfSoldAsset: 5000,
      valueOfScrappedAsset: 0,
      valueOfCapitalizedAsset: 0,
      adjustmentDuringPeriod: 1500,
      valueAsOnToDate: 116500,
      accumulatedDepreciationAsOnFromDate: 10000,
      depreciationAmountDuringThePeriod: 4000,
      accumulatedDepreciationAsOnToDate: 14000,
      netAssetValueAsOnFromDate: 90000,
      netAssetValueAsOnToDate: 102500,
    },
    {
      assetCategoryName: 'Vehicles',
      assetNumber: 'AST-002',
      assetName: 'Delivery Van',
      valueAsOnFromDate: 60000,
      valueOfNewPurchase: 0,
      valueOfSoldAsset: 0,
      valueOfScrappedAsset: 2000,
      valueOfCapitalizedAsset: 0,
      adjustmentDuringPeriod: 0,
      valueAsOnToDate: 58000,
      accumulatedDepreciationAsOnFromDate: 8000,
      depreciationAmountDuringThePeriod: 3000,
      accumulatedDepreciationAsOnToDate: 11000,
      netAssetValueAsOnFromDate: 52000,
      netAssetValueAsOnToDate: 47000,
    },
  ];

  it('filters rows by category, asset number, or asset name', () => {
    expect(filterDepreciationBalanceRows(undefined, '')).toEqual([]);
    expect(filterDepreciationBalanceRows(sampleRows, '')).toHaveLength(2);
    expect(filterDepreciationBalanceRows(sampleRows, 'vehicles')).toHaveLength(1);
    expect(filterDepreciationBalanceRows(sampleRows, 'milling')[0].assetCategoryName).toBe('Machinery');
  });

  it('calculates totals across all balance columns', () => {
    const totals = calculateDepreciationBalanceTotals(sampleRows);
    expect(totals.valueAsOnFromDate).toBe(160000);
    expect(totals.valueOfNewPurchase).toBe(20000);
    expect(totals.valueOfSoldAsset).toBe(5000);
    expect(totals.valueOfScrappedAsset).toBe(2000);
    expect(totals.adjustmentDuringPeriod).toBe(1500);
    expect(totals.valueAsOnToDate).toBe(174500);
    expect(totals.accumulatedDepreciationAsOnFromDate).toBe(18000);
    expect(totals.depreciationAmountDuringThePeriod).toBe(7000);
    expect(totals.accumulatedDepreciationAsOnToDate).toBe(25000);
    expect(totals.netAssetValueAsOnToDate).toBe(149500);
  });
});
