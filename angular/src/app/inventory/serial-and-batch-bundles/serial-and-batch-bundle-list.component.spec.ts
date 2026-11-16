import { describe, it, expect } from 'vitest';
import { filterBundlesBySearch } from './serial-and-batch-bundle-list.component';
import type { SerialAndBatchBundleDto } from '../../proxy/inventory/models';

describe('SerialAndBatchBundleListComponent', () => {
  const sampleBundles: SerialAndBatchBundleDto[] = [
    {
      id: 'b-1',
      itemName: 'Industrial Pump X1',
      bundleType: 'Inward',
      voucherType: 'Purchase Receipt',
      totalQty: 5,
      entryCount: 5,
      isCancelled: false,
    },
    {
      id: 'b-2',
      itemName: 'Valve Kit',
      bundleType: 'Outward',
      voucherType: 'Delivery Note',
      totalQty: 2,
      entryCount: 2,
      isCancelled: false,
    },
  ];

  it('filters bundles by item name, voucher type, or bundle type', () => {
    expect(filterBundlesBySearch(sampleBundles, '')).toHaveLength(2);
    expect(filterBundlesBySearch(sampleBundles, 'pump')).toHaveLength(1);
    expect(filterBundlesBySearch(sampleBundles, 'delivery note')[0].id).toBe('b-2');
  });
});
