import { Component, ChangeDetectionStrategy } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { HierarchyTreeComponent } from '../../shared/components/hierarchy-tree/hierarchy-tree.component';

@Component({
  selector: 'app-supplier-group-tree',
  standalone: true,
  imports: [HierarchyTreeComponent, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-hierarchy-tree type="SupplierGroup" [title]="'::SupplierGroups' | abpLocalization" iconClass="bi bi-truck" />`,
})
export class SupplierGroupTreeComponent {}
