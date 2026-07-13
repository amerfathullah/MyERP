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

// === Production Plan ===

export interface ProductionPlanDto {
  id?: string;
  planNumber?: string;
  status?: number;
  companyId?: string;
  postingDate?: string;
  combineItems?: boolean;
  ignoreExistingOrderedQty?: boolean;
  considerMinimumOrderQty?: boolean;
  includeSafetyStock?: boolean;
  skipAvailableSubAssemblyItem?: boolean;
  rawMaterialGroupWarehouseId?: string;
  forWarehouseId?: string;
  notes?: string;
  plannedItems?: ProductionPlanItemDto[];
  materialRequirements?: ProductionPlanMrItemDto[];
  creationTime?: string;
}

export interface ProductionPlanItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  bomId?: string;
  plannedQty?: number;
  producedQty?: number;
  warehouseId?: string;
  plannedStartDate?: string;
  salesOrderId?: string;
  materialRequestId?: string;
  workOrderId?: string;
}

export interface ProductionPlanMrItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  requiredQty?: number;
  orderedQty?: number;
  availableQty?: number;
  plannedQty?: number;
  minOrderQty?: number;
  safetyStock?: number;
  uom?: string;
  warehouseId?: string;
  materialRequestId?: string;
  procurementType?: number;
}

export interface CreateProductionPlanDto {
  companyId: string;
  postingDate?: string;
  combineItems?: boolean;
  ignoreExistingOrderedQty?: boolean;
  considerMinimumOrderQty?: boolean;
  includeSafetyStock?: boolean;
  skipAvailableSubAssemblyItem?: boolean;
  rawMaterialGroupWarehouseId?: string;
  forWarehouseId?: string;
  notes?: string;
  items: CreateProductionPlanItemDto[];
}

export interface CreateProductionPlanItemDto {
  itemId: string;
  itemName: string;
  bomId: string;
  plannedQty: number;
  warehouseId?: string;
  plannedStartDate?: string;
  salesOrderId?: string;
  materialRequestId?: string;
}
