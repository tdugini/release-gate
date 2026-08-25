import type {
  FeatureFlagDetail,
  FeatureFlagSummary,
  ProjectDetail,
  ProjectSummary,
} from '../types';

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed with ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export const api = {
  projects: {
    list: () => request<ProjectSummary[]>('/api/projects'),
    get: (projectKey: string) =>
      request<ProjectDetail>(`/api/projects/${projectKey}`),
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
      request<FeatureFlagDetail>(
        `/api/projects/${projectKey}/flags/${flagKey}`,
      ),
    create: (
      projectKey: string,
      input: { name: string; key: string; description?: string },
    ) =>
      request(`/api/projects/${projectKey}/flags`, {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    updateEnvironment: (
      projectKey: string,
      flagKey: string,
      environment: string,
      input: { enabled: boolean; rolloutPercentage: number },
    ) =>
      request(
        `/api/projects/${projectKey}/flags/${flagKey}/environments/${environment}`,
        {
          method: 'PATCH',
          body: JSON.stringify(input),
        },
      ),
  },
};
