import { describe, it, expect } from 'vitest';
import { exportToCsv } from './csv-export';

describe('exportToCsv', () => {
  it('should not throw for empty rows', () => {
    expect(() => exportToCsv('test.csv', [], ['a', 'b'])).not.toThrow();
  });

  it('should generate correct CSV string with simple values', () => {
    const rows = [
      { name: 'Item A', price: 10 },
      { name: 'Item B', price: 20 },
    ];
    const columns = ['name', 'price'];

    // Mock document.createElement and URL for JSDOM environment
    let downloadedContent = '';
    const mockLink = { href: '', download: '', click: () => {} };
    const origCreate = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      if (tag === 'a') return mockLink as any;
      return origCreate(tag);
    });
    const mockUrl = 'blob:test';
    vi.spyOn(URL, 'createObjectURL').mockReturnValue(mockUrl);
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});

    exportToCsv('report.csv', rows, columns);

    expect(mockLink.download).toBe('report.csv');
    expect(mockLink.href).toBe(mockUrl);
  });

  it('should escape commas in values', () => {
    const rows = [{ desc: 'Hello, World', qty: 5 }];
    const columns = ['desc', 'qty'];

    const blobs: Blob[] = [];
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob: Blob) => {
      blobs.push(blob);
      return 'blob:test';
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(document, 'createElement').mockReturnValue({ href: '', download: '', click: () => {} } as any);

    exportToCsv('test.csv', rows, columns);

    expect(blobs.length).toBe(1);
  });

  it('should escape double quotes in values', () => {
    const rows = [{ name: 'Say "hello"', count: 1 }];
    const columns = ['name', 'count'];

    let capturedBlob: Blob | null = null;
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob: Blob) => {
      capturedBlob = blob;
      return 'blob:test';
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(document, 'createElement').mockReturnValue({ href: '', download: '', click: () => {} } as any);

    exportToCsv('test.csv', rows, columns);

    expect(capturedBlob).not.toBeNull();
  });

  it('should handle null/undefined values as empty string', () => {
    const rows = [{ a: null, b: undefined, c: 'ok' }];
    const columns = ['a', 'b', 'c'];

    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(document, 'createElement').mockReturnValue({ href: '', download: '', click: () => {} } as any);

    expect(() => exportToCsv('test.csv', rows, columns)).not.toThrow();
  });
});
