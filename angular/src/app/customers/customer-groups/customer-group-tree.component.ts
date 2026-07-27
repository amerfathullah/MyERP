import { Component } from '@angular/core';
import { HierarchyTreeComponent } from '../../shared/components/hierarchy-tree/hierarchy-tree.component';

@Component({
  selector: 'app-customer-group-tree',
  standalone: true,
  imports: [HierarchyTreeComponent],
  template: `<app-hierarchy-tree type="CustomerGroup" title="Customer Groups" iconClass="bi bi-people" />`,
})
export class CustomerGroupTreeComponent {}
