import type { MaterialRequestType } from '../material-request-type.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { DocumentStatus } from '../../core/document-status.enum';

export interface CreateMaterialRequestDto {
  companyId: string;
  requestType: MaterialRequestType;
  requestDate: string;
  requiredByDate?: string | null;
  workOrderId?: string | null;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  notes?: string | null;
  items?: CreateMaterialRequestItemDto[];
}

export interface CreateMaterialRequestItemDto {
  itemId: string;
  itemName: string;
  quantity: number;
  uom?: string;
  warehouseId?: string | null;
}

export interface GetMaterialRequestListDto extends PagedAndSortedResultRequestDto {
  requestType?: MaterialRequestType | null;
  companyId?: string | null;
  filter?: string | null;
  status?: string | null;
}

export interface MaterialRequestDto {
  id?: string;
  requestNumber?: string | null;
  requestType?: MaterialRequestType;
  status?: DocumentStatus;
  requestDate?: string;
  requiredByDate?: string | null;
  companyId?: string;
  workOrderId?: string | null;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  notes?: string | null;
  creationTime?: string;
  items?: MaterialRequestItemDto[];
}

export interface MaterialRequestItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  orderedQuantity?: number;
  receivedQuantity?: number;
  uom?: string | null;
  warehouseId?: string | null;
}
