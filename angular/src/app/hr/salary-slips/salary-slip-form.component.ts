import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SalarySlipService } from '../../proxy/human-resources/salary-slip.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { SalaryComponentService } from '../../proxy/human-resources/salary-component.service';
import type { CreateSalarySlipDto, SalarySlipComponentInputDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-salary-slip-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  templateUrl: './salary-slip-form.component.html',
})
export class SalarySlipFormComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private salarySlipService = inject(SalarySlipService);
  private employeeService = inject(EmployeeService);
  private salaryComponentService = inject(SalaryComponentService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  isEditMode = false;
  entityId: string | null = null;

  employees = signal<{ id: string; name: string }[]>([]);
  earningComponents = signal<{ id: string; name: string; isStatutory: boolean }[]>([]);
  deductionComponents = signal<{ id: string; name: string; isStatutory: boolean }[]>([]);

  companyId = signal('');
  employeeId = signal('');
  postingDate = signal(new Date().toISOString().split('T')[0]);
  startDate = signal(new Date().toISOString().split('T')[0]);
  endDate = signal(new Date().toISOString().split('T')[0]);
  totalWorkingDays = signal(30);
  paymentDays = signal(30);
  leavesWithoutPay = signal(0);

  earnings = signal<SalarySlipComponentInputDto[]>([]);
  deductions = signal<SalarySlipComponentInputDto[]>([]);

  totalEarnings = computed(() => this.earnings().reduce((s, e) => s + (e.amount ?? 0), 0));
  totalDeductions = computed(() => this.deductions().reduce((s, d) => s + (d.amount ?? 0), 0));
  netPay = computed(() => this.totalEarnings() - this.totalDeductions());

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    const cid = this.companyContext.currentCompanyId();
    if (cid) this.companyId.set(cid);

    this.employeeService.getList({ companyId: this.companyId() || undefined, skipCount: 0, maxResultCount: 500 } as any)
      .subscribe({
        next: res => this.employees.set((res.items ?? []).map((e: any) => ({ id: e.id, name: e.fullName ?? e.name ?? e.id }))),
        error: () => {},
      });

    this.salaryComponentService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.earningComponents.set(items.filter((c: any) => c.componentType === 0).map((c: any) => ({ id: c.id, name: c.name, isStatutory: !!c.isStatutory })));
        this.deductionComponents.set(items.filter((c: any) => c.componentType === 1).map((c: any) => ({ id: c.id, name: c.name, isStatutory: !!c.isStatutory })));
      },
      error: () => {},
    });

    if (this.isEditMode) {
      this.salarySlipService.get(this.entityId!).subscribe(s => {
        this.companyId.set(s.companyId ?? '');
        this.employeeId.set(s.employeeId ?? '');
        this.postingDate.set((s.postingDate ?? '').split('T')[0]);
        this.startDate.set((s.startDate ?? '').split('T')[0]);
        this.endDate.set((s.endDate ?? '').split('T')[0]);
        this.totalWorkingDays.set((s as any).totalWorkingDays ?? 30);
        this.paymentDays.set((s as any).paymentDays ?? 30);
        this.leavesWithoutPay.set((s as any).leavesWithoutPay ?? 0);
        this.earnings.set((s.earnings ?? []).map(e => ({
          salaryComponentId: e.salaryComponentId, componentName: e.componentName, amount: e.amount, isStatutory: e.isStatutory,
        })));
        this.deductions.set((s.deductions ?? []).map(d => ({
          salaryComponentId: d.salaryComponentId, componentName: d.componentName, amount: d.amount, isStatutory: d.isStatutory,
        })));
      });
    }
  }

  addEarning(): void {
    this.earnings.set([...this.earnings(), { salaryComponentId: '', componentName: '', amount: 0, isStatutory: false }]);
  }

  addDeduction(): void {
    this.deductions.set([...this.deductions(), { salaryComponentId: '', componentName: '', amount: 0, isStatutory: false }]);
  }

  removeEarning(index: number): void {
    const rows = [...this.earnings()];
    rows.splice(index, 1);
    this.earnings.set(rows);
  }

  removeDeduction(index: number): void {
    const rows = [...this.deductions()];
    rows.splice(index, 1);
    this.deductions.set(rows);
  }

  onEarningComponentChange(index: number, componentId: string): void {
    const component = this.earningComponents().find(c => c.id === componentId);
    const rows = [...this.earnings()];
    rows[index] = { ...rows[index], salaryComponentId: componentId, componentName: component?.name ?? '', isStatutory: component?.isStatutory ?? false };
    this.earnings.set(rows);
  }

  onDeductionComponentChange(index: number, componentId: string): void {
    const component = this.deductionComponents().find(c => c.id === componentId);
    const rows = [...this.deductions()];
    rows[index] = { ...rows[index], salaryComponentId: componentId, componentName: component?.name ?? '', isStatutory: component?.isStatutory ?? false };
    this.deductions.set(rows);
  }

  updateEarningAmount(index: number, amount: number): void {
    const rows = [...this.earnings()];
    rows[index] = { ...rows[index], amount };
    this.earnings.set(rows);
  }

  updateDeductionAmount(index: number, amount: number): void {
    const rows = [...this.deductions()];
    rows[index] = { ...rows[index], amount };
    this.deductions.set(rows);
  }

  cancel(): void {
    if (this.isEditMode) this.router.navigate(['/hr/salary-slips', this.entityId]);
    else this.router.navigate(['/hr/salary-slips']);
  }

  save(): void {
    if (!this.companyId() || !this.employeeId()) {
      this.toaster.error('::PleaseSelectEmployee');
      return;
    }

    const dto: CreateSalarySlipDto = {
      companyId: this.companyId(),
      employeeId: this.employeeId(),
      postingDate: this.postingDate(),
      startDate: this.startDate(),
      endDate: this.endDate(),
      totalWorkingDays: this.totalWorkingDays(),
      paymentDays: this.paymentDays(),
      leavesWithoutPay: this.leavesWithoutPay(),
      earnings: this.earnings().filter(e => e.salaryComponentId),
      deductions: this.deductions().filter(d => d.salaryComponentId),
    };

    if (this.isEditMode) {
      this.salarySlipService.update(this.entityId!, dto).subscribe({
        next: (s) => { this.toaster.success('::SuccessfullyUpdated'); this.router.navigate(['/hr/salary-slips', s.id]); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::FailedToCreate'),
      });
    } else {
      this.salarySlipService.create(dto).subscribe({
        next: (s) => { this.toaster.success('::SuccessfullyCreated'); this.router.navigate(['/hr/salary-slips', s.id]); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::FailedToCreate'),
      });
    }
  }
}
