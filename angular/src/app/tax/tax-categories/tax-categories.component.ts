import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import type { TaxCategoryDto, TaxRuleDto } from '../../proxy/tax/models';

@Component({
  selector: 'app-tax-categories',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './tax-categories.component.html',
  styleUrls: ['./tax-categories.component.scss'],
})
export class TaxCategoriesComponent implements OnInit {
  private categoryService = inject(TaxCategoryService);
  private ruleService = inject(TaxRuleService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private fb = inject(FormBuilder);

  categories = signal<TaxCategoryDto[]>([]);
  rulesMap = signal<Record<string, TaxRuleDto[]>>({});
  isLoading = signal(false);
  showCategoryForm = false;
  showRuleForm: string | null = null;
  editingRuleId: string | null = null;

  categoryForm = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: [''],
    taxType: ['Sales', Validators.required],
    isActive: [true],
  });

  ruleForm = this.fb.group({
    taxCategoryId: ['', Validators.required],
    rate: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    effectiveFrom: ['', Validators.required],
    effectiveTo: [''],
    itemGroupFilter: [''],
    regionFilter: [''],
    priority: [0],
    description: [''],
    isActive: [true],
  });

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    this.isLoading.set(true);
    this.categoryService.getList({ skipCount: 0, maxResultCount: 100, sorting: 'code ASC' }).subscribe(res => {
      this.categories.set(res.items ?? []);
      this.isLoading.set(false);
    });
  }

  loadRules(categoryId: string): void {
    this.ruleService.getList(categoryId, { skipCount: 0, maxResultCount: 50, sorting: '' }).subscribe(res => {
      this.rulesMap.update(m => ({ ...m, [categoryId]: res.items ?? [] }));
    });
  }

  saveCategory(): void {
    if (this.categoryForm.invalid) { this.categoryForm.markAllAsTouched(); return; }
    this.categoryService.create(this.categoryForm.getRawValue() as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.showCategoryForm = false;
        this.categoryForm.reset({ taxType: 'Sales', isActive: true });
        this.loadCategories();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  deleteCategory(id: string): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe(status => {
      if (status === Confirmation.Status.confirm) {
        this.categoryService.delete(id).subscribe({
          next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadCategories(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
        });
      }
    });
  }

  openRuleForm(categoryId: string): void {
    this.showRuleForm = categoryId;
    this.editingRuleId = null;
    this.ruleForm.reset({ taxCategoryId: categoryId, rate: 0, isActive: true, priority: 0 });
  }

  editRule(rule: TaxRuleDto): void {
    this.showRuleForm = rule.taxCategoryId!;
    this.editingRuleId = rule.id!;
    this.ruleForm.reset({
      taxCategoryId: rule.taxCategoryId,
      rate: rule.rate,
      effectiveFrom: rule.effectiveFrom ? rule.effectiveFrom.substring(0, 10) : '',
      effectiveTo: rule.effectiveTo ? rule.effectiveTo.substring(0, 10) : '',
      itemGroupFilter: rule.itemGroupFilter ?? '',
      regionFilter: rule.regionFilter ?? '',
      priority: rule.priority ?? 0,
      description: rule.description ?? '',
      isActive: rule.isActive ?? true,
    });
  }

  saveRule(): void {
    if (this.ruleForm.invalid) { this.ruleForm.markAllAsTouched(); return; }
    const payload = this.ruleForm.getRawValue() as any;
    const request$ = this.editingRuleId
      ? this.ruleService.update(this.editingRuleId, payload)
      : this.ruleService.create(payload);
    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingRuleId ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.loadRules(this.showRuleForm!);
        this.showRuleForm = null;
        this.editingRuleId = null;
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  deleteRule(id: string, categoryId: string): void {
    this.ruleService.delete(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadRules(categoryId); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }
}
