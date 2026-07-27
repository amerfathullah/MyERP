import { Component } from '@angular/core';
import { HierarchyTreeComponent } from '../../shared/components/hierarchy-tree/hierarchy-tree.component';

@Component({
  selector: 'app-supplier-group-tree',
  standalone: true,
  imports: [HierarchyTreeComponent],
  template: `<app-hierarchy-tree type="SupplierGroup" title="Supplier Groups" iconClass="bi bi-truck" />`,
})
export class SupplierGroupTreeComponent {}
