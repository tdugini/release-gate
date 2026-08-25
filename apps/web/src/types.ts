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
