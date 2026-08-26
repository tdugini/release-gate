export type RuntimeFlag = {
  key: string;
  enabled: boolean;
};

export type RuntimeSnapshot = {
  projectKey: string;
  environment: string;
  subjectKey: string;
  generatedAt: string;
  flags: RuntimeFlag[];
};

export type ReleaseGateClientOptions = {
  baseUrl: string;
  projectKey: string;
  environment: string;
  fetch?: typeof globalThis.fetch;
};

export class ReleaseGateClient {
  private readonly baseUrl: string;
  private readonly projectKey: string;
  private readonly environment: string;
  private readonly fetchImpl: typeof globalThis.fetch;
  private subjectKey: string | null = null;
  private snapshot: RuntimeSnapshot | null = null;
  private flags = new Map<string, boolean>();

  constructor(options: ReleaseGateClientOptions) {
    this.baseUrl = normalizeRequired(options.baseUrl, 'baseUrl').replace(/\/+$/, '');
    this.projectKey = normalizeRequired(options.projectKey, 'projectKey');
    this.environment = normalizeRequired(options.environment, 'environment');
    this.fetchImpl = options.fetch ?? globalThis.fetch;

    if (!this.fetchImpl) {
      throw new Error('ReleaseGateClient requires a fetch implementation.');
    }
  }

  get initialized(): boolean {
    return this.snapshot !== null;
  }

  get currentSnapshot(): RuntimeSnapshot | null {
    return this.snapshot;
  }

  async initialize(subjectKey: string): Promise<RuntimeSnapshot> {
    this.subjectKey = normalizeRequired(subjectKey, 'subjectKey');
    return this.refresh();
  }

  async refresh(): Promise<RuntimeSnapshot> {
    if (!this.subjectKey) {
      throw new Error('ReleaseGateClient must be initialized with a subject key before refresh().');
    }

    const url = new URL(
      `${this.baseUrl}/api/runtime/projects/${encodeURIComponent(this.projectKey)}` +
        `/environments/${encodeURIComponent(this.environment)}/snapshot`,
    );
    url.searchParams.set('subjectKey', this.subjectKey);

    const response = await this.fetchImpl(url, {
      headers: {
        Accept: 'application/json',
      },
    });

    if (!response.ok) {
      throw new Error(`ReleaseGate snapshot request failed with status ${response.status}.`);
    }

    const snapshot = validateSnapshot(await response.json());

    this.snapshot = snapshot;
    this.flags = new Map(snapshot.flags.map((flag) => [flag.key, flag.enabled]));

    return snapshot;
  }

  isEnabled(flagKey: string, fallback = false): boolean {
    const normalizedFlagKey = flagKey.trim();
    if (!normalizedFlagKey) return fallback;

    return this.flags.get(normalizedFlagKey) ?? fallback;
  }
}

function normalizeRequired(value: string, field: string): string {
  const normalized = value?.trim();
  if (!normalized) {
    throw new Error(`ReleaseGateClient option '${field}' is required.`);
  }

  return normalized;
}

function validateSnapshot(value: unknown): RuntimeSnapshot {
  if (!value || typeof value !== 'object') {
    throw new Error('ReleaseGate returned an invalid runtime snapshot.');
  }

  const snapshot = value as Partial<RuntimeSnapshot>;

  if (
    typeof snapshot.projectKey !== 'string' ||
    typeof snapshot.environment !== 'string' ||
    typeof snapshot.subjectKey !== 'string' ||
    typeof snapshot.generatedAt !== 'string' ||
    !Array.isArray(snapshot.flags) ||
    snapshot.flags.some(
      (flag) =>
        !flag ||
        typeof flag !== 'object' ||
        typeof (flag as RuntimeFlag).key !== 'string' ||
        typeof (flag as RuntimeFlag).enabled !== 'boolean',
    )
  ) {
    throw new Error('ReleaseGate returned an invalid runtime snapshot.');
  }

  return snapshot as RuntimeSnapshot;
}
