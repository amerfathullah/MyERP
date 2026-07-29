import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { ActivatedRoute } from '@angular/router';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PayrollService } from '../../proxy/human-resources/payroll.service';
import { PayrollStore } from '../store/payroll.store';
import type { PayrollEntryDto } from '../../proxy/human-resources/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-payroll-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, PageModule,
    DocumentWorkflowComponent, LoadingOverlayComponent, LocalizationPipe],
  templateUrl: './payroll-detail.component.html',
  styleUrls: ['./payroll-detail.component.scss'],
})
export class PayrollDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PayrollService);
  private store = inject(PayrollStore);

  entry: PayrollEntryDto | null = null;
  lineColumns = ['employeeName', 'grossSalary', 'epfEmployee', 'socsoEmployee', 'eisEmployee', 'mtdAmount', 'totalDeductions', 'netSalary'];

  get workflowActions(): WorkflowAction[] {
    if (!this.entry) return [];
    const actions: WorkflowAction[] = [];
    if (this.entry.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (this.entry.status === 'Submitted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe(r => { this.entry = r; });
  }

  onWorkflowAction(action: string): void {
    const id = this.entry!.id!;
    if (action === 'submit') {
      this.store.submitEntry(id);
    } else if (action === 'cancel') {
      this.store.cancelEntry(id);
    }
    this.service.get(id).subscribe({
      next: (r) => { this.entry = r; },
      error: () => {}
    });
  }
}
