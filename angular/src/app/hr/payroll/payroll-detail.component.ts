import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { ActivatedRoute } from '@angular/router';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { PayrollService } from '../../proxy/hr/payroll.service';
import { PayrollStore } from '../store/payroll.store';
import type { PayrollEntryDto } from '../../proxy/hr/models';

@Component({
  selector: 'app-payroll-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatDividerModule,
  ],
  templateUrl: './payroll-detail.component.html',
  styleUrls: ['./payroll-detail.component.scss'],
})
export class PayrollDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PayrollService);
  private store = inject(PayrollStore);

  entry: PayrollEntryDto | null = null;
  lineColumns = ['employeeName', 'grossSalary', 'epfEmployee', 'socsoEmployee', 'eisEmployee', 'pcb', 'totalDeductions', 'netSalary'];

  get workflowActions(): WorkflowAction[] {
    if (!this.entry) return [];
    const actions: WorkflowAction[] = [];
    if (this.entry.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.entry.status === 'Submitted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
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
    setTimeout(() => {
      this.service.get(id).subscribe(r => { this.entry = r; });
    }, 500);
  }
}
