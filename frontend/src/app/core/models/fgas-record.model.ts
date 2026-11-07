export interface FGasRecord {
  id: number;
  quoteId?: number;
  repairId?: number;
  gasType: string;
  kilogramsOfGas: number;
  isRefill: boolean;
  hasExtraCharge: boolean;
  recordedAt: string;
}

export interface UpsertFGasRecordRequest {
  quoteId?: number;
  repairId?: number;
  gasType: string;
  kilogramsOfGas: number;
  isRefill: boolean;
  hasExtraCharge: boolean;
}
