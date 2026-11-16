import { describe, it, expect } from 'vitest';
import { filterItemManufacturers } from './item-manufacturer-list.component';
import type { ItemManufacturerDto } from '../../proxy/inventory/models';

describe('ItemManufacturerListComponent', () => {
  const rows: ItemManufacturerDto[] = [
    {
      id: 'im-1',
      companyId: 'comp-1',
      itemCode: 'CPU-01',
      itemName: 'Core Processor',
      manufacturerShortName: 'INTEL',
      manufacturerPartNo: 'BX8071514700K',
      isDefault: true,
    },
    {
      id: 'im-2',
      companyId: 'comp-2',
      itemCode: 'MEM-16',
      itemName: 'DDR5 16GB',
      manufacturerShortName: 'SAMSUNG',
      manufacturerPartNo: 'M323R2GA3BB0',
      isDefault: false,
    },
  ];

  it('scopes by company and filters by part number, item, or manufacturer', () => {
    expect(filterItemManufacturers(rows, '', 'comp-1')).toHaveLength(1);
    expect(filterItemManufacturers(rows, 'samsung', '')).toHaveLength(1);
    expect(filterItemManufacturers(rows, 'BX807', 'comp-1')[0].id).toBe('im-1');
  });
});
