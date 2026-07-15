import { describe, it, expect } from 'vitest';
import { CurrencyFormatPipe } from './currency-format.pipe';

describe('CurrencyFormatPipe', () => {
  const pipe = new CurrencyFormatPipe();

  it('should format with default MYR currency', () => {
    expect(pipe.transform(1234.56)).toBe('MYR 1,234.56');
  });

  it('should format with custom currency', () => {
    expect(pipe.transform(500, 'USD')).toBe('USD 500.00');
  });

  it('should handle zero', () => {
    expect(pipe.transform(0)).toBe('MYR 0.00');
  });

  it('should handle large numbers with thousand separators', () => {
    expect(pipe.transform(1234567.89)).toBe('MYR 1,234,567.89');
  });

  it('should handle null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('should handle undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('should respect custom decimal places', () => {
    expect(pipe.transform(99.999, 'MYR', 3)).toBe('MYR 99.999');
  });

  it('should round to specified decimals', () => {
    expect(pipe.transform(10.456, 'MYR', 2)).toBe('MYR 10.46');
  });

  it('should format small amounts', () => {
    expect(pipe.transform(0.5)).toBe('MYR 0.50');
  });

  it('should handle negative amounts', () => {
    expect(pipe.transform(-1500.75)).toBe('MYR -1,500.75');
  });
});
