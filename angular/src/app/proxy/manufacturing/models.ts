export interface BomDto {
  id?: string;
  bomNumber?: string;
  itemId?: string;
  itemName?: string;
  quantity?: number;
  uom?: string;
  companyId?: string;
  isActive?: boolean;
  isDefault?: boolean;
  totalMaterialCost?: number;
  totalCost?: number;
  items?: BomItemDto[];
  creationTime?: string;
}

export interface BomItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  quantity?: number;
  uom?: string;
  rate?: number;
  amount?: number;
}

export interface CreateBomDto {
  itemId: string;
  quantity?: number;
  uom?: string;
  companyId: string;
  isDefault?: boolean;
  sourceWarehouseId?: string;
  targetWarehouseId?: string;
  items: { itemId: string; itemName: string; quantity: number; uom?: string; rate: number }[];
}

export interface WorkOrderDto {
  id?: string;
  workOrderNumber?: string;
  status?: number;
  itemId?: string;
  itemName?: string;
  bomId?: string;
  quantity?: number;
  producedQuantity?: number;
  materialTransferred?: number;
  percentComplete?: number;
  companyId?: string;
  salesOrderId?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  notes?: string;
  requiredItems?: WorkOrderItemDto[];
  creationTime?: string;
}

export interface WorkOrderItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  requiredQuantity?: number;
  transferredQuantity?: number;
  consumedQuantity?: number;
}

export interface CreateWorkOrderDto {
  itemId: string;
  bomId: string;
  quantity: number;
  companyId: string;
  salesOrderId?: string;
  sourceWarehouseId?: string;
  wipWarehouseId?: string;
  fgWarehouseId?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  notes?: string;
}
