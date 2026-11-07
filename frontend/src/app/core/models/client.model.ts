export interface Client {
  id: number;
  code: string;
  fullName: string;
  address?: string;
  city?: string;
  phone?: string;
  mobile?: string;
  email?: string;
  taxCode?: string;
  shippingAddress?: string;
  shippingCity?: string;
  sourceId?: number;
  sourceDescription?: string;
  referredByClientId?: number;
}

export interface UpsertClientRequest {
  code: string;
  fullName: string;
  address?: string;
  city?: string;
  phone?: string;
  mobile?: string;
  email?: string;
  taxCode?: string;
  shippingAddress?: string;
  shippingCity?: string;
  sourceId?: number;
  referredByClientId?: number;
}
