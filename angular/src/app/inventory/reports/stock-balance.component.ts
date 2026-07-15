import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import type { StockBalanceDto } from '../../proxy/inventory/models';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-stock-balance',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  templateUrl: './stock-balance.component.html',
  styleUrls: ['./stock-balance.component.scss'],
})
export class StockBalanceComponent implements OnInit {
  private service = inject(StockBalanceService);

  items = signal<StockBalanceDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.service.getStockBalance({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (result) => {
        this.items.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
