import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'myrCurrency', standalone: true })
export class CurrencyFormatPipe implements PipeTransform {
  transform(value: number | null | undefined, currency = 'MYR', decimals = 2): string {
    if (value == null) return '';
    const formatted = value.toFixed(decimals).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return `${currency} ${formatted}`;
  }
}
