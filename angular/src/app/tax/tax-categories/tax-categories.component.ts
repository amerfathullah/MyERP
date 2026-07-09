import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TaxCategoryService, TaxRuleService } from '../../proxy/tax/tax.service';
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
    this.categoryService.create(this.categoryForm.getRawValue() as any).subscribe(() => {
      this.toaster.success('Tax category created');
      this.showCategoryForm = false;
      this.categoryForm.reset({ taxType: 'Sales', isActive: true });
      this.loadCategories();
    });
  }

  deleteCategory(id: string): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe(status => {
      if (status === Confirmation.Status.confirm) {
        this.categoryService.delete(id).subscribe(() => {
          this.toaster.success('Deleted');
          this.loadCategories();
        });
      }
    });
  }

  openRuleForm(categoryId: string): void {
    this.showRuleForm = categoryId;
    this.ruleForm.reset({ taxCategoryId: categoryId, rate: 0, isActive: true, priority: 0 });
  }

  saveRule(): void {
    if (this.ruleForm.invalid) { this.ruleForm.markAllAsTouched(); return; }
    this.ruleService.create(this.ruleForm.getRawValue() as any).subscribe(() => {
      this.toaster.success('Tax rule added');
      this.loadRules(this.showRuleForm!);
      this.showRuleForm = null;
    });
  }

  deleteRule(id: string, categoryId: string): void {
    this.ruleService.delete(id).subscribe(() => {
      this.toaster.success('Rule deleted');
      this.loadRules(categoryId);
    });
  }
}
