import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

interface CouponCodeDto {
  id: string;
  code: string;
  couponName: string;
  couponType: number;
  pricingRuleId: string;
  maximumUse: number;
  used: number;
  isEnabled: boolean;
  validFrom?: string;
  validUpto?: string;
  customerId?: string;
  description?: string;
}

@Component({
  selector: 'app-coupon-code-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::CouponCodes' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
            <i class="fa fa-plus me-1"></i>{{ '::NewCoupon' | abpLocalization }}
          </button>
        </div>

        @if (showCreateForm) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-3">
              <div class="col-md-3">
                <label class="form-label">{{ '::CouponName' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" [(ngModel)]="newCoupon.couponName" />
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::CouponType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newCoupon.couponType">
                  <option [value]="0">Promotional</option>
                  <option [value]="1">Gift Card</option>
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::MaxUses' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="newCoupon.maximumUse" />
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::ValidFrom' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newCoupon.validFrom" />
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::ValidUpto' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newCoupon.validUpto" />
              </div>
              <div class="col-md-1 d-flex align-items-end">
                <button class="btn btn-success btn-sm w-100" (click)="create()" [disabled]="!newCoupon.couponName">
                  <i class="fa fa-check"></i>
                </button>
              </div>
            </div>
          </div>
        }

        <div class="card-body p-0">
          @if (coupons().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-ticket fa-3x text-muted mb-3"></i>
              <p class="text-muted mb-3">{{ '::NoCouponCodesYet' | abpLocalization }}</p>
              <button class="btn btn-outline-primary btn-sm" (click)="showCreateForm = true">
                <i class="fa fa-plus me-1"></i>{{ '::NewCoupon' | abpLocalization }}
              </button>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::Code' | abpLocalization }}</th>
                  <th>{{ '::CouponName' | abpLocalization }}</th>
                  <th>{{ '::CouponType' | abpLocalization }}</th>
                  <th>{{ '::Usage' | abpLocalization }}</th>
                  <th>{{ '::Validity' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (coupon of coupons(); track coupon.id) {
                  <tr>
                    <td><code class="text-primary">{{ coupon.code }}</code></td>
                    <td>{{ coupon.couponName }}</td>
                    <td>
                      <span class="badge" [class.bg-info]="coupon.couponType === 0" [class.bg-warning]="coupon.couponType === 1">
                        {{ coupon.couponType === 0 ? 'Promotional' : 'Gift Card' }}
                      </span>
                    </td>
                    <td>
                      <span [class.text-danger]="coupon.used >= coupon.maximumUse && coupon.maximumUse > 0">
                        {{ coupon.used }} / {{ coupon.maximumUse || '∞' }}
                      </span>
                    </td>
                    <td>
                      @if (coupon.validFrom || coupon.validUpto) {
                        <small class="text-muted">
                          {{ coupon.validFrom | date:'dd/MM/yyyy' }} – {{ coupon.validUpto | date:'dd/MM/yyyy' }}
                        </small>
                      } @else {
                        <small class="text-muted">{{ '::NoExpiry' | abpLocalization }}</small>
                      }
                    </td>
                    <td>
                      @if (coupon.isEnabled) {
                        <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ '::Disabled' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-secondary" (click)="toggle(coupon)" [title]="coupon.isEnabled ? 'Disable' : 'Enable'">
                          <i class="fa" [class.fa-toggle-on]="coupon.isEnabled" [class.fa-toggle-off]="!coupon.isEnabled"></i>
                        </button>
                        <button class="btn btn-outline-danger" (click)="remove(coupon)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>

        @if (totalCount() > 10) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage()" (pageChange)="onPageChange($event)" />
          </div>
        }
      </div>
    </div>
  `
})
export class CouponCodeListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  coupons = signal<CouponCodeDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(1);
  showCreateForm = false;
  newCoupon = { couponName: '', couponType: 0, maximumUse: 0, validFrom: '', validUpto: '' };

  ngOnInit() { this.loadData(); }

  loadData() {
    const skip = (this.currentPage() - 1) * 10;
    this.http.get<any>(`/api/app/coupon-code?skipCount=${skip}&maxResultCount=10`).subscribe(res => {
      this.coupons.set(res.items ?? []);
      this.totalCount.set(res.totalCount ?? 0);
    });
  }

  create() {
    const dto: any = {
      couponName: this.newCoupon.couponName,
      couponType: +this.newCoupon.couponType,
      maximumUse: this.newCoupon.maximumUse || 0,
      pricingRuleId: '00000000-0000-0000-0000-000000000000', // placeholder — ideally select from pricing rules
      validFrom: this.newCoupon.validFrom || undefined,
      validUpto: this.newCoupon.validUpto || undefined,
    };
    this.http.post<CouponCodeDto>('/api/app/coupon-code', dto).subscribe({
      next: () => { this.toaster.success('Coupon created'); this.showCreateForm = false; this.loadData(); },
      error: () => {}
    });
  }

  toggle(coupon: CouponCodeDto) {
    this.http.post(`/api/app/coupon-code/${coupon.id}/toggle`, {}).subscribe({
      next: () => { this.toaster.success(coupon.isEnabled ? 'Disabled' : 'Enabled'); this.loadData(); },
      error: () => {}
    });
  }

  remove(coupon: CouponCodeDto) {
    if (!confirm('Delete this coupon code?')) return;
    this.http.delete(`/api/app/coupon-code/${coupon.id}`).subscribe({
      next: () => { this.toaster.success('Deleted'); this.loadData(); },
      error: () => {}
    });
  }

  onPageChange(event: any) {
    this.currentPage.set(event.pageIndex);
    this.loadData();
  }
}
