import type {
  FeatureFlagDetail,
  FeatureFlagSummary,
  FlagChange,
  FlagEnvironment,
  ProjectDetail,
  ProjectSummary,
} from '../types';

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

type ProblemDetails = {
  title?: string;
  message?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const raw = await response.text();
    let problem: ProblemDetails | null = null;

    try {
      problem = raw ? (JSON.parse(raw) as ProblemDetails) : null;
    } catch {
      problem = null;
    }

    const firstFieldError = problem?.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined;

    throw new ApiError(
      problem?.message ?? firstFieldError ?? problem?.title ?? raw ?? `Request failed with ${response.status}`,
      response.status,
      problem?.errors ?? {},
    );
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  projects: {
    list: () => request<ProjectSummary[]>('/api/projects'),
    get: (projectKey: string) => request<ProjectDetail>(`/api/projects/${projectKey}`),
    create: (input: { name: string; key: string; description?: string }) =>
      request<ProjectDetail>('/api/projects', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
  },
  flags: {
    list: (projectKey: string, environment: string) =>
      request<FeatureFlagSummary[]>(
        `/api/projects/${projectKey}/flags?environment=${environment}`,
      ),
    get: (projectKey: string, flagKey: string) =>
      request<FeatureFlagDetail>(`/api/projects/${projectKey}/flags/${flagKey}`),
    changes: (projectKey: string, flagKey: string, environment?: string) => {
      const query = environment ? `?environment=${encodeURIComponent(environment)}` : '';
      return request<FlagChange[]>(
        `/api/projects/${projectKey}/flags/${flagKey}/changes${query}`,
      );
    },
    create: (
      projectKey: string,
      input: { name: string; key: string; description?: string },
    ) =>
      request<{ id: string; name: string; key: string; description: string | null }>(
        `/api/projects/${projectKey}/flags`,
        {
          method: 'POST',
          body: JSON.stringify(input),
        },
      ),
    updateEnvironment: (
      projectKey: string,
      flagKey: string,
      environment: string,
      input: { enabled: boolean; rolloutPercentage: number },
    ) =>
      request<FlagEnvironment | FlagChange>(
        `/api/projects/${projectKey}/flags/${flagKey}/environments/${environment}`,
        {
          method: 'PATCH',
          body: JSON.stringify(input),
        },
      ),
    approveChange: (projectKey: string, flagKey: string, changeId: string) =>
      request<FlagChange>(
        `/api/projects/${projectKey}/flags/${flagKey}/changes/${changeId}/approve`,
        { method: 'POST' },
      ),
    rejectChange: (projectKey: string, flagKey: string, changeId: string) =>
      request<FlagChange>(
        `/api/projects/${projectKey}/flags/${flagKey}/changes/${changeId}/reject`,
        { method: 'POST' },
      ),
  },
};
