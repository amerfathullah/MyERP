import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AccountCategoryService } from '../../proxy/accounting/account-category.service';
import { ToasterService } from '@abp/ng.theme.shared';

interface AccountCategoryDto {
  id?: string;
  name?: string;
  rootType?: string;
  description?: string;
}

@Component({
  selector: 'app-account-category-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::AccountCategories' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showCreate = !showCreate">
            <i class="fa fa-plus me-1"></i>{{ '::NewCategory' | abpLocalization }}
          </button>
        </div>

        @if (showCreate) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-3">
              <div class="col-md-4">
                <label class="form-label">{{ '::Name' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" [(ngModel)]="newCategory.name" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::RootType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newCategory.rootType">
                  <option value="Asset">Asset</option>
                  <option value="Liability">Liability</option>
                  <option value="Equity">Equity</option>
                  <option value="Income">Income</option>
                  <option value="Expense">Expense</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" [(ngModel)]="newCategory.description" />
              </div>
              <div class="col-md-1 d-flex align-items-end">
                <button class="btn btn-success btn-sm w-100" (click)="create()" [disabled]="!newCategory.name || !newCategory.rootType">
                  <i class="fa fa-check"></i>
                </button>
              </div>
            </div>
          </div>
        }

        <div class="card-body p-0">
          @if (categories().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-layer-group fa-3x text-muted mb-3"></i>
              <p class="text-muted">{{ '::NoAccountCategoriesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::RootType' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (cat of categories(); track cat.id) {
                  <tr>
                    <td><strong>{{ cat.name }}</strong></td>
                    <td>
                      <span class="badge"
                        [class.bg-primary]="cat.rootType === 'Asset'"
                        [class.bg-danger]="cat.rootType === 'Liability'"
                        [class.bg-success]="cat.rootType === 'Equity'"
                        [class.bg-info]="cat.rootType === 'Income'"
                        [class.bg-warning]="cat.rootType === 'Expense'">
                        {{ cat.rootType }}
                      </span>
                    </td>
                    <td class="text-muted">{{ cat.description || '—' }}</td>
                    <td>
                      <button class="btn btn-outline-danger btn-sm" (click)="remove(cat)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class AccountCategoryListComponent implements OnInit {
  private accountCategoryService = inject(AccountCategoryService);
  private toaster = inject(ToasterService);

  categories = signal<AccountCategoryDto[]>([]);
  showCreate = false;
  newCategory = { name: '', rootType: 'Asset', description: '' };

  ngOnInit() { this.loadData(); }

  loadData() {
    this.accountCategoryService.getList({ maxResultCount: 100, skipCount: 0, sorting: '' }).subscribe(res => {
      this.categories.set(res.items ?? []);
    });
  }

  create() {
    this.accountCategoryService.create(this.newCategory as any).subscribe({
      next: () => {
        this.toaster.success('Category created');
        this.showCreate = false;
        this.newCategory = { name: '', rootType: 'Asset', description: '' };
        this.loadData();
      },
      error: () => {}
    });
  }

  remove(cat: AccountCategoryDto) {
    if (!confirm(`Delete category "${cat.name}"?`)) return;
    this.accountCategoryService.delete(cat.id).subscribe({
      next: () => { this.toaster.success('Deleted'); this.loadData(); },
      error: () => {}
    });
  }
}
