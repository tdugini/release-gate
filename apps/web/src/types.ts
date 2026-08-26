export type Environment = {
  id: string;
  name: string;
  key: string;
  sortOrder: number;
};

export type ProjectSummary = {
  id: string;
  name: string;
  key: string;
  description: string | null;
  environmentCount: number;
  flagCount: number;
};

export type ProjectDetail = {
  id: string;
  name: string;
  key: string;
  description: string | null;
  environments: Environment[];
};

export type FeatureFlagSummary = {
  id: string;
  name: string;
  key: string;
  description: string | null;
  enabled: boolean;
  rolloutPercentage: number;
  environment: string;
};

export type FlagEnvironment = {
  environment: string;
  enabled: boolean;
  rolloutPercentage: number;
  updatedAt: string;
};

export type FeatureFlagDetail = {
  id: string;
  name: string;
  key: string;
  description: string | null;
  environments: FlagEnvironment[];
};

export type FlagChangeStatus = 'applied' | 'pending' | 'approved' | 'rejected';

export type FlagChange = {
  id: string;
  environment: string;
  previousEnabled: boolean;
  previousRolloutPercentage: number;
  requestedEnabled: boolean;
  requestedRolloutPercentage: number;
  status: FlagChangeStatus;
  requestedBy: string;
  requestedAt: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
};
