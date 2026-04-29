// All IDs are Guids (string UUIDs) except TransactionId (long → number) and Points (int → number).

export interface AuthResponseDto {
  message: string;
  userId: string;
  phoneNumber: string;
  name: string;
  totalPoints: number;
  role: 'user' | 'manager';
  sessionId: string;
}

export interface RegisterRequestDto {
  name: string;
  phoneNumber: string;
  password: string;
  mallId: string;
  managerId?: string;
}

export interface LoginRequestDto {
  phoneNumber: string;
  password: string;
  mallId: string;
}

export interface AddTransactionDto {
  phoneNumber: string;
  storeId: string;
  receiptId: string;
  receiptDescription?: string;
  price: number;
}

export interface TransactionResultDto {
  transactionId: number;
  userId: string;
  storeId: string;
  receiptId: string;
  price: number;
  points: number;
  newTotalPoints: number;
}

export interface RedeemCouponDto {
  couponId: string;
}

export interface RedeemCouponBySerialDto {
  serialNumber: string;
}

export interface UserPointsDto {
  totalPoints: number;
}

export interface StoreResponse {
  id: string;
  name: string;
  mallId: string;
  description?: string;
  floorNumber?: string;
  phoneNumber?: string;
  email?: string;
}

export interface OfferResponse {
  id: number;
  storeId: string;
  title: string;
  description?: string;
  startAt: string;
  endAt: string;
  isActive: boolean;
}

export interface CreateOfferRequest {
  storeId: string;
  title: string;
  description?: string;
  startAt: string;
  endAt: string;
  isActive: boolean;
}

export interface CouponResponse {
  id: string;
  type: string;
  description?: string;
  startAt: string;
  endAt: string;
  isActive: boolean;
  costPoint?: number;
}

export interface AnnouncementResponse {
  id: string;
  title: string;
  content: string;
  isActive: boolean;
  isPinned: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  totalPages: number;
  pageNumber: number;
  pageSize: number;
}

export interface ReceiptListItemResponse {
  id: number;
  storeId: string;
  receiptId: string;
  price: number;
  points: number;
  createdAt: string;
}
