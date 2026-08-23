export const OBLIGATION_STATUSES = [
  'Pending',
  'Completed',
  'Skipped',
  'Overdue',
  'Archived',
] as const;
export type ObligationStatus = (typeof OBLIGATION_STATUSES)[number];

export const OBLIGATION_TYPES = [
  'Task',
  'Payment',
  'Document',
  'Subscription',
  'Contract',
  'Debt',
  'Maintenance',
  'Appointment',
  'Custom',
] as const;
export type ObligationType = (typeof OBLIGATION_TYPES)[number];

export const OBLIGATION_PRIORITIES = ['Low', 'Medium', 'High'] as const;
export type ObligationPriority = (typeof OBLIGATION_PRIORITIES)[number];

export interface Obligation {
  id: string;
  type: ObligationType;
  title: string;
  notes: string | null;
  startDate: string | null;
  dueDate: string | null;
  endDate: string | null;
  status: ObligationStatus;
  priority: ObligationPriority;
  categoryId: string | null;
  extraFields: string;
  isRecurring: boolean;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
}

export interface ObligationListResponse {
  items: Obligation[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface Category {
  id: string;
  userId: string | null;
  name: string;
  isDefault: boolean;
  icon: string | null;
}

export interface AuthUser {
  id: string;
  phoneNumber: string;
  displayName: string | null;
}

export interface VerifyOtpResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: AuthUser;
}

export interface RefreshTokenResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
}
