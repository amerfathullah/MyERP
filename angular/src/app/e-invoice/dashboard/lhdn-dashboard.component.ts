import { Component, inject, OnInit, AfterViewInit, ViewChild, ElementRef, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { LhdnDashboardStore } from '../store/lhdn-dashboard.store';
import { Chart, ArcElement, BarElement, CategoryScale, LinearScale, Tooltip, Legend, PieController, BarController } from 'chart.js';

Chart.register(ArcElement, BarElement, CategoryScale, LinearScale, Tooltip, Legend, PieController, BarController);

interface StatCard {
  label: string;
  count: number;
  textClass: string;
  icon: string;
}

@Component({
  selector: 'app-lhdn-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatButtonModule,
    LoadingOverlayComponent,
  ],
  templateUrl: './lhdn-dashboard.component.html',
  styleUrls: ['./lhdn-dashboard.component.scss'],
})
export class LhdnDashboardComponent implements OnInit, AfterViewInit {
  readonly store = inject(LhdnDashboardStore);

  @ViewChild('pieCanvas') pieCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('barCanvas') barCanvas!: ElementRef<HTMLCanvasElement>;

  private pieChart?: Chart;
  private barChart?: Chart;

  get statusCards(): StatCard[] {
    const stats = this.store.salesStats();
    return [
      { label: 'Valid', count: stats.valid, textClass: 'text-green-600', icon: 'verified' },
      { label: 'Invalid', count: stats.invalid, textClass: 'text-red-600', icon: 'error' },
      { label: 'Submitted', count: stats.submitted, textClass: 'text-blue-600', icon: 'schedule' },
      { label: 'Cancelled', count: stats.cancelled, textClass: 'text-gray-500', icon: 'cancel' },
      { label: 'Failed', count: stats.failed, textClass: 'text-orange-600', icon: 'warning' },
      { label: 'Not Submitted', count: stats.notSubmitted, textClass: 'text-gray-400', icon: 'draft' },
    ];
  }

  constructor() {
    // React to store data changes and update charts
    effect(() => {
      const sales = this.store.salesStats();
      if (this.pieChart) {
        this.pieChart.data.datasets[0].data = [sales.valid, sales.invalid, sales.submitted, sales.cancelled, sales.failed];
        this.pieChart.update();
      }
      if (this.barChart) {
        const purchase = this.store.purchaseStats();
        this.barChart.data.datasets[0].data = [sales.valid, sales.invalid, sales.submitted, sales.cancelled, sales.failed];
        this.barChart.data.datasets[1].data = [purchase.valid, purchase.invalid, purchase.submitted, purchase.cancelled, purchase.failed];
        this.barChart.update();
      }
    });
  }

  ngOnInit(): void {
    this.store.loadDashboard();
  }

  ngAfterViewInit(): void {
    this.initPieChart();
    this.initBarChart();
  }

  private initPieChart(): void {
    const ctx = this.pieCanvas?.nativeElement?.getContext('2d');
    if (!ctx) return;
    this.pieChart = new Chart(ctx, {
      type: 'pie',
      data: {
        labels: ['Valid', 'Invalid', 'Submitted', 'Cancelled', 'Failed'],
        datasets: [{
          data: [0, 0, 0, 0, 0],
          backgroundColor: ['#16a34a', '#dc2626', '#2563eb', '#6b7280', '#ea580c'],
        }],
      },
      options: { responsive: true, plugins: { legend: { position: 'bottom' } } },
    });
  }

  private initBarChart(): void {
    const ctx = this.barCanvas?.nativeElement?.getContext('2d');
    if (!ctx) return;
    this.barChart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Valid', 'Invalid', 'Submitted', 'Cancelled', 'Failed'],
        datasets: [
          { label: 'Sales', data: [0, 0, 0, 0, 0], backgroundColor: '#2563eb' },
          { label: 'Purchase', data: [0, 0, 0, 0, 0], backgroundColor: '#7c3aed' },
        ],
      },
      options: { responsive: true, plugins: { legend: { position: 'bottom' } } },
    });
  }
}
