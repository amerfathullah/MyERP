import { Component, inject, OnInit, AfterViewInit, ViewChild, ElementRef, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LhdnDashboardStore } from '../store/lhdn-dashboard.store';
import { Chart, ArcElement, BarElement, LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, PieController, BarController, LineController } from 'chart.js';

Chart.register(ArcElement, BarElement, LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, PieController, BarController, LineController);

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
    LocalizationPipe],
  templateUrl: './lhdn-dashboard.component.html',
  styleUrls: ['./lhdn-dashboard.component.scss'],
})
export class LhdnDashboardComponent implements OnInit, AfterViewInit {
  readonly store = inject(LhdnDashboardStore);

  @ViewChild('pieCanvas') pieCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('barCanvas') barCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('trendCanvas') trendCanvas!: ElementRef<HTMLCanvasElement>;

  private pieChart?: Chart;
  private barChart?: Chart;
  private trendChart?: Chart;

  get statusCards(): StatCard[] {
    const stats = this.store.salesStats();
    return [
      { label: 'Valid', count: stats.valid, textClass: 'text-success', icon: 'fa-circle-check' },
      { label: 'Invalid', count: stats.invalid, textClass: 'text-danger', icon: 'fa-circle-xmark' },
      { label: 'Submitted', count: stats.submitted, textClass: 'text-primary', icon: 'fa-clock' },
      { label: 'Cancelled', count: stats.cancelled, textClass: 'text-secondary', icon: 'fa-ban' },
      { label: 'Failed', count: stats.failed, textClass: 'text-warning', icon: 'fa-triangle-exclamation' },
      { label: 'Not Submitted', count: stats.notSubmitted, textClass: 'text-muted', icon: 'fa-file' }];
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
      if (this.trendChart) {
        const trend = this.store.monthlyTrend();
        this.trendChart.data.labels = trend.map((m) => m.month);
        this.trendChart.data.datasets[0].data = trend.map((m) => m.valid);
        this.trendChart.data.datasets[1].data = trend.map((m) => m.invalid);
        this.trendChart.data.datasets[2].data = trend.map((m) => m.submitted);
        this.trendChart.update();
      }
    });
  }

  ngOnInit(): void {
    this.store.loadDashboard();
    this.store.loadMonthlyTrend();
  }

  ngAfterViewInit(): void {
    this.initPieChart();
    this.initBarChart();
    this.initTrendChart();
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
          { label: 'Purchase', data: [0, 0, 0, 0, 0], backgroundColor: '#7c3aed' }],
      },
      options: { responsive: true, plugins: { legend: { position: 'bottom' } } },
    });
  }

  private initTrendChart(): void {
    const ctx = this.trendCanvas?.nativeElement?.getContext('2d');
    if (!ctx) return;
    this.trendChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: [],
        datasets: [
          { label: 'Valid', data: [], borderColor: '#16a34a', backgroundColor: '#16a34a', tension: 0.3 },
          { label: 'Invalid', data: [], borderColor: '#dc2626', backgroundColor: '#dc2626', tension: 0.3 },
          { label: 'Submitted', data: [], borderColor: '#2563eb', backgroundColor: '#2563eb', tension: 0.3 }],
      },
      options: { responsive: true, plugins: { legend: { position: 'bottom' } }, scales: { y: { beginAtZero: true } } },
    });
  }
}
