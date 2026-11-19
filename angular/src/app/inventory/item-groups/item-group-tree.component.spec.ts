import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ItemGroupTreeComponent } from './item-group-tree.component';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

describe('ItemGroupTreeComponent', () => {
  function createComponent() {
    const items = [
      { id: 'ig-root', name: 'All Items', isGroup: true, parentId: null, defaultWarehouseId: 'wh-1', defaultInventoryAccountId: 'acc-1', defaultSupplierId: 'sup-1' },
      { id: 'ig-child', name: 'Electronics', isGroup: true, parentId: 'ig-root', defaultWarehouseId: 'wh-2', defaultInventoryAccountId: 'acc-2', defaultSupplierId: null },
      { id: 'ig-grandchild', name: 'Phones', isGroup: true, parentId: 'ig-child', defaultWarehouseId: null, defaultInventoryAccountId: null, defaultSupplierId: null },
      { id: 'ig-leaf', name: 'Chargers', isGroup: false, parentId: 'ig-child', defaultWarehouseId: null, defaultInventoryAccountId: null, defaultSupplierId: null },
      { id: 'ig-other', name: 'Services', isGroup: true, parentId: null, defaultWarehouseId: null, defaultInventoryAccountId: null, defaultSupplierId: null },
    ];
    const itemGroupMock = {
      getList: vi.fn().mockReturnValue(of({ items })),
      create: vi.fn().mockReturnValue(of({ id: 'ig-new' })),
      update: vi.fn().mockReturnValue(of({ id: 'ig-child' })),
    };
    const supplierMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sup-1', name: 'Main Supplier' }] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ItemGroupService, useValue: itemGroupMock },
        { provide: SupplierService, useValue: supplierMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ItemGroupTreeComponent());
    comp.ngOnInit();
    return { comp, itemGroupMock, toasterMock };
  }

  it('excludes self and descendants from availableParentOptions when editing a group', () => {
    const { comp } = createComponent();

    // New group mode: all group nodes (not leaves) are available
    expect(comp.availableParentOptions().map(g => g.id)).toEqual(['ig-root', 'ig-child', 'ig-grandchild', 'ig-other']);

    // Editing ig-child: excludes ig-child and descendant ig-grandchild
    const childNode = comp.tree()[0].children[0];
    comp.editGroup(childNode);
    expect(comp.availableParentOptions().map(g => g.id)).toEqual(['ig-root', 'ig-other']);
  });

  it('calls update instead of create when saving an edited group and preserves default warehouse/account', () => {
    const { comp, itemGroupMock, toasterMock } = createComponent();
    const childNode = comp.tree()[0].children[0];
    comp.editGroup(childNode);
    comp.newGroup.name = 'Consumer Electronics';

    comp.saveGroup();

    expect(itemGroupMock.create).not.toHaveBeenCalled();
    expect(itemGroupMock.update).toHaveBeenCalledWith('ig-child', {
      name: 'Consumer Electronics',
      parentId: 'ig-root',
      isGroup: true,
      defaultWarehouseId: 'wh-2',
      defaultInventoryAccountId: 'acc-2',
      defaultSupplierId: undefined,
    });
    expect(toasterMock.success).toHaveBeenCalled();
  });

  it('calls create when saving a new group', () => {
    const { comp, itemGroupMock } = createComponent();
    comp.addChildTo(comp.tree()[0]);
    comp.newGroup.name = 'Accessories';

    comp.saveGroup();

    expect(itemGroupMock.update).not.toHaveBeenCalled();
    expect(itemGroupMock.create).toHaveBeenCalledWith({
      name: 'Accessories',
      parentId: 'ig-root',
      isGroup: false,
      defaultWarehouseId: undefined,
      defaultInventoryAccountId: undefined,
      defaultSupplierId: undefined,
    });
  });
});
